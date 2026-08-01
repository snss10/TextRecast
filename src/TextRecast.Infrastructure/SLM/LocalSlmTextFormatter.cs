using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using TextRecast.Core.Abstractions;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public sealed class LocalSlmTextFormatter : ITextFormatter
{
    private const int ContextSafetyMarginTokens = 32;
    private const int MinimumOutputTokens = 64;
    private readonly SlmModelOptions _options;
    private readonly ISlmModelAdapter _adapter;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private LLamaWeights? _weights;
    private StatelessExecutor? _executor;
    private int _disposeState;

    public LocalSlmTextFormatter(SlmModelOptions options)
        : this(options, SlmModelAdapterRegistry.Default.Resolve(options.Profile))
    {
    }

    internal LocalSlmTextFormatter(SlmModelOptions options, ISlmModelAdapter adapter)
    {
        _options = options;
        _adapter = adapter;
    }

    public async Task<string> FormatAsync(FormatTextRequest request, CancellationToken cancellationToken)
    {
        return await FormatCoreAsync(request, null, cancellationToken);
    }

    internal async Task LoadModelAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownCancellation.Token);
        await _inferenceGate.WaitAsync(linkedCancellation.Token);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
            await EnsureModelLoadedAsync(linkedCancellation.Token);
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    internal async Task<SlmFormattingMeasurement> FormatMeasuredAsync(
        FormatTextRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var stopwatch = Stopwatch.StartNew();
        long firstTokenTimestamp = -1;
        var output = await FormatCoreAsync(
            request,
            () => Interlocked.CompareExchange(
                ref firstTokenTimestamp,
                Stopwatch.GetTimestamp(),
                -1),
            cancellationToken);
        stopwatch.Stop();

        if (firstTokenTimestamp < 0)
        {
            throw new TextFormattingException("The local model returned no measurable output tokens.");
        }

        var firstToken = Stopwatch.GetElapsedTime(
            startedAt,
            firstTokenTimestamp);
        var outputTokens = _weights!.Tokenize(output, false, false, Encoding.UTF8).Length;
        return new SlmFormattingMeasurement(output, firstToken, stopwatch.Elapsed, outputTokens);
    }

    private async Task<string> FormatCoreAsync(
        FormatTextRequest request,
        Action? firstTokenObserved,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
        if (!request.Text.Any(char.IsLetterOrDigit))
        {
            throw new TextFormattingException("The selection does not contain enough editable text.");
        }

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownCancellation.Token);
        await _inferenceGate.WaitAsync(linkedCancellation.Token);

        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeState) != 0, this);
            await EnsureModelLoadedAsync(linkedCancellation.Token);
            var output = CanFormatSingle(request)
                ? await FormatSingleAsync(request, firstTokenObserved, linkedCancellation.Token)
                : request.Operation == FormatOperation.Summarize
                    ? await FormatHierarchicalSummaryAsync(
                        request,
                        firstTokenObserved,
                        linkedCancellation.Token)
                    : await FormatInChunksAsync(
                        request,
                        firstTokenObserved,
                        linkedCancellation.Token);
            EnsureOutputPresent(output);
            return output;
        }
        finally
        {
            _inferenceGate.Release();
        }
    }

    private async Task<string> FormatInChunksAsync(
        FormatTextRequest request,
        Action? firstTokenObserved,
        CancellationToken cancellationToken)
    {
        var chunks = SplitToFit(request);
        var output = new StringBuilder(request.Text.Length);
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkRequest = request with { Text = chunk.Text };
            var formattedChunk = await FormatSingleAsync(
                chunkRequest,
                firstTokenObserved,
                cancellationToken);
            output.Append(formattedChunk.Trim());
            output.Append(chunk.Separator);
        }

        return output.ToString().Trim();
    }

    private async Task<string> FormatHierarchicalSummaryAsync(
        FormatTextRequest request,
        Action? firstTokenObserved,
        CancellationToken cancellationToken)
    {
        var currentText = request.Text;
        for (var level = 0; !CanFormatSingle(request with { Text = currentText }); level++)
        {
            if (level >= 8)
            {
                throw new TextFormattingException(
                    "The local model could not reduce this selection reliably. Try a smaller section.");
            }

            var levelOutput = new StringBuilder(currentText.Length / 2);
            foreach (var chunk in SplitToFit(request with { Text = currentText }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkRequest = request with { Text = chunk.Text };
                var summary = await FormatSingleAsync(
                    chunkRequest,
                    firstTokenObserved,
                    cancellationToken);
                levelOutput.Append(summary.Trim());
                levelOutput.Append(' ');
            }

            var reducedText = levelOutput.ToString().Trim();
            if (reducedText.Length >= currentText.Length)
            {
                throw new TextFormattingException(
                    "The local model could not reduce this selection reliably. Try a smaller section.");
            }

            currentText = reducedText;
        }

        return await FormatSingleAsync(
            request with { Text = currentText },
            firstTokenObserved,
            cancellationToken);
    }

    private async Task<string> FormatSingleAsync(
        FormatTextRequest request,
        Action? firstTokenObserved,
        CancellationToken cancellationToken)
    {
        var prompt = _adapter.BuildPrompt(request);
        return await Task.Run(
            () => InferAsync(request, prompt, firstTokenObserved, cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<TextChunker.Chunk> SplitToFit(FormatTextRequest request)
    {
        var maximumCharacters = Math.Max(64, request.Text.Length - 1);
        while (true)
        {
            var chunks = TextChunker.Split(request.Text, maximumCharacters);
            if (chunks.Count > 1 && chunks.All(chunk =>
                    CanFormatSingle(request with { Text = chunk.Text })))
            {
                return chunks;
            }

            if (maximumCharacters == 64)
            {
                throw new TextFormattingException(
                    "This selection exceeds the local model's context capacity. Try a smaller section.");
            }

            maximumCharacters = Math.Max(64, maximumCharacters * 3 / 4);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _shutdownCancellation.Cancel();
        if (_inferenceGate.Wait(TimeSpan.FromSeconds(2)))
        {
            _weights?.Dispose();
            _inferenceGate.Release();
            _inferenceGate.Dispose();
            _shutdownCancellation.Dispose();
        }
    }

    private async Task<string> InferAsync(
        FormatTextRequest request,
        string prompt,
        Action? firstTokenObserved,
        CancellationToken cancellationToken)
    {
        var inferenceParams = new InferenceParams
        {
            MaxTokens = GetOutputTokenBudget(request, prompt),
            AntiPrompts = [.. _adapter.StopSequences],
            SamplingPipeline = _adapter.CreateSamplingPipeline()
        };

        var output = new StringBuilder();
        await foreach (var token in _executor!.InferAsync(
                           prompt,
                           inferenceParams,
                           cancellationToken))
        {
            if (token.Length > 0)
            {
                firstTokenObserved?.Invoke();
            }

            output.Append(token);
        }

        return _adapter.CleanOutput(output.ToString());
    }

    private int GetOutputTokenBudget(FormatTextRequest request, string prompt)
    {
        var promptTokens = _weights!.Tokenize(prompt, true, true, Encoding.UTF8).Length;
        var availableTokens = checked((int)_options.Profile.ContextSize) - promptTokens - ContextSafetyMarginTokens;
        if (availableTokens < MinimumOutputTokens)
        {
            throw new TextFormattingException(
                "This selection exceeds the local model's context capacity. Try a smaller section.");
        }

        var desiredTokens = Math.Min(
            GetEstimatedOutputTokens(request),
            _options.Profile.MaxOutputTokens);
        return Math.Min(desiredTokens, availableTokens);
    }

    private bool CanFormatSingle(FormatTextRequest request)
    {
        var prompt = _adapter.BuildPrompt(request);
        var promptTokens = _weights!.Tokenize(prompt, true, true, Encoding.UTF8).Length;
        return CanFitSingleRequest(
            promptTokens,
            GetEstimatedOutputTokens(request),
            checked((int)_options.Profile.ContextSize),
            _options.Profile.MaxOutputTokens,
            request.Operation is FormatOperation.Shorten or FormatOperation.Summarize);
    }

    internal static bool CanFitSingleRequest(
        int promptTokens,
        int estimatedOutputTokens,
        int contextTokens,
        int maximumOutputTokens,
        bool outputMayBeCapped = false)
    {
        if (!outputMayBeCapped && estimatedOutputTokens > maximumOutputTokens)
        {
            return false;
        }

        var reservedOutputTokens = Math.Min(estimatedOutputTokens, maximumOutputTokens);
        return (long)promptTokens + ContextSafetyMarginTokens + reservedOutputTokens <= contextTokens;
    }

    private int GetEstimatedOutputTokens(FormatTextRequest request)
    {
        var outputWordCapacity = _adapter.GetOutputWordCapacity(request);
        return Math.Max(
            MinimumOutputTokens,
            checked((int)Math.Ceiling(outputWordCapacity * 1.9) + 48));
    }

    private static void EnsureOutputPresent(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new TextFormattingException("The local model returned no formatted text.");
        }
    }

    private async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
    {
        if (_executor is not null)
        {
            return;
        }

        if (!File.Exists(_options.ModelPath))
        {
            throw new FileNotFoundException(
                "The local SLM model was not found in the Models folder.",
                _options.ModelPath);
        }

        if (new FileInfo(_options.ModelPath).Length != _options.Profile.ExpectedFileSize)
        {
            throw new InvalidDataException("The local SLM model has an unexpected file size.");
        }

        await VerifyModelAsync(cancellationToken);

        await Task.Run(() =>
        {
            if (!NativeLibraryConfig.LLama.LibraryHasLoaded)
            {
                NativeLibraryConfig.LLama.WithLogCallback(static (_, _) => { });
            }

            var parameters = new ModelParams(_options.ModelPath)
            {
                ContextSize = _options.Profile.ContextSize,
                Threads = _options.ThreadCount,
                GpuLayerCount = 0
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _executor = new StatelessExecutor(_weights, parameters);
        }, CancellationToken.None);

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task VerifyModelAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            _options.ModelPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexStringLower(hash);
        if (!actual.Equals(_options.Profile.ExpectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The local SLM model failed its integrity check.");
        }
    }

}

internal sealed record SlmFormattingMeasurement(
    string Output,
    TimeSpan FirstTokenLatency,
    TimeSpan TotalLatency,
    int OutputTokens);
