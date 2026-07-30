using TextRecast.Core.Formatting;
using TextRecast.ModelBenchmarks;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class ModelQualificationCorpusTests
{
    private static readonly string[] ExpectedCategories =
        ["short", "medium", "long", "multilingual", "punctuation-heavy", "adversarial"];
    private static readonly string[] ExpectedLanguages = ["en", "hi", "es", "fr", "de", "ja"];

    [TestMethod]
    public void CorpusCoversEveryOperationToneCategoryAndTargetLanguage()
    {
        var cases = ModelQualificationCorpus.All;

        CollectionAssert.AreEquivalent(
            Enum.GetValues<FormatOperation>(),
            cases.Select(testCase => testCase.Request.Operation).Distinct().ToArray());
        CollectionAssert.AreEquivalent(
            Enum.GetValues<ToneStyle>(),
            cases.Where(testCase => testCase.Request.Tone is not null)
                .Select(testCase => testCase.Request.Tone!.Value)
                .Distinct()
                .ToArray());
        CollectionAssert.IsSubsetOf(
            ExpectedCategories,
            cases.Select(testCase => testCase.Category).Distinct().ToArray());
        CollectionAssert.IsSubsetOf(
            ExpectedLanguages,
            cases.Select(testCase => testCase.Language).Distinct().ToArray());
        Assert.AreEqual(cases.Count, cases.Select(testCase => testCase.Id).Distinct().Count());
        Assert.IsTrue(cases.All(testCase => !string.IsNullOrWhiteSpace(testCase.Request.Text)));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "hi").Request.Text.Contains('क'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "es").Request.Text.Contains('í'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "fr").Request.Text.Contains('é'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "ja").Request.Text.Contains('ル'));
    }

    [TestMethod]
    public void EvaluatorScoresCleanConstrainedOutputAtTen()
    {
        var testCase = new ModelQualificationCase(
            "test",
            "short",
            "es",
            new FormatTextRequest("source", FormatOperation.Improve),
            new ModelQualificationExpectation(
                ["informe", "viernes"],
                ["forbidden"],
                ["viernes"],
                2,
                5));

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Informe listo el viernes.",
            TimeSpan.FromMilliseconds(25));

        Assert.AreEqual(10D, result.QualityScore);
        Assert.IsTrue(result.ProtocolSafe);
        Assert.IsTrue(result.RepetitionSafe);
        Assert.IsTrue(result.LanguagePreserved);
        Assert.IsTrue(result.LengthWithinBounds);
    }

    [TestMethod]
    public void EvaluatorDetectsProtocolLeakageRepetitionAndLanguageLoss()
    {
        var testCase = new ModelQualificationCase(
            "test",
            "adversarial",
            "ja",
            new FormatTextRequest("source", FormatOperation.Improve),
            new ModelQualificationExpectation(["ルーター"], [], ["ルーター"], null, null));

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "<think>one two three one two three</think>",
            TimeSpan.Zero);

        Assert.IsFalse(result.ProtocolSafe);
        Assert.IsFalse(result.RepetitionSafe);
        Assert.IsFalse(result.LanguagePreserved);
        Assert.AreEqual(0, result.RequiredTermsMatched);
        Assert.IsLessThan(8D, result.QualityScore);
    }

    [TestMethod]
    public void EvaluatorCalculatesFirstTokenAndTokenizerThroughputMetrics()
    {
        var testCase = new ModelQualificationCase(
            "test",
            "short",
            "en",
            new FormatTextRequest("source", FormatOperation.Improve),
            new ModelQualificationExpectation([], [], [], null, null));

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Clear output.",
            TimeSpan.FromSeconds(2.5),
            TimeSpan.FromSeconds(0.5),
            outputTokens: 21,
            iteration: 2);

        Assert.AreEqual(2, result.Iteration);
        Assert.AreEqual(500D, result.FirstTokenMilliseconds);
        Assert.AreEqual(21, result.OutputTokens);
        Assert.AreEqual(10D, result.GenerationTokensPerSecond);
        Assert.AreEqual(8.4D, result.EndToEndTokensPerSecond);
    }
}
