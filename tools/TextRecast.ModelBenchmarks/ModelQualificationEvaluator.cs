using System.Text.RegularExpressions;

namespace TextRecast.ModelBenchmarks;

public sealed record ModelQualificationResult(
    int Iteration,
    string CaseId,
    string Category,
    ModelQualificationSplit Split,
    string Language,
    string Operation,
    string? Tone,
    string SourceText,
    IReadOnlyList<string> RiskTags,
    IReadOnlyList<string> SemanticRequirements,
    ModelQualificationLengthIntent LengthIntent,
    string Output,
    double DurationMilliseconds,
    double FirstTokenMilliseconds,
    int OutputTokens,
    double GenerationTokensPerSecond,
    double EndToEndTokensPerSecond,
    int InputWords,
    int OutputWords,
    bool OutputPresent,
    bool ProtocolSafe,
    bool RepetitionSafe,
    bool LanguagePreserved,
    int RequiredTermsMatched,
    int RequiredTermsTotal,
    bool ForbiddenTermsAbsent,
    bool LengthIntentSatisfied,
    double QualityScore,
    string? Error);

public static partial class ModelQualificationEvaluator
{
    public const string Version = "semantic-v2-2026-08-01";

    private static readonly string[] ProtocolMarkers =
    [
        "<|im_start|>",
        "<|im_end|>",
        "<|assistant|>",
        "<|system|>",
        "<|user|>",
        "<|start_of_role|>",
        "<|end_of_role|>",
        "[SYSTEM_PROMPT]",
        "[INST]",
        "<think>",
        "</think>",
        "Here's your revised version:",
        "Here’s your revised version:",
        "revised version:",
        "improved version:",
        "(Note:",
        "Source text:",
        "Task:"
    ];

    public static ModelQualificationResult Evaluate(
        ModelQualificationCase testCase,
        string output,
        TimeSpan duration,
        TimeSpan? firstTokenLatency = null,
        int outputTokens = 0,
        int iteration = 1)
    {
        var normalizedOutput = output.Trim();
        var outputPresent = normalizedOutput.Length > 0;
        var inputWords = WordRegex().Count(testCase.Request.Text);
        var outputWords = WordRegex().Count(normalizedOutput);
        var protocolSafe = ProtocolMarkers.All(
            marker => !normalizedOutput.Contains(marker, StringComparison.OrdinalIgnoreCase));
        var repetitionSafe = !HasRepeatedPhrase(normalizedOutput);
        var languagePreserved = testCase.Expectation.LanguageMarkers.Count == 0 ||
            testCase.Expectation.LanguageMarkers.Any(
                marker => normalizedOutput.Contains(marker, StringComparison.OrdinalIgnoreCase));
        var requiredTermsMatched = testCase.Expectation.RequiredTerms.Count(
            term => normalizedOutput.Contains(term, StringComparison.OrdinalIgnoreCase));
        var forbiddenTermsAbsent = testCase.Expectation.ForbiddenTerms.All(
            term => !normalizedOutput.Contains(term, StringComparison.OrdinalIgnoreCase));
        var lengthIntentSatisfied = outputPresent && SatisfiesLengthIntent(
            testCase.Expectation.LengthIntent,
            inputWords,
            outputWords);

        var requiredRatio = testCase.Expectation.RequiredTerms.Count == 0
            ? 1D
            : requiredTermsMatched / (double)testCase.Expectation.RequiredTerms.Count;
        var qualityScore =
            (outputPresent ? 2D : 0D) +
            (protocolSafe ? 2D : 0D) +
            (repetitionSafe ? 1D : 0D) +
            (languagePreserved ? 1D : 0D) +
            (requiredRatio * 2D) +
            (forbiddenTermsAbsent ? 1D : 0D) +
            (lengthIntentSatisfied ? 1D : 0D);
        var firstToken = firstTokenLatency ?? TimeSpan.Zero;
        var generationSeconds = Math.Max(
            0,
            (duration - firstToken).TotalSeconds);
        var generatedAfterFirstToken = Math.Max(0, outputTokens - 1);
        var generationTokensPerSecond = generationSeconds > 0
            ? generatedAfterFirstToken / generationSeconds
            : 0;
        var endToEndTokensPerSecond = duration.TotalSeconds > 0
            ? outputTokens / duration.TotalSeconds
            : 0;

        return new ModelQualificationResult(
            iteration,
            testCase.Id,
            testCase.Category,
            testCase.Split,
            testCase.Language,
            testCase.Request.Operation.ToString(),
            testCase.Request.Tone?.ToString(),
            testCase.Request.Text,
            testCase.RiskTags,
            testCase.Expectation.SemanticRequirements,
            testCase.Expectation.LengthIntent,
            normalizedOutput,
            Math.Round(duration.TotalMilliseconds, 2),
            Math.Round(firstToken.TotalMilliseconds, 2),
            outputTokens,
            Math.Round(generationTokensPerSecond, 2),
            Math.Round(endToEndTokensPerSecond, 2),
            inputWords,
            outputWords,
            outputPresent,
            protocolSafe,
            repetitionSafe,
            languagePreserved,
            requiredTermsMatched,
            testCase.Expectation.RequiredTerms.Count,
            forbiddenTermsAbsent,
            lengthIntentSatisfied,
            Math.Round(qualityScore, 2),
            null);
    }

    public static ModelQualificationResult Failure(
        ModelQualificationCase testCase,
        Exception exception,
        TimeSpan duration,
        int iteration = 1)
    {
        return new ModelQualificationResult(
            iteration,
            testCase.Id,
            testCase.Category,
            testCase.Split,
            testCase.Language,
            testCase.Request.Operation.ToString(),
            testCase.Request.Tone?.ToString(),
            testCase.Request.Text,
            testCase.RiskTags,
            testCase.Expectation.SemanticRequirements,
            testCase.Expectation.LengthIntent,
            string.Empty,
            Math.Round(duration.TotalMilliseconds, 2),
            0,
            0,
            0,
            0,
            WordRegex().Count(testCase.Request.Text),
            0,
            false,
            true,
            true,
            false,
            0,
            testCase.Expectation.RequiredTerms.Count,
            true,
            false,
            0,
            exception.Message);
    }

    private static bool SatisfiesLengthIntent(
        ModelQualificationLengthIntent intent,
        int inputWords,
        int outputWords)
    {
        return intent switch
        {
            ModelQualificationLengthIntent.Unconstrained => true,
            ModelQualificationLengthIntent.MoreConcise => outputWords < inputWords,
            ModelQualificationLengthIntent.MoreExplicit => outputWords > inputWords,
            ModelQualificationLengthIntent.Summarized => outputWords < inputWords,
            _ => throw new ArgumentOutOfRangeException(nameof(intent))
        };
    }

    private static bool HasRepeatedPhrase(string output)
    {
        var words = WordRegex()
            .Matches(output)
            .Select(match => match.Value.ToUpperInvariant())
            .ToArray();
        for (var index = 0; index + 5 < words.Length; index++)
        {
            if (words[index] == words[index + 3] &&
                words[index + 1] == words[index + 4] &&
                words[index + 2] == words[index + 5])
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:['’.-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
