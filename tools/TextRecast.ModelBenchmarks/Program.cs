using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.ModelBenchmarks;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
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
            Console.WriteLine($"Automated quality: {run.Summary.AverageQualityScore:F2}/10");
            Console.WriteLine($"Minimum case quality: {run.Summary.MinimumCaseAverageQualityScore:F2}/10");
            Console.WriteLine($"Median latency: {run.Summary.MedianLatencyMilliseconds:F0} ms");
            Console.WriteLine($"Median first token: {run.Summary.MedianFirstTokenMilliseconds:F0} ms");
            Console.WriteLine($"Generation throughput: {run.Summary.GenerationTokensPerSecond:F2} tokens/s");
            Console.WriteLine($"Automated gate: {(run.Summary.AutomatedGatePassed ? "PASS" : "FAIL")}");
            Console.WriteLine($"Corpus: {run.Corpus.Scope} ({run.Corpus.CaseCount} cases)");
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
        if (fileInfo.Length != options.ExpectedFileSize)
        {
            throw new InvalidDataException(
                $"Expected {options.ExpectedFileSize} bytes but found {fileInfo.Length} bytes.");
        }

        var profile = new SlmModelProfile
        {
            Id = options.ModelId,
            AdapterId = options.AdapterId,
            FileName = fileInfo.Name,
            DownloadUri = new Uri("https://localhost/model-benchmark"),
            ExpectedSha256 = options.ExpectedSha256,
            ExpectedFileSize = options.ExpectedFileSize,
            ContextSize = options.ContextSize,
            MaxOutputTokens = options.MaxOutputTokens
        };
        var adapter = QualificationModelAdapters.Resolve(options.AdapterId);
        var hardware = new HardwareInspector().Inspect(fileInfo.DirectoryName!);
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var baselineWorkingSet = process.WorkingSet64;
        var startedAt = DateTimeOffset.UtcNow;

        using var formatter = new LocalSlmTextFormatter(
            new SlmModelOptions
            {
                Profile = profile,
                ModelPath = modelPath,
                ThreadCount = options.ThreadCount
            },
            adapter);

        var loadStopwatch = Stopwatch.StartNew();
        await formatter.LoadModelAsync(CancellationToken.None);
        loadStopwatch.Stop();
        var actualSha256 = await ComputeSha256Async(modelPath);
        if (!actualSha256.Equals(options.ExpectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The independently calculated model SHA-256 did not match.");
        }

        var corpus = ModelQualificationCorpus.GetCases(options.CorpusScope);
        var warmup = await formatter.FormatMeasuredAsync(
            corpus[0].Request,
            CancellationToken.None);

        var results = new List<ModelQualificationResult>(
            corpus.Count * options.Iterations);
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            foreach (var testCase in corpus)
            {
                var failureStopwatch = Stopwatch.StartNew();
                try
                {
                    var measurement = await formatter.FormatMeasuredAsync(
                        testCase.Request,
                        CancellationToken.None);
                    failureStopwatch.Stop();
                    results.Add(ModelQualificationEvaluator.Evaluate(
                        testCase,
                        measurement.Output,
                        measurement.TotalLatency,
                        measurement.FirstTokenLatency,
                        measurement.OutputTokens,
                        iteration));
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failureStopwatch.Stop();
                    results.Add(ModelQualificationEvaluator.Failure(
                        testCase,
                        exception,
                        failureStopwatch.Elapsed,
                        iteration));
                }
            }
        }

        process.Refresh();
        var finishedAt = DateTimeOffset.UtcNow;
        return new ModelBenchmarkRun(
            startedAt,
            finishedAt,
            new ModelSourceEvidence(
                options.SourceRepository,
                options.SourceRevision,
                options.SourceLicense,
                options.Quantization,
                fileInfo.Name,
                actualSha256,
                fileInfo.Length),
            new BenchmarkCorpusEvidence(
                ModelQualificationCorpus.Version,
                ModelQualificationCorpus.EnglishFingerprint,
                ModelQualificationEvaluator.Version,
                options.CorpusScope,
                corpus.Count),
            new BenchmarkEnvironment(
                Environment.OSVersion.ToString(),
                RuntimeInformation.FrameworkDescription,
                Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unavailable",
                hardware.TotalPhysicalMemoryBytes,
                hardware.AvailablePhysicalMemoryBytes,
                hardware.LogicalProcessorCount,
                hardware.ProcessArchitecture.ToString(),
                hardware.SupportsAvx2,
                hardware.AvailableModelStorageBytes,
                options.ThreadCount,
                options.ContextSize,
                options.MaxOutputTokens),
            profile.Id,
            options.AdapterId,
            options.Iterations,
            Math.Round(loadStopwatch.Elapsed.TotalMilliseconds, 2),
            new WarmupMeasurement(
                Math.Round(warmup.FirstTokenLatency.TotalMilliseconds, 2),
                Math.Round(warmup.TotalLatency.TotalMilliseconds, 2),
                warmup.OutputTokens),
            baselineWorkingSet,
            process.PeakWorkingSet64,
            BenchmarkSummary.Create(results),
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
    DateTimeOffset FinishedAtUtc,
    ModelSourceEvidence Source,
    BenchmarkCorpusEvidence Corpus,
    BenchmarkEnvironment Environment,
    string ModelId,
    string AdapterId,
    int Iterations,
    double ColdLoadMilliseconds,
    WarmupMeasurement Warmup,
    long BaselineWorkingSetBytes,
    long PeakWorkingSetBytes,
    BenchmarkSummary Summary,
    IReadOnlyList<ModelQualificationResult> Results);

