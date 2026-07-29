using System.IO;
using System.Security.Cryptography;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using TextRecast.Core.Abstractions;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public sealed class LocalSlmTextFormatter : ITextFormatter
{
    private const int ContextSafetyMarginTokens = 32;
    private const int MaxFinalSummaryCharacters = 6000;
    private const int MaxChunkCharacters = 450;
    private const int MinimumOutputTokens = 64;
    private readonly SlmModelOptions _options;
    private readonly ISlmPromptBuilder _promptBuilder;
    private readonly SemaphoreSlim _inferenceGate = new(1, 1);
    private readonly CancellationTokenSource _shutdownCancellation = new();
    private LLamaWeights? _weights;
    private StatelessExecutor? _executor;
    private int _disposeState;

    public LocalSlmTextFormatter(SlmModelOptions options)
        : this(options, new ChatMlPromptBuilder())
    {
    }

    public LocalSlmTextFormatter(SlmModelOptions options, ISlmPromptBuilder promptBuilder)
    {
        _options = options;
        _promptBuilder = promptBuilder;
    }

    public async Task<string> FormatAsync(FormatTextRequest request, CancellationToken cancellationToken)
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
            var output = request.Operation == FormatOperation.Summarize &&
                         request.Text.Length > MaxFinalSummaryCharacters
                ? await FormatHierarchicalSummaryAsync(request, linkedCancellation.Token)
                : ShouldFormatInChunks(request)
                    ? await FormatInChunksAsync(request, linkedCancellation.Token)
                    : await FormatSingleAsync(request, linkedCancellation.Token);
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
        CancellationToken cancellationToken)
    {
        var chunks = TextChunker.Split(request.Text, MaxChunkCharacters);
        var output = new StringBuilder(request.Text.Length);
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunkRequest = request with { Text = chunk.Text };
            var formattedChunk = await FormatSingleAsync(chunkRequest, cancellationToken);
            output.Append(formattedChunk.Trim());
            output.Append(chunk.Separator);
        }

        return output.ToString().Trim();
    }

    private async Task<string> FormatHierarchicalSummaryAsync(
        FormatTextRequest request,
        CancellationToken cancellationToken)
    {
        var currentText = request.Text;
        for (var level = 0; currentText.Length > MaxFinalSummaryCharacters; level++)
        {
            if (level >= 8)
            {
                throw new TextFormattingException(
                    "The local model could not reduce this selection reliably. Try a smaller section.");
            }

            var levelOutput = new StringBuilder(currentText.Length / 2);
            foreach (var chunk in TextChunker.Split(currentText, MaxChunkCharacters))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chunkRequest = request with { Text = chunk.Text };
                var summary = await FormatSingleAsync(chunkRequest, cancellationToken);
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

        return await FormatSingleAsync(request with { Text = currentText }, cancellationToken);
    }

    private async Task<string> FormatSingleAsync(
        FormatTextRequest request,
        CancellationToken cancellationToken)
    {
        var prompt = _promptBuilder.Build(request);
        return await Task.Run(
            () => InferAsync(request, prompt, cancellationToken),
            cancellationToken);
    }

    private static bool ShouldFormatInChunks(FormatTextRequest request)
    {
        return request.Text.Length > MaxChunkCharacters &&
               request.Operation != FormatOperation.Summarize;
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
        CancellationToken cancellationToken)
    {
        var inferenceParams = new InferenceParams
        {
            MaxTokens = GetOutputTokenBudget(request, prompt),
            AntiPrompts = ["<|im_end|>", "<|im_start|>"],
            SamplingPipeline = new GreedySamplingPipeline()
        };

        var output = new StringBuilder();
        await foreach (var token in _executor!.InferAsync(
                           prompt,
                           inferenceParams,
                           cancellationToken))
        {
            output.Append(token);
        }

        return RemoveProtocolMarkers(output.ToString());
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

        var inputWords = ChatMlPromptBuilder.CountWords(request.Text);
        var expectedOutputWords = request.Operation switch
        {
            FormatOperation.Shorten => ChatMlPromptBuilder.GetShorterWordTarget(inputWords),
            FormatOperation.Lengthen => ChatMlPromptBuilder.GetLongerWordTarget(inputWords),
            FormatOperation.Summarize => ChatMlPromptBuilder.GetSummaryWordTarget(inputWords),
            _ => inputWords
        };
        var desiredTokens = Math.Clamp(
            (int)Math.Ceiling(expectedOutputWords * 1.9) + 48,
            MinimumOutputTokens,
            _options.Profile.MaxOutputTokens);
        return Math.Min(desiredTokens, availableTokens);
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

    private static string RemoveProtocolMarkers(string output)
    {
        return output
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|im_start|>", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
