using System.Text.RegularExpressions;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;
using TextRecast.ModelBenchmarks;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class QualificationModelAdapterTests
{
    private static readonly FormatTextRequest Request = new(
        "Send the report before Friday.",
        FormatOperation.Improve);
    private static readonly (string RuntimeModelId, string AdapterId)[] RuntimeModels =
    [
        ("Qwen2.5-1.5B-Instruct-Q5_K_M", Qwen25ModelAdapter.AdapterId),
        ("Qwen3.5-2B-Q5_K_M", Qwen35QualificationAdapter.AdapterId),
        ("Qwen3.5-4B-Q5_K_M", Qwen35QualificationAdapter.AdapterId),
        ("Phi-4-Mini-Instruct-Q5_K_M", Phi4MiniQualificationAdapter.AdapterId),
        ("Ministral-3-3B-Instruct-2512-Q5_K_M", Ministral3QualificationAdapter.AdapterId),
        ("Granite-4.1-3B-Q5_K_M", Granite41QualificationAdapter.AdapterId)
    ];

    [TestMethod]
    public void CatalogProvidesVersionedProfilesForEveryExactModel()
    {
        Assert.HasCount(6, QualificationPromptCatalog.Models);
        Assert.AreEqual(
            QualificationPromptCatalog.Models.Count,
            QualificationPromptCatalog.Models.Select(model => model.Id).Distinct().Count());

        var profileIds = new List<string>();
        foreach (var model in QualificationPromptCatalog.Models)
        {
            var expectedProfileCount = model.Id switch
            {
                "ministral-3-3b-instruct-2512" => 5,
                "qwen3.5-2b" or "qwen3.5-4b" => 7,
                _ => 6
            };
            Assert.HasCount(expectedProfileCount, model.PromptProfiles);
            Assert.HasCount(1, model.PromptProfiles.Where(profile => profile.IsBaseline));
            Assert.IsTrue(model.PromptProfiles.All(profile => profile.CandidateModelId == model.Id));
            Assert.IsTrue(model.PromptProfiles.All(profile => profile.AdapterId == model.AdapterId));
            Assert.HasCount(4, model.PromptProfiles.Where(profile => profile.Version == "1"));
            Assert.HasCount(1, model.PromptProfiles.Where(profile => profile.Version == "2"));
            Assert.HasCount(1, model.PromptProfiles.Where(
                profile => profile.Id.EndsWith("-tuned-v2", StringComparison.Ordinal)));
            Assert.HasCount(
                model.Id == "ministral-3-3b-instruct-2512" ? 0 : 1,
                model.PromptProfiles.Where(
                    profile => profile.Id.EndsWith("-balanced-v3", StringComparison.Ordinal)));
            Assert.HasCount(
                model.Id is "qwen3.5-2b" or "qwen3.5-4b" ? 1 : 0,
                model.PromptProfiles.Where(
                    profile => profile.Id.EndsWith("-final-v4", StringComparison.Ordinal)));
            Assert.IsTrue(model.PromptProfiles.All(profile => Regex.IsMatch(
                profile.Fingerprint,
                "^[a-f0-9]{64}$",
                RegexOptions.CultureInvariant)));
            profileIds.AddRange(model.PromptProfiles.Select(profile => profile.Id));
        }

        Assert.AreEqual(profileIds.Count, profileIds.Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void TunedProfilesAreDistinctAndModelBound()
    {
        var tunedProfiles = QualificationPromptCatalog.Models
            .Select(model => model.PromptProfiles.Single(profile => profile.Version == "2"))
            .ToArray();

        Assert.AreEqual(
            tunedProfiles.Length,
            tunedProfiles.Select(profile => profile.SystemInstruction).Distinct().Count());
        Assert.AreEqual(
            tunedProfiles.Length,
            tunedProfiles.Select(profile => profile.TaskWording).Distinct().Count());

        foreach (var model in QualificationPromptCatalog.Models)
        {
            var baseline = model.PromptProfiles.Single(profile => profile.IsBaseline);
            var tuned = model.PromptProfiles.Single(profile => profile.Version == "2");

            Assert.AreNotEqual(baseline.SystemInstruction, tuned.SystemInstruction, model.Id);
            Assert.AreNotEqual(baseline.TaskWording, tuned.TaskWording, model.Id);
            Assert.AreEqual(QualificationSourceLayout.Labeled, tuned.SourceLayout, model.Id);
        }
    }

    [TestMethod]
    public void BalancedProfilesAreDistinctAndExcludeRejectedMinistralCandidate()
    {
        var balancedProfiles = QualificationPromptCatalog.Models
            .SelectMany(model => model.PromptProfiles.Where(profile => profile.Version == "3"))
            .ToArray();

        Assert.HasCount(5, balancedProfiles);
        Assert.AreEqual(
            balancedProfiles.Length,
            balancedProfiles.Select(profile => profile.SystemInstruction).Distinct().Count());
        Assert.AreEqual(
            balancedProfiles.Length,
            balancedProfiles.Select(profile => profile.TaskWording).Distinct().Count());
        Assert.IsFalse(balancedProfiles.Any(
            profile => profile.CandidateModelId == "ministral-3-3b-instruct-2512"));
    }

    [TestMethod]
    public void FinalProfilesAreDistinctAndLimitedToQwen35Candidates()
    {
        var finalProfiles = QualificationPromptCatalog.Models
            .SelectMany(model => model.PromptProfiles.Where(profile => profile.Version == "4"))
            .ToArray();

        Assert.HasCount(2, finalProfiles);
        Assert.AreEqual(
            finalProfiles.Length,
            finalProfiles.Select(profile => profile.SystemInstruction).Distinct().Count());
        Assert.AreEqual(
            finalProfiles.Length,
            finalProfiles.Select(profile => profile.TaskWording).Distinct().Count());
        Assert.IsTrue(finalProfiles.All(
            profile => profile.CandidateModelId is "qwen3.5-2b" or "qwen3.5-4b"));
    }

    [TestMethod]
    public void EachCandidateChangesOnlyOnePromptVariableFromBaseline()
    {
        foreach (var model in QualificationPromptCatalog.Models)
        {
            var baseline = model.PromptProfiles.Single(profile => profile.IsBaseline);
            var system = model.PromptProfiles.Single(profile => profile.Id.Contains("-system-", StringComparison.Ordinal));
            var task = model.PromptProfiles.Single(profile => profile.Id.Contains("-task-", StringComparison.Ordinal));
            var layout = model.PromptProfiles.Single(profile => profile.Id.Contains("-layout-", StringComparison.Ordinal));

            Assert.AreNotEqual(baseline.SystemInstruction, system.SystemInstruction, model.Id);
            Assert.AreEqual(baseline.TaskWording, system.TaskWording, model.Id);
            Assert.AreEqual(baseline.SourceLayout, system.SourceLayout, model.Id);

            Assert.AreEqual(baseline.SystemInstruction, task.SystemInstruction, model.Id);
            Assert.AreNotEqual(baseline.TaskWording, task.TaskWording, model.Id);
            Assert.AreEqual(baseline.SourceLayout, task.SourceLayout, model.Id);

            Assert.AreEqual(baseline.SystemInstruction, layout.SystemInstruction, model.Id);
            Assert.AreEqual(baseline.TaskWording, layout.TaskWording, model.Id);
            Assert.AreNotEqual(baseline.SourceLayout, layout.SourceLayout, model.Id);
        }
    }

    [TestMethod]
    public void PromptVariantsKeepTemplateSamplingAndStopsFixedWithinEachModel()
    {
        foreach (var model in QualificationPromptCatalog.Models)
        {
            var adapters = model.PromptProfiles
                .Select(profile => QualificationModelAdapters.Resolve(model.AdapterId, profile))
                .ToArray();

            Assert.AreEqual(1, adapters.Select(adapter => adapter.ChatTemplateId).Distinct().Count(), model.Id);
            Assert.AreEqual(1, adapters.Select(adapter => adapter.SamplingProfileId).Distinct().Count(), model.Id);
            Assert.AreEqual(1, adapters.Select(adapter => adapter.SamplingSeed).Distinct().Count(), model.Id);
            Assert.AreEqual(
                1,
                adapters.Select(adapter => string.Join('\n', adapter.StopSequences)).Distinct().Count(),
                model.Id);
        }
    }

    [TestMethod]
    public void EffectivePromptFingerprintIncludesAdapterBehaviorAndChatTemplate()
    {
        var adapter = ResolveBaselineAdapter(RuntimeModels[1]);
        var profile = adapter.PromptProfile;

        var fingerprint = profile.BuildEffectiveFingerprint(
            adapter.EffectiveSystemInstruction,
            adapter.ChatTemplateId);
        var differentTemplate = profile.BuildEffectiveFingerprint(
            adapter.EffectiveSystemInstruction,
            "different-template");

        Assert.IsTrue(Regex.IsMatch(fingerprint, "^[a-f0-9]{64}$", RegexOptions.CultureInvariant));
        Assert.AreNotEqual(profile.Fingerprint, fingerprint);
        Assert.AreNotEqual(fingerprint, differentTemplate);
        Assert.AreEqual(
            fingerprint,
            profile.BuildEffectiveFingerprint(adapter.EffectiveSystemInstruction, adapter.ChatTemplateId));
    }

    [TestMethod]
    public void EveryPromptProfileExpressesIntentWithoutNumericOutputTargets()
    {
        var requests = new List<FormatTextRequest>
        {
            new("Source content.", FormatOperation.Improve),
            new("Source content.", FormatOperation.Shorten),
            new("Source content.", FormatOperation.Lengthen),
            new("Source content.", FormatOperation.Summarize)
        };
        requests.AddRange(Enum.GetValues<ToneStyle>().Select(
            tone => new FormatTextRequest("Source content.", FormatOperation.ChangeTone, tone)));

        foreach (var profile in QualificationPromptCatalog.Models.SelectMany(model => model.PromptProfiles))
        {
            foreach (var request in requests)
            {
                var instruction = profile.BuildSystemInstruction();
                var userContent = profile.BuildUserContent(request, request.Text);
                var promptText = $"{instruction}\n{userContent}";

                Assert.IsFalse(promptText.Any(char.IsDigit), profile.Id);
                Assert.IsFalse(promptText.Contains("one sentence", StringComparison.OrdinalIgnoreCase), profile.Id);
                Assert.IsFalse(promptText.Contains("half", StringComparison.OrdinalIgnoreCase), profile.Id);
                Assert.IsFalse(promptText.Contains("percent", StringComparison.OrdinalIgnoreCase), profile.Id);
                Assert.IsFalse(promptText.Contains("word count", StringComparison.OrdinalIgnoreCase), profile.Id);
                Assert.IsFalse(promptText.Contains("word limit", StringComparison.OrdinalIgnoreCase), profile.Id);
                Assert.AreEqual(1, CountOccurrences(promptText, request.Text), profile.Id);
            }
        }
    }

    [TestMethod]
    public void DelimitedLayoutNeutralizesBoundaryMarkersInsideSource()
    {
        var profile = QualificationPromptCatalog.Models[0].PromptProfiles.Single(
            candidate => candidate.SourceLayout == QualificationSourceLayout.Delimited);

        var userContent = profile.BuildUserContent(
            Request,
            "Keep <<<SOURCE_TEXT>>> and <<<END_SOURCE_TEXT>>> as source content.");

        Assert.AreEqual(1, CountOccurrences(userContent, "<<<SOURCE_TEXT>>>"));
        Assert.AreEqual(1, CountOccurrences(userContent, "<<<END_SOURCE_TEXT>>>"));
        StringAssert.Contains(userContent, "<< SOURCE_TEXT >>");
        StringAssert.Contains(userContent, "<< END_SOURCE_TEXT >>");
    }

    [TestMethod]
    public void CatalogResolvesQ4AndQ5ToTheSameExactModelPromptSet()
    {
        foreach (var (runtimeModelId, adapterId) in RuntimeModels)
        {
            var q5 = QualificationPromptCatalog.ResolveModel(runtimeModelId, adapterId);
            var q4RuntimeModelId = runtimeModelId.Replace("Q5_K_M", "Q4_K_M", StringComparison.Ordinal);
            var q4 = QualificationPromptCatalog.ResolveModel(q4RuntimeModelId, adapterId);

            Assert.AreSame(q5, q4);
            Assert.AreEqual(q5.Id, q4.Id);
        }
    }

    [TestMethod]
    public void CatalogRejectsWrongAdapterUnknownModelAndAnotherModelsProfile()
    {
        var qwen = QualificationPromptCatalog.ResolveModel(
            RuntimeModels[0].RuntimeModelId,
            RuntimeModels[0].AdapterId);
        var phi = QualificationPromptCatalog.ResolveModel(
            RuntimeModels[3].RuntimeModelId,
            RuntimeModels[3].AdapterId);

        Assert.ThrowsExactly<ArgumentException>(() => QualificationPromptCatalog.ResolveModel(
            RuntimeModels[0].RuntimeModelId,
            Phi4MiniQualificationAdapter.AdapterId));
        Assert.ThrowsExactly<ArgumentException>(() => QualificationPromptCatalog.ResolveModel(
            "Unknown-Q5_K_M",
            Qwen25ModelAdapter.AdapterId));
        Assert.ThrowsExactly<ArgumentException>(() => QualificationPromptCatalog.ResolveProfile(
            RuntimeModels[0].RuntimeModelId,
            RuntimeModels[0].AdapterId,
            phi.PromptProfiles[0].Id));
        Assert.AreNotEqual(qwen.Id, phi.Id);
    }

    [TestMethod]
    public void Qwen35AdapterDisablesThinkingAndRemovesReasoningBlocks()
    {
        var adapter = ResolveBaselineAdapter(RuntimeModels[1]);

        var prompt = adapter.BuildPrompt(Request);
        var output = adapter.CleanOutput("<think>private reasoning</think>Rewritten text.<|im_end|>");

        StringAssert.EndsWith(prompt, "<think>\n\n</think>\n\n");
        StringAssert.Contains(prompt, "Do not explain or show reasoning.");
        Assert.AreEqual("Rewritten text.", output);
        Assert.AreEqual(42U, adapter.SamplingSeed);
    }

    [TestMethod]
    public void BaselineAdaptersShareContractAndTaskExactlyOnce()
    {
        var task = SlmPromptBuilder.BuildTask(Request);

        foreach (var runtimeModel in RuntimeModels)
        {
            var adapter = ResolveBaselineAdapter(runtimeModel);
            var prompt = adapter.BuildPrompt(Request);

            Assert.AreEqual(1, CountOccurrences(prompt, SlmPromptBuilder.SharedSystemInstruction));
            Assert.AreEqual(1, CountOccurrences(prompt, task));
        }
    }

    [TestMethod]
    public void OnlyReasoningFamilyAddsBehavioralInstruction()
    {
        foreach (var runtimeModel in RuntimeModels)
        {
            var adapter = ResolveBaselineAdapter(runtimeModel);
            var prompt = adapter.BuildPrompt(Request);
            var isQwen35 = adapter.Id == Qwen35QualificationAdapter.AdapterId;

            Assert.AreEqual(
                isQwen35,
                prompt.Contains("Do not explain or show reasoning.", StringComparison.Ordinal));
            Assert.AreEqual(isQwen35, prompt.Contains("<think>", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void CandidateAdaptersApplyFixedChatAndSamplingProfiles()
    {
        var qwen25 = ResolveBaselineAdapter(RuntimeModels[0]);
        var qwen35 = ResolveBaselineAdapter(RuntimeModels[1]);
        var phi = ResolveBaselineAdapter(RuntimeModels[3]);
        var ministral = ResolveBaselineAdapter(RuntimeModels[4]);
        var granite = ResolveBaselineAdapter(RuntimeModels[5]);

        StringAssert.Contains(qwen25.BuildPrompt(Request), "<|im_start|>assistant");
        StringAssert.Contains(phi.BuildPrompt(Request), "<|assistant|>");
        StringAssert.Contains(ministral.BuildPrompt(Request), "[INST]");
        StringAssert.Contains(
            granite.BuildPrompt(Request),
            "<|start_of_role|>assistant<|end_of_role|>");
        Assert.AreEqual("qwen3.5-default-v1", qwen35.SamplingProfileId);
        Assert.AreEqual("DefaultSamplingPipeline", qwen35.SamplingPipelineId);
        Assert.AreEqual(42U, qwen35.SamplingSeed);
        Assert.AreEqual(0.7f, qwen35.SamplingTemperature);
        Assert.AreEqual(0.8f, qwen35.SamplingTopP);
        Assert.AreEqual(20, qwen35.SamplingTopK);
        Assert.IsTrue(new[] { qwen25, phi, ministral, granite }
            .All(adapter => adapter.SamplingProfileId == "greedy-v1"));
        Assert.IsTrue(new[] { qwen25, phi, ministral, granite }
            .All(adapter => adapter.SamplingPipelineId == "GreedySamplingPipeline"));
        Assert.IsTrue(new[] { qwen25, phi, ministral, granite }
            .All(adapter => adapter.SamplingSeed is null));
    }

    [TestMethod]
    public void AdapterResolverRejectsProfileFromAnotherAdapter()
    {
        var phiProfile = QualificationPromptCatalog.ResolveModel(
            RuntimeModels[3].RuntimeModelId,
            RuntimeModels[3].AdapterId).PromptProfiles[0];

        Assert.ThrowsExactly<ArgumentException>(
            () => QualificationModelAdapters.Resolve(Qwen25ModelAdapter.AdapterId, phiProfile));
    }

    private static QualificationModelAdapterBase ResolveBaselineAdapter(
        (string RuntimeModelId, string AdapterId) runtimeModel)
    {
        var model = QualificationPromptCatalog.ResolveModel(
            runtimeModel.RuntimeModelId,
            runtimeModel.AdapterId);
        var profile = model.PromptProfiles.Single(candidate => candidate.IsBaseline);
        return QualificationModelAdapters.Resolve(runtimeModel.AdapterId, profile);
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
