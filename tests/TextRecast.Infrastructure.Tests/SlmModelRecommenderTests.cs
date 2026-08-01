using System.Runtime.InteropServices;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelRecommenderTests
{
    private const long Mebibyte = 1024L * 1024;
    private const long Gibibyte = 1024L * Mebibyte;

    private static readonly SlmModelProfile FastModel = CreateProfile(
        "fast-model",
        SlmModelTier.Fast,
        qualityScore: 8.1,
        peakWorkingSetBytes: 1 * Gibibyte,
        measuredTokensPerSecond: 20,
        expectedFileSize: 600 * Mebibyte);

    private static readonly SlmModelProfile BalancedModel = CreateProfile(
        "balanced-model",
        SlmModelTier.Balanced,
        qualityScore: 8.6,
        peakWorkingSetBytes: 3 * Gibibyte,
        measuredTokensPerSecond: 12,
        expectedFileSize: 2 * Gibibyte);

    private static readonly SlmModelProfile QualityModel = CreateProfile(
        "quality-model",
        SlmModelTier.Quality,
        qualityScore: 9.2,
        peakWorkingSetBytes: 7 * Gibibyte,
        measuredTokensPerSecond: 7,
        expectedFileSize: 4 * Gibibyte);

    private static readonly SlmModelProfile[] AllModels =
        [FastModel, BalancedModel, QualityModel];

    [TestMethod]
    public void RecommendReturnsNoSafeModelWhenEveryProfileFails()
    {
        var result = SlmModelRecommender.Recommend(
            CreateHardware(availableMemory: 1 * Gibibyte, availableStorage: 1 * Gibibyte),
            AllModels);

        Assert.IsNull(result.RecommendedProfile);
        Assert.HasCount(3, result.Assessments);
        Assert.IsTrue(result.Assessments.All(assessment => !assessment.IsEligible));
        StringAssert.Contains(result.Reason, "No model safely meets");
    }

    [TestMethod]
    public void RecommendSelectsFastModelOnFastOnlyHardware()
    {
        var result = SlmModelRecommender.Recommend(
            CreateHardware(availableMemory: 2 * Gibibyte, availableStorage: 2 * Gibibyte),
            AllModels);

        Assert.AreSame(FastModel, result.RecommendedProfile);
    }

    [TestMethod]
    public void RecommendSelectsBalancedModelWhenItIsTheHighestSafeQuality()
    {
        var result = SlmModelRecommender.Recommend(
            CreateHardware(availableMemory: 5 * Gibibyte, availableStorage: 5 * Gibibyte),
            AllModels);

        Assert.AreSame(BalancedModel, result.RecommendedProfile);
        StringAssert.Contains(result.Reason, "quality 8.6/10");
    }

    [TestMethod]
    public void RecommendSelectsQualityModelOnQualityCapableHardware()
    {
        var result = SlmModelRecommender.Recommend(
            CreateHardware(availableMemory: 12 * Gibibyte, availableStorage: 8 * Gibibyte),
            AllModels);

        Assert.AreSame(QualityModel, result.RecommendedProfile);
    }

    [TestMethod]
    public void RecommendPrefersHigherTierWhenEligibleScoresAreClose()
    {
        var balanced = CreateProfile(
            "balanced-close-score",
            SlmModelTier.Balanced,
            qualityScore: 9.8,
            peakWorkingSetBytes: 2 * Gibibyte,
            measuredTokensPerSecond: 12,
            expectedFileSize: 2 * Gibibyte);
        var quality = CreateProfile(
            "quality-close-score",
            SlmModelTier.Quality,
            qualityScore: 9.7,
            peakWorkingSetBytes: 3 * Gibibyte,
            measuredTokensPerSecond: 8,
            expectedFileSize: 3 * Gibibyte);

        var result = SlmModelRecommender.Recommend(
            CreateHardware(availableMemory: 8 * Gibibyte, availableStorage: 8 * Gibibyte),
            [balanced, quality]);

        Assert.AreSame(quality, result.RecommendedProfile);
        StringAssert.Contains(result.Reason, "Quality tier");
    }

    [TestMethod]
    public void AssessRejectsLowTemporaryMemoryAndInsufficientStorage()
    {
        var lowMemory = SlmModelRecommender.Assess(
            CreateHardware(availableMemory: 2 * Gibibyte, availableStorage: 8 * Gibibyte),
            BalancedModel);
        var lowStorage = SlmModelRecommender.Assess(
            CreateHardware(availableMemory: 12 * Gibibyte, availableStorage: 1 * Gibibyte),
            BalancedModel);

        Assert.IsFalse(lowMemory.IsEligible);
        Assert.IsTrue(lowMemory.RejectionReasons.Any(reason => reason.Contains("available memory", StringComparison.Ordinal)));
        Assert.IsFalse(lowStorage.IsEligible);
        Assert.IsTrue(lowStorage.RejectionReasons.Any(reason => reason.Contains("free model storage", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AssessSkipsDownloadStorageGateForAnInstalledModel()
    {
        var assessment = SlmModelRecommender.Assess(
            CreateHardware(availableMemory: 12 * Gibibyte, availableStorage: 0),
            BalancedModel,
            modelInstalled: true);

        Assert.IsTrue(assessment.IsEligible);
    }

    [TestMethod]
    public void AssessRejectsUnsupportedBackendAndSlowMeasuredResponse()
    {
        var slowModel = CreateProfile(
            "slow-model",
            SlmModelTier.Fast,
            qualityScore: 8.5,
            peakWorkingSetBytes: 1 * Gibibyte,
            measuredTokensPerSecond: 4,
            expectedFileSize: 600 * Mebibyte);
        var hardware = CreateHardware(
            availableMemory: 12 * Gibibyte,
            availableStorage: 8 * Gibibyte,
            architecture: Architecture.Arm64,
            supportsAvx2: false);

        var assessment = SlmModelRecommender.Assess(hardware, slowModel);

        Assert.IsFalse(assessment.IsEligible);
        Assert.IsTrue(assessment.RejectionReasons.Any(reason => reason.Contains("tokens/second", StringComparison.Ordinal)));
        Assert.IsTrue(assessment.RejectionReasons.Any(reason => reason.Contains("backend", StringComparison.Ordinal)));
        Assert.IsTrue(assessment.RejectionReasons.Any(reason => reason.Contains("AVX2", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AssessRejectsUnmeasuredAndUnqualifiedModelsWithManualWarning()
    {
        var unmeasured = CreateProfile("unmeasured-model", requirements: null);
        var unqualified = CreateProfile(
            "unqualified-model",
            SlmModelTier.Fast,
            qualityScore: 7.9,
            peakWorkingSetBytes: 1 * Gibibyte,
            measuredTokensPerSecond: 20,
            expectedFileSize: 600 * Mebibyte);
        var hardware = CreateHardware(12 * Gibibyte, 8 * Gibibyte);

        var unmeasuredAssessment = SlmModelRecommender.Assess(hardware, unmeasured);
        var unqualifiedAssessment = SlmModelRecommender.Assess(hardware, unqualified);

        Assert.IsNotNull(unmeasuredAssessment.ManualOverrideWarning);
        StringAssert.Contains(unmeasuredAssessment.ManualOverrideWarning, "no verified benchmark");
        Assert.IsNotNull(unqualifiedAssessment.ManualOverrideWarning);
        StringAssert.Contains(unqualifiedAssessment.ManualOverrideWarning, "8.0/10");
    }

    [TestMethod]
    public void GraniteAlternativeIsEligibleOnlyWhenMeasuredRequirementsFit()
    {
        var eligible = SlmModelRecommender.Assess(
            CreateHardware(availableMemory: 6 * Gibibyte, availableStorage: 4 * Gibibyte),
            SlmModelCatalog.Granite41Alternative);
        var lowMemory = SlmModelRecommender.Assess(
            CreateHardware(availableMemory: 3 * Gibibyte, availableStorage: 4 * Gibibyte),
            SlmModelCatalog.Granite41Alternative);

        Assert.IsTrue(eligible.IsEligible);
        Assert.IsFalse(lowMemory.IsEligible);
    }

    [TestMethod]
    public void RequiredAvailableMemoryIncludesThirtyPercentAndFixedReserve()
    {
        var required = SlmModelRecommender.CalculateRequiredAvailableMemory(2 * Gibibyte);

        Assert.AreEqual(3_328_599_655L, required);
    }

    private static HardwareProfile CreateHardware(
        long availableMemory,
        long availableStorage,
        Architecture architecture = Architecture.X64,
        bool supportsAvx2 = true) => new(
            TotalPhysicalMemoryBytes: 16 * Gibibyte,
            AvailablePhysicalMemoryBytes: availableMemory,
            LogicalProcessorCount: 8,
            ProcessArchitecture: architecture,
            SupportsAvx2: supportsAvx2,
            AvailableModelStorageBytes: availableStorage,
            ModelStorageRoot: @"C:\");

    private static SlmModelProfile CreateProfile(
        string id,
        SlmModelTier tier,
        double qualityScore,
        long peakWorkingSetBytes,
        double measuredTokensPerSecond,
        long expectedFileSize) => CreateProfile(
            id,
            new SlmModelRequirements
            {
                Tier = tier,
                QualityScore = qualityScore,
                PeakWorkingSetBytes = peakWorkingSetBytes,
                MeasuredTokensPerSecond = measuredTokensPerSecond
            },
            expectedFileSize);

    private static SlmModelProfile CreateProfile(
        string id,
        SlmModelRequirements? requirements,
        long expectedFileSize = 600 * Mebibyte) => new()
        {
            Id = id,
            DisplayName = id,
            Role = SlmModelRole.Fast,
            Description = "Test model.",
            LanguageSupport = "English",
            LimitationNotice = "Test limitation.",
            IsExperimental = false,
            AdapterId = Qwen25ModelAdapter.AdapterId,
            PromptProfileId = "test-prompt-v1",
            SamplingProfileId = "greedy-v1",
            FileName = id + ".gguf",
            DownloadUri = new Uri("https://example.com/" + id + ".gguf"),
            ExpectedSha256 = new string('0', 64),
            ExpectedFileSize = expectedFileSize,
            SourceRepository = "example/test",
            SourceRevision = "test-revision",
            LicenseExpression = "Apache-2.0",
            Requirements = requirements
        };
}
