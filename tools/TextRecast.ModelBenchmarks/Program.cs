using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.ModelBenchmarks;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = BenchmarkOptions.Parse(args);
            var run = await RunAsync(options);
            var outputPath = Path.GetFullPath(options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(
                outputPath,
                JsonSerializer.Serialize(run, JsonOptions));
            Console.WriteLine($"Qualification score: {run.AverageQualityScore:F2}/10");
            Console.WriteLine($"Results: {outputPath}");
            return 0;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(BenchmarkOptions.Usage);
            return 1;
        }
    }

    private static async Task<ModelBenchmarkRun> RunAsync(BenchmarkOptions options)
    {
        var modelPath = Path.GetFullPath(options.ModelPath);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The local GGUF model was not found.", modelPath);
        }

        var fileInfo = new FileInfo(modelPath);
        var hash = await ComputeSha256Async(modelPath);
        var profile = new SlmModelProfile
        {
            Id = Path.GetFileNameWithoutExtension(modelPath),
            AdapterId = options.AdapterId,
            FileName = fileInfo.Name,
            DownloadUri = new Uri("https://localhost/model-benchmark"),
            ExpectedSha256 = hash,
            ExpectedFileSize = fileInfo.Length,
            ContextSize = options.ContextSize,
            MaxOutputTokens = options.MaxOutputTokens
        };

        using var formatter = new LocalSlmTextFormatter(new SlmModelOptions
        {
            Profile = profile,
            ModelPath = modelPath,
            ThreadCount = options.ThreadCount
        });
        var results = new List<ModelQualificationResult>(ModelQualificationCorpus.All.Count);
        foreach (var testCase in ModelQualificationCorpus.All)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var output = await formatter.FormatAsync(testCase.Request, CancellationToken.None);
                stopwatch.Stop();
                results.Add(ModelQualificationEvaluator.Evaluate(testCase, output, stopwatch.Elapsed));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                stopwatch.Stop();
                results.Add(ModelQualificationEvaluator.Failure(testCase, exception, stopwatch.Elapsed));
            }
        }

        var averageScore = results.Count == 0
            ? 0
            : Math.Round(results.Average(result => result.QualityScore), 2);
        return new ModelBenchmarkRun(
            DateTimeOffset.UtcNow,
            profile.Id,
            options.AdapterId,
            hash,
            fileInfo.Length,
            Environment.OSVersion.ToString(),
            Environment.ProcessorCount,
            options.ThreadCount,
            Process.GetCurrentProcess().PeakWorkingSet64,
            averageScore,
            results);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}

public sealed record ModelBenchmarkRun(
    DateTimeOffset StartedAtUtc,
    string ModelId,
    string AdapterId,
    string ModelSha256,
    long ModelFileSize,
    string OperatingSystem,
    int LogicalProcessors,
    int Threads,
    long PeakWorkingSetBytes,
    double AverageQualityScore,
    IReadOnlyList<ModelQualificationResult> Results);

internal sealed record BenchmarkOptions(
    string ModelPath,
    string AdapterId,
    string OutputPath,
    uint ContextSize,
    int MaxOutputTokens,
    int ThreadCount)
{
    public const string Usage =
        "Usage: --model <local.gguf> --adapter <adapter-id> --output <results.json> " +
        "[--context 4096] [--max-output 768] [--threads 1-8]";

    public static BenchmarkOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Benchmark arguments must be provided as named value pairs.");
            }

            values[args[index]] = args[index + 1];
        }

        var modelPath = GetRequired(values, "--model");
        var adapterId = GetRequired(values, "--adapter");
        var outputPath = GetRequired(values, "--output");
        var contextSize = ParseNumber(values, "--context", 4096U, 256U, 1_048_576U);
        var maxOutputTokens = ParseNumber(values, "--max-output", 768, 32, 32_768);
        var threadCount = ParseNumber(
            values,
            "--threads",
            Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
            1,
            64);
        return new BenchmarkOptions(
            modelPath,
            adapterId,
            outputPath,
            contextSize,
            maxOutputTokens,
            threadCount);
    }

    private static string GetRequired(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ArgumentException($"Missing required argument {name}.");
    }

    private static T ParseNumber<T>(
        Dictionary<string, string> values,
        string name,
        T defaultValue,
        T minimum,
        T maximum)
        where T : struct, IParsable<T>, IComparable<T>
    {
        if (!values.TryGetValue(name, out var value))
        {
            return defaultValue;
        }

        if (!T.TryParse(value, null, out var parsed) ||
            parsed.CompareTo(minimum) < 0 ||
            parsed.CompareTo(maximum) > 0)
        {
            throw new ArgumentException($"Argument {name} must be between {minimum} and {maximum}.");
        }

        return parsed;
    }
}
