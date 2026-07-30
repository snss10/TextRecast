using System.Text.RegularExpressions;

namespace TextRecast.ModelBenchmarks;

public sealed record ModelQualificationResult(
    string CaseId,
    string Category,
    string Language,
    string Output,
    double DurationMilliseconds,
    int OutputWords,
    bool OutputPresent,
    bool ProtocolSafe,
    bool RepetitionSafe,
    bool LanguagePreserved,
    int RequiredTermsMatched,
    int RequiredTermsTotal,
    bool ForbiddenTermsAbsent,
    bool LengthWithinBounds,
    double QualityScore,
    string? Error);

public static partial class ModelQualificationEvaluator
{
    private static readonly string[] ProtocolMarkers =
    [
        "<|im_start|>",
        "<|im_end|>",
        "<|assistant|>",
        "<think>",
        "</think>",
        "Source text:",
        "Task:"
    ];

    public static ModelQualificationResult Evaluate(
        ModelQualificationCase testCase,
        string output,
        TimeSpan duration)
    {
        var normalizedOutput = output.Trim();
        var outputPresent = normalizedOutput.Length > 0;
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
        var lengthWithinBounds =
            (testCase.Expectation.MinimumWords is null ||
             outputWords >= testCase.Expectation.MinimumWords.Value) &&
            (testCase.Expectation.MaximumWords is null ||
             outputWords <= testCase.Expectation.MaximumWords.Value);

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
            (lengthWithinBounds ? 1D : 0D);

        return new ModelQualificationResult(
            testCase.Id,
            testCase.Category,
            testCase.Language,
            normalizedOutput,
            duration.TotalMilliseconds,
            outputWords,
            outputPresent,
            protocolSafe,
            repetitionSafe,
            languagePreserved,
            requiredTermsMatched,
            testCase.Expectation.RequiredTerms.Count,
            forbiddenTermsAbsent,
            lengthWithinBounds,
            Math.Round(qualityScore, 2),
            null);
    }

    public static ModelQualificationResult Failure(
        ModelQualificationCase testCase,
        Exception exception,
        TimeSpan duration)
    {
        return new ModelQualificationResult(
            testCase.Id,
            testCase.Category,
            testCase.Language,
            string.Empty,
            duration.TotalMilliseconds,
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