public sealed record ModelSourceEvidence(
    string Repository,
    string Revision,
    string License,
    string Quantization,
    string FileName,
    string Sha256,
    long FileSizeBytes);

public sealed record BenchmarkCorpusEvidence(
    string Version,
    string Fingerprint,
    string EvaluatorVersion,
    ModelQualificationCorpusScope Scope,
    int CaseCount);

public sealed record BenchmarkEnvironment(
    string OperatingSystem,
    string DotNetRuntime,
    string Processor,
    long TotalPhysicalMemoryBytes,
    long AvailablePhysicalMemoryAtStartBytes,
    int LogicalProcessors,
    string ProcessArchitecture,
    bool SupportsAvx2,
    long AvailableModelStorageBytes,
    int Threads,
    uint ContextSize,
    int MaxOutputTokens);

public sealed record WarmupMeasurement(
    double FirstTokenMilliseconds,
    double TotalLatencyMilliseconds,
    int OutputTokens);

public sealed record BenchmarkDimensionSummary(
    string Name,
    int Samples,
    double AverageQualityScore,
    double MinimumQualityScore,
    double MedianLatencyMilliseconds,
    double MedianFirstTokenMilliseconds);

public sealed record BenchmarkSummary(
    double AverageQualityScore,
    double MinimumCaseAverageQualityScore,
    bool AutomatedGatePassed,
    double? HoldoutAverageQualityScore,
    bool? HoldoutAutomatedGatePassed,
    int FailureCount,
    int ProtocolFailureCount,
    int RepetitionFailureCount,
    int LanguageFailureCount,
    int OperationIntentFailureCount,
    double MedianLatencyMilliseconds,
    double P95LatencyMilliseconds,
    double MedianFirstTokenMilliseconds,
    double P95FirstTokenMilliseconds,
    double GenerationTokensPerSecond,
    double EndToEndTokensPerSecond,
    IReadOnlyList<BenchmarkDimensionSummary> Cases,
    IReadOnlyList<BenchmarkDimensionSummary> Operations,
    IReadOnlyList<BenchmarkDimensionSummary> Categories,
    IReadOnlyList<BenchmarkDimensionSummary> Splits,
    IReadOnlyList<BenchmarkDimensionSummary> HoldoutCategories,
    IReadOnlyList<BenchmarkDimensionSummary> Languages)
{
    public static BenchmarkSummary Create(IReadOnlyList<ModelQualificationResult> results)
    {
        var successful = results.Where(result => result.Error is null).ToArray();
        var caseSummaries = Summarize(results, result => result.CaseId);
        var holdout = results
            .Where(result => result.Split == ModelQualificationSplit.Holdout)
            .ToArray();
        var holdoutCategories = Summarize(holdout, result => result.Category);
        var generationSeconds = successful.Sum(
            result => Math.Max(0, result.DurationMilliseconds - result.FirstTokenMilliseconds)) / 1000D;
        var generatedAfterFirstToken = successful.Sum(result => Math.Max(0, result.OutputTokens - 1));
        var totalSeconds = successful.Sum(result => result.DurationMilliseconds) / 1000D;
        var totalTokens = successful.Sum(result => result.OutputTokens);
        var automatedGatePassed = PassesAutomatedGate(results, caseSummaries);
        bool? holdoutAutomatedGatePassed = holdout.Length == 0
            ? null
            : holdoutCategories.Length == ModelQualificationTaskGroups.All.Count &&
              holdoutCategories.All(summary => summary.AverageQualityScore >= 8) &&
              PassesRequiredConstraints(holdout);

        return new BenchmarkSummary(
            Round(results.Count == 0 ? 0 : results.Average(result => result.QualityScore)),
            Round(caseSummaries.Length == 0 ? 0 : caseSummaries.Min(summary => summary.AverageQualityScore)),
            automatedGatePassed,
            holdout.Length == 0 ? null : Round(holdout.Average(result => result.QualityScore)),
            holdoutAutomatedGatePassed,
            results.Count(result => result.Error is not null),
            results.Count(result => !result.ProtocolSafe),
            results.Count(result => !result.RepetitionSafe),
            results.Count(result => !result.LanguagePreserved),
            results.Count(result => !result.LengthIntentSatisfied),
            Percentile(successful.Select(result => result.DurationMilliseconds), 0.50),
            Percentile(successful.Select(result => result.DurationMilliseconds), 0.95),
            Percentile(successful.Select(result => result.FirstTokenMilliseconds), 0.50),
            Percentile(successful.Select(result => result.FirstTokenMilliseconds), 0.95),
            Round(generationSeconds > 0 ? generatedAfterFirstToken / generationSeconds : 0),
            Round(totalSeconds > 0 ? totalTokens / totalSeconds : 0),
            caseSummaries,
            Summarize(results, result => result.Operation),
            Summarize(results, result => result.Category),
            Summarize(results, result => result.Split.ToString()),
            holdoutCategories,
            Summarize(results, result => result.Language));
    }

    private static bool PassesAutomatedGate(
        IReadOnlyList<ModelQualificationResult> results,
        BenchmarkDimensionSummary[] caseSummaries)
    {
        return caseSummaries.Length > 0 &&
            caseSummaries.All(summary => summary.AverageQualityScore >= 8) &&
            PassesRequiredConstraints(results);
    }

    private static bool PassesRequiredConstraints(
        IReadOnlyList<ModelQualificationResult> results)
    {
        return results.All(result =>
            result.Error is null &&
            result.OutputPresent &&
            result.ProtocolSafe &&
            result.RepetitionSafe &&
            result.LanguagePreserved &&
            result.RequiredTermsMatched == result.RequiredTermsTotal &&
            result.ForbiddenTermsAbsent &&
            result.LengthIntentSatisfied);
    }

    private static BenchmarkDimensionSummary[] Summarize(
        IEnumerable<ModelQualificationResult> results,
        Func<ModelQualificationResult, string> selector)
    {
        return results
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var items = group.ToArray();
                var successful = items.Where(item => item.Error is null).ToArray();
                return new BenchmarkDimensionSummary(
                    group.Key,
                    items.Length,
                    Round(items.Average(item => item.QualityScore)),
                    Round(items.Min(item => item.QualityScore)),
                    Percentile(successful.Select(item => item.DurationMilliseconds), 0.50),
                    Percentile(successful.Select(item => item.FirstTokenMilliseconds), 0.50));
            })
            .ToArray();
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0)
        {
            return 0;
        }

        var index = Math.Clamp((int)Math.Ceiling(percentile * ordered.Length) - 1, 0, ordered.Length - 1);
        return Round(ordered[index]);
    }

    private static double Round(double value) => Math.Round(value, 2);
}

