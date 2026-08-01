using System.Text.RegularExpressions;
using TextRecast.Core.Formatting;
using TextRecast.ModelBenchmarks;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class ModelQualificationCorpusTests
{
    private static readonly string[] ExpectedRiskTags =
    [
        "adversarial",
        "causality",
        "deadline",
        "fragment",
        "identifier",
        "multi-actor",
        "negation",
        "no-change",
        "numeric",
        "punctuation",
        "sequence",
        "technical"
    ];
    private static readonly string[] ExpectedDeferredLanguages = ["hi", "es", "fr", "de", "ja"];

    [TestMethod]
    public void EnglishCorpusHasNinetyCasesBalancedAcrossTasksAndSplits()
    {
        var cases = ModelQualificationCorpus.English;

        Assert.HasCount(90, cases);
        CollectionAssert.AreEquivalent(
            ModelQualificationTaskGroups.All.ToArray(),
            cases.Select(testCase => testCase.Category).Distinct().ToArray());
        Assert.HasCount(36, cases.Where(testCase => testCase.Split == ModelQualificationSplit.Development));
        Assert.HasCount(27, cases.Where(testCase => testCase.Split == ModelQualificationSplit.Validation));
        Assert.HasCount(27, cases.Where(testCase => testCase.Split == ModelQualificationSplit.Holdout));

        foreach (var group in cases.GroupBy(testCase => testCase.Category, StringComparer.Ordinal))
        {
            Assert.HasCount(10, group);
            Assert.HasCount(4, group.Where(testCase => testCase.Split == ModelQualificationSplit.Development));
            Assert.HasCount(3, group.Where(testCase => testCase.Split == ModelQualificationSplit.Validation));
            Assert.HasCount(3, group.Where(testCase => testCase.Split == ModelQualificationSplit.Holdout));
        }
    }

    [TestMethod]
    public void EnglishCorpusCoversOperationsTonesAndSemanticRisks()
    {
        var cases = ModelQualificationCorpus.English;

        Assert.IsTrue(cases.All(testCase => testCase.Language == "en"));
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
            ExpectedRiskTags,
            cases.SelectMany(testCase => testCase.RiskTags).Distinct().ToArray());
        Assert.AreEqual(cases.Count, cases.Select(testCase => testCase.Id).Distinct().Count());
        Assert.IsTrue(cases.All(testCase => !string.IsNullOrWhiteSpace(testCase.Request.Text)));
        Assert.IsTrue(cases.All(testCase => testCase.RiskTags.Count > 0));
        Assert.IsTrue(cases.All(testCase => testCase.Expectation.RequiredTerms.Count > 0));
        Assert.IsTrue(cases.All(testCase => testCase.Expectation.SemanticRequirements.Count > 0));
        Assert.IsTrue(cases.All(testCase => testCase.Expectation.LanguageMarkers.Count == 0));
    }

    [TestMethod]
    public void EveryCaseUsesTheTaskGroupMatchingItsOperationAndTone()
    {
        foreach (var testCase in ModelQualificationCorpus.English)
        {
            var expected = testCase.Request.Operation switch
            {
                FormatOperation.Improve => ModelQualificationTaskGroups.Improve,
                FormatOperation.Shorten => ModelQualificationTaskGroups.Shorten,
                FormatOperation.Lengthen => ModelQualificationTaskGroups.Lengthen,
                FormatOperation.Summarize => ModelQualificationTaskGroups.Summarize,
                FormatOperation.ChangeTone => testCase.Request.Tone switch
                {
                    ToneStyle.Professional => ModelQualificationTaskGroups.ToneProfessional,
                    ToneStyle.Casual => ModelQualificationTaskGroups.ToneCasual,
                    ToneStyle.Friendly => ModelQualificationTaskGroups.ToneFriendly,
                    ToneStyle.Formal => ModelQualificationTaskGroups.ToneFormal,
                    ToneStyle.Direct => ModelQualificationTaskGroups.ToneDirect,
                    _ => throw new AssertFailedException($"Missing tone for {testCase.Id}.")
                },
                _ => throw new AssertFailedException($"Unknown operation for {testCase.Id}.")
            };

            Assert.AreEqual(expected, testCase.Category, testCase.Id);
        }
    }

    [TestMethod]
    public void CorpusUsesSemanticLengthIntentInsteadOfFixedOutputShapes()
    {
        foreach (var testCase in ModelQualificationCorpus.English)
        {
            var expected = testCase.Request.Operation switch
            {
                FormatOperation.Shorten => ModelQualificationLengthIntent.MoreConcise,
                FormatOperation.Lengthen => ModelQualificationLengthIntent.MoreExplicit,
                FormatOperation.Summarize => ModelQualificationLengthIntent.Summarized,
                _ => ModelQualificationLengthIntent.Unconstrained
            };

            Assert.AreEqual(expected, testCase.Expectation.LengthIntent, testCase.Id);
        }
    }

    [TestMethod]
    public void LengthenCorpusIncludesFragmentsAndContextualPassages()
    {
        var cases = ModelQualificationCorpus.English
            .Where(testCase => testCase.Category == ModelQualificationTaskGroups.Lengthen)
            .ToArray();

        Assert.IsTrue(cases.Any(testCase => CountWords(testCase.Request.Text) <= 5));
        Assert.IsTrue(cases.Any(testCase => CountWords(testCase.Request.Text) >= 20));
        Assert.IsTrue(cases.Any(testCase => testCase.Request.Text.Count(character => character == '.') >= 2));
        Assert.IsTrue(cases.Any(testCase => testCase.RiskTags.Contains("multi-actor")));
        Assert.IsTrue(cases.Any(testCase => testCase.RiskTags.Contains("negation")));
        Assert.IsTrue(cases.Any(testCase => testCase.RiskTags.Contains("sequence")));
    }

    [TestMethod]
    public void PromptScopesCannotIncludeHoldoutCases()
    {
        var development = ModelQualificationCorpus.GetCases(
            ModelQualificationCorpusScope.PromptDevelopment);
        var validation = ModelQualificationCorpus.GetCases(
            ModelQualificationCorpusScope.PromptValidation);
        var finalQualification = ModelQualificationCorpus.GetCases(
            ModelQualificationCorpusScope.FinalQualification);

        Assert.HasCount(36, development);
        Assert.HasCount(27, validation);
        Assert.HasCount(90, finalQualification);
        Assert.IsTrue(development.All(testCase => testCase.Split == ModelQualificationSplit.Development));
        Assert.IsTrue(validation.All(testCase => testCase.Split == ModelQualificationSplit.Validation));
        Assert.IsFalse(development.Any(testCase => testCase.Split == ModelQualificationSplit.Holdout));
        Assert.IsFalse(validation.Any(testCase => testCase.Split == ModelQualificationSplit.Holdout));
        Assert.IsEmpty(development.Select(testCase => testCase.Id)
            .Intersect(validation.Select(testCase => testCase.Id), StringComparer.Ordinal));
    }

    [TestMethod]
    public void BenchmarkCommandDefaultsToPromptDevelopmentAndRejectsDirectHoldoutScope()
    {
        var options = BenchmarkOptions.Parse(CreateBenchmarkArguments());
        var invalid = CreateBenchmarkArguments()
            .Concat(["--corpus-scope", "holdout"])
            .ToArray();

        Assert.AreEqual(ModelQualificationCorpusScope.PromptDevelopment, options.CorpusScope);
        Assert.ThrowsExactly<ArgumentException>(() => BenchmarkOptions.Parse(invalid));
    }

    [TestMethod]
    public void BenchmarkCommandRequiresExplicitFinalQualificationScope()
    {
        var options = BenchmarkOptions.Parse(
            CreateBenchmarkArguments()
                .Concat(["--corpus-scope", "final-qualification"])
                .ToArray());

        Assert.AreEqual(ModelQualificationCorpusScope.FinalQualification, options.CorpusScope);
        Assert.IsTrue(ModelQualificationCorpus.GetCases(options.CorpusScope)
            .Any(testCase => testCase.Split == ModelQualificationSplit.Holdout));
    }

    [TestMethod]
    public void CorpusCarriesValidSha256Fingerprint()
    {
        Assert.IsTrue(Regex.IsMatch(
            ModelQualificationCorpus.EnglishFingerprint,
            "^[a-f0-9]{64}$",
            RegexOptions.CultureInvariant));
    }

    [TestMethod]
    public void DeferredMultilingualCorpusRemainsAvailableButOutsideEnglishQualification()
    {
        var cases = ModelQualificationCorpus.DeferredMultilingual;

        Assert.HasCount(5, cases);
        CollectionAssert.AreEquivalent(
            ExpectedDeferredLanguages,
            cases.Select(testCase => testCase.Language).ToArray());
        Assert.IsTrue(cases.All(testCase => testCase.Expectation.LanguageMarkers.Count > 0));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "hi").Request.Text.Contains('क'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "es").Request.Text.Contains('í'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "fr").Request.Text.Contains('é'));
        Assert.IsTrue(cases.Single(testCase => testCase.Language == "ja").Request.Text.Contains('ル'));
    }

    [TestMethod]
    public void EvaluatorScoresCleanIntentCompliantOutputAtTen()
    {
        var testCase = CreateCase(
            "Please send the completed report to Finance today.",
            FormatOperation.Shorten,
            ModelQualificationLengthIntent.MoreConcise,
            required: ["report", "Finance", "today"]);

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Send the report to Finance today.",
            TimeSpan.FromMilliseconds(25));

        Assert.AreEqual(10D, result.QualityScore);
        Assert.IsTrue(result.ProtocolSafe);
        Assert.IsTrue(result.RepetitionSafe);
        Assert.IsTrue(result.LanguagePreserved);
        Assert.IsTrue(result.LengthIntentSatisfied);
        Assert.IsGreaterThan(0, result.OutputWords);
        Assert.IsGreaterThan(result.OutputWords, result.InputWords);
    }

    [TestMethod]
    [DataRow(FormatOperation.Shorten, ModelQualificationLengthIntent.MoreConcise,
        "Please send the complete report today because the legal team is waiting.", "Send report today.", true)]
    [DataRow(FormatOperation.Shorten, ModelQualificationLengthIntent.MoreConcise,
        "Send report today.", "Send report today.", false)]
    [DataRow(FormatOperation.Lengthen, ModelQualificationLengthIntent.MoreExplicit,
        "Send report.", "Please send the complete report today.", true)]
    [DataRow(FormatOperation.Lengthen, ModelQualificationLengthIntent.MoreExplicit,
        "Send report.", "Send report.", false)]
    [DataRow(FormatOperation.Summarize, ModelQualificationLengthIntent.Summarized,
        "The report was delayed today because the review is incomplete.", "Report delayed today.", true)]
    public void EvaluatorMeasuresRelativeOperationIntent(
        FormatOperation operation,
        ModelQualificationLengthIntent intent,
        string source,
        string output,
        bool expected)
    {
        var testCase = CreateCase(
            source,
            operation,
            intent);

        var result = ModelQualificationEvaluator.Evaluate(testCase, output, TimeSpan.Zero);

        Assert.AreEqual(expected, result.LengthIntentSatisfied);
    }

    [TestMethod]
    public void EvaluatorIncludesContextNeededForHumanSemanticReview()
    {
        var testCase = CreateCase(
            "Omar has not approved the contract.",
            FormatOperation.Improve,
            ModelQualificationLengthIntent.Unconstrained,
            semanticRequirements: ["Omar has not approved the contract."],
            riskTags: ["multi-actor", "negation"]);

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Omar has not approved the contract.",
            TimeSpan.Zero);

        Assert.AreEqual(testCase.Request.Text, result.SourceText);
        CollectionAssert.AreEqual(testCase.RiskTags.ToArray(), result.RiskTags.ToArray());
        CollectionAssert.AreEqual(
            testCase.Expectation.SemanticRequirements.ToArray(),
            result.SemanticRequirements.ToArray());
        Assert.AreEqual(ModelQualificationSplit.Development, result.Split);
    }

    [TestMethod]
    public void EvaluatorDetectsProtocolLeakageRepetitionAndLanguageLoss()
    {
        var testCase = new ModelQualificationCase(
            "test",
            "multilingual",
            ModelQualificationSplit.Development,
            "ja",
            new FormatTextRequest("source", FormatOperation.Improve),
            ["language-preservation"],
            new ModelQualificationExpectation(
                ["ルーター"],
                [],
                ["ルーター"],
                ["Preserve the router reference."],
                ModelQualificationLengthIntent.Unconstrained));

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
        var testCase = CreateCase(
            "source",
            FormatOperation.Improve,
            ModelQualificationLengthIntent.Unconstrained);

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

    [TestMethod]
    public void AutomatedGateRejectsHighScoreThatMissesARequiredConstraint()
    {
        var testCase = CreateCase(
            "Send revised quote by noon.",
            FormatOperation.Lengthen,
            ModelQualificationLengthIntent.MoreExplicit,
            required: ["quote", "noon"]);
        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Please send the revised quote by the end of the working day.",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(250),
            outputTokens: 12);

        Assert.AreEqual(9D, result.QualityScore);
        Assert.IsFalse(BenchmarkSummary.Create([result]).AutomatedGatePassed);
    }

    [TestMethod]
    public void SummarySeparatesHoldoutEvidenceFromPromptDevelopmentResults()
    {
        var developmentCase = CreateCase(
            "Development source.",
            FormatOperation.Improve,
            ModelQualificationLengthIntent.Unconstrained);
        var holdoutCase = CreateCase(
            "Holdout source.",
            FormatOperation.Improve,
            ModelQualificationLengthIntent.Unconstrained,
            split: ModelQualificationSplit.Holdout,
            category: ModelQualificationTaskGroups.Improve);
        var developmentResult = ModelQualificationEvaluator.Evaluate(
            developmentCase,
            "Development output.",
            TimeSpan.Zero);
        var holdoutResult = ModelQualificationEvaluator.Evaluate(
            holdoutCase,
            "Holdout output.",
            TimeSpan.Zero);

        var summary = BenchmarkSummary.Create([developmentResult, holdoutResult]);

        Assert.AreEqual(10D, summary.HoldoutAverageQualityScore);
        Assert.IsFalse(summary.HoldoutAutomatedGatePassed);
        Assert.HasCount(1, summary.HoldoutCategories);
        Assert.AreEqual(ModelQualificationTaskGroups.Improve, summary.HoldoutCategories[0].Name);
    }

    [TestMethod]
    public void EvaluatorRejectsCommonResponseLabelsAsProtocolLeakage()
    {
        var testCase = CreateCase(
            "source",
            FormatOperation.ChangeTone,
            ModelQualificationLengthIntent.Unconstrained,
            tone: ToneStyle.Friendly);

        var result = ModelQualificationEvaluator.Evaluate(
            testCase,
            "Here is the revised version: Friendly text.",
            TimeSpan.FromSeconds(1));

        Assert.IsFalse(result.ProtocolSafe);
    }

    private static ModelQualificationCase CreateCase(
        string source,
        FormatOperation operation,
        ModelQualificationLengthIntent intent,
        IReadOnlyList<string>? required = null,
        IReadOnlyList<string>? semanticRequirements = null,
        IReadOnlyList<string>? riskTags = null,
        ToneStyle? tone = null,
        ModelQualificationSplit split = ModelQualificationSplit.Development,
        string category = "test")
    {
        return new ModelQualificationCase(
            "test",
            category,
            split,
            "en",
            new FormatTextRequest(source, operation, tone),
            riskTags ?? ["test"],
            new ModelQualificationExpectation(
                required ?? [],
                [],
                [],
                semanticRequirements ?? ["Preserve the source meaning."],
                intent));
    }

    private static string[] CreateBenchmarkArguments()
    {
        return
        [
            "--model", "model.gguf",
            "--model-id", "test-model",
            "--adapter", "test-adapter",
            "--output", "result.json",
            "--source-repo", "owner/repository",
            "--source-revision", "0123456789abcdef",
            "--source-license", "Apache-2.0",
            "--quantization", "Q5_K_M",
            "--expected-sha", new string('0', 64),
            "--expected-size", "1"
        ];
    }

    private static int CountWords(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