internal sealed record BenchmarkOptions(
    string ModelPath,
    string ModelId,
    string AdapterId,
    string OutputPath,
    string SourceRepository,
    string SourceRevision,
    string SourceLicense,
    string Quantization,
    string ExpectedSha256,
    long ExpectedFileSize,
    uint ContextSize,
    int MaxOutputTokens,
    int ThreadCount,
    int Iterations,
    ModelQualificationCorpusScope CorpusScope)
{
    public const string Usage =
        "Usage: --model <local.gguf> --model-id <id> --adapter <adapter-id> " +
        "--output <results.json> --source-repo <owner/repo> --source-revision <commit> " +
        "--source-license <SPDX> --quantization <Q5_K_M> --expected-sha <sha256> " +
        "--expected-size <bytes> [--context 4096] [--max-output 768] " +
        "[--threads 1-64] [--iterations 1-10] " +
        "[--corpus-scope prompt-development|prompt-validation|final-qualification]";

    public static BenchmarkOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            if (index + 1 >= args.Count || !args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException("Benchmark arguments must be provided as named value pairs.");
            }

            if (!values.TryAdd(args[index], args[index + 1]))
            {
                throw new ArgumentException($"Argument {args[index]} was provided more than once.");
            }
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "--model", "--model-id", "--adapter", "--output", "--source-repo",
            "--source-revision", "--source-license", "--quantization", "--expected-sha",
            "--expected-size", "--context", "--max-output", "--threads", "--iterations",
            "--corpus-scope"
        };
        var unknown = values.Keys.FirstOrDefault(key => !allowed.Contains(key));
        if (unknown is not null)
        {
            throw new ArgumentException($"Unknown argument {unknown}.");
        }

        var expectedSha256 = GetRequired(values, "--expected-sha").ToLowerInvariant();
        ValidateSha256(expectedSha256);
        return new BenchmarkOptions(
            GetRequired(values, "--model"),
            GetRequired(values, "--model-id"),
            GetRequired(values, "--adapter"),
            GetRequired(values, "--output"),
            GetRequired(values, "--source-repo"),
            GetRequired(values, "--source-revision"),
            GetRequired(values, "--source-license"),
            GetRequired(values, "--quantization"),
            expectedSha256,
            ParseNumber(values, "--expected-size", 1L, long.MaxValue),
            ParseNumber(values, "--context", 4096U, 256U, 1_048_576U),
            ParseNumber(values, "--max-output", 768, 32, 32_768),
            ParseNumber(
                values,
                "--threads",
                Math.Clamp(Environment.ProcessorCount - 1, 1, 8),
                1,
                64),
            ParseNumber(values, "--iterations", 3, 1, 10),
            ParseCorpusScope(values));
    }

    private static ModelQualificationCorpusScope ParseCorpusScope(
        Dictionary<string, string> values)
    {
        if (!values.TryGetValue("--corpus-scope", out var value))
        {
            return ModelQualificationCorpusScope.PromptDevelopment;
        }

        return value switch
        {
            "prompt-development" => ModelQualificationCorpusScope.PromptDevelopment,
            "prompt-validation" => ModelQualificationCorpusScope.PromptValidation,
            "final-qualification" => ModelQualificationCorpusScope.FinalQualification,
            _ => throw new ArgumentException(
                "Argument --corpus-scope must be prompt-development, prompt-validation, or final-qualification.")
        };
    }

    private static void ValidateSha256(string value)
    {
        if (value.Length != 64)
        {
            throw new ArgumentException("Argument --expected-sha must be a 64-character SHA-256 value.");
        }

        try
        {
            _ = Convert.FromHexString(value);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "Argument --expected-sha must contain only hexadecimal characters.",
                exception);
        }
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
        T minimum,
        T maximum)
        where T : struct, IParsable<T>, IComparable<T>
    {
        return values.TryGetValue(name, out var value) &&
               T.TryParse(value, null, out var parsed) &&
               parsed.CompareTo(minimum) >= 0 &&
               parsed.CompareTo(maximum) <= 0
            ? parsed
            : throw new ArgumentException($"Argument {name} must be between {minimum} and {maximum}.");
    }

    private static T ParseNumber<T>(
        Dictionary<string, string> values,
        string name,
        T defaultValue,
        T minimum,
        T maximum)
        where T : struct, IParsable<T>, IComparable<T>
    {
        return values.ContainsKey(name)
            ? ParseNumber(values, name, minimum, maximum)
            : defaultValue;
    }
}
