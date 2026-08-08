using System.Runtime.InteropServices;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Tests;

[TestClass]
public sealed class SlmModelCompatibilityEvaluatorTests
{
    private const long Mebibyte = 1024L * 1024;
    private const long Gibibyte = 1024L * Mebibyte;

    private static readonly SlmModelProfile BalancedModel = CreateProfile(
        "balanced-model",
        qualityScore: 8.6,
        peakWorkingSetBytes: 3 * Gibibyte,
        measuredTokensPerSecond: 12,
        expectedFileSize: 2 * Gibibyte);

    [TestMethod]
    public void AssessRejectsLowTemporaryMemoryAndInsufficientStorage()
    {
        var lowMemory = SlmModelCompatibilityEvaluator.Assess(
            CreateHardware(availableMemory: 2 * Gibibyte, availableStorage: 8 * Gibibyte),
            BalancedModel);
        var lowStorage = SlmModelCompatibilityEvaluator.Assess(
            CreateHardware(availableMemory: 12 * Gibibyte, availableStorage: 1 * Gibibyte),
            BalancedModel);

        Assert.IsFalse(lowMemory.IsEligible);
        Assert.IsTrue(lowMemory.RejectionReasons.Any(reason =>
            reason.Contains("available memory", StringComparison.Ordinal)));
        Assert.IsFalse(lowStorage.IsEligible);
        Assert.IsTrue(lowStorage.RejectionReasons.Any(reason =>
            reason.Contains("free model storage", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AssessSkipsDownloadStorageGateForAnInstalledModel()
    {
        var assessment = SlmModelCompatibilityEvaluator.Assess(
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
            qualityScore: 8.5,
            peakWorkingSetBytes: 1 * Gibibyte,
            measuredTokensPerSecond: 4,
            expectedFileSize: 600 * Mebibyte);
        var hardware = CreateHardware(
            availableMemory: 12 * Gibibyte,
            availableStorage: 8 * Gibibyte,
            architecture: Architecture.Arm64,
            supportsAvx2: false);

        var assessment = SlmModelCompatibilityEvaluator.Assess(hardware, slowModel);

        Assert.IsFalse(assessment.IsEligible);
        Assert.IsTrue(assessment.RejectionReasons.Any(reason =>
            reason.Contains("tokens/second", StringComparison.Ordinal)));
        Assert.IsTrue(assessment.RejectionReasons.Any(reason =>
            reason.Contains("backend", StringComparison.Ordinal)));
        Assert.IsTrue(assessment.RejectionReasons.Any(reason =>
            reason.Contains("AVX2", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AssessRejectsUnmeasuredAndUnqualifiedModels()
    {
        var unmeasured = CreateProfile("unmeasured-model", requirements: null);
        var unqualified = CreateProfile(
            "unqualified-model",
            qualityScore: 7.9,
            peakWorkingSetBytes: 1 * Gibibyte,
            measuredTokensPerSecond: 20,
            expectedFileSize: 600 * Mebibyte);
        var hardware = CreateHardware(12 * Gibibyte, 8 * Gibibyte);

        var unmeasuredAssessment = SlmModelCompatibilityEvaluator.Assess(
            hardware,
            unmeasured);
        var unqualifiedAssessment = SlmModelCompatibilityEvaluator.Assess(
            hardware,
            unqualified);

        Assert.IsFalse(unmeasuredAssessment.IsEligible);
        Assert.IsTrue(unmeasuredAssessment.RejectionReasons.Any(reason =>
            reason.Contains("no verified benchmark", StringComparison.Ordinal)));
        Assert.IsFalse(unqualifiedAssessment.IsEligible);
        Assert.IsTrue(unqualifiedAssessment.RejectionReasons.Any(reason =>
            reason.Contains("8.0/10", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void GraniteAlternativeIsEligibleOnlyWhenMeasuredRequirementsFit()
    {
        var eligible = SlmModelCompatibilityEvaluator.Assess(
            CreateHardware(availableMemory: 6 * Gibibyte, availableStorage: 4 * Gibibyte),
            SlmModelCatalog.Granite41Alternative);
        var lowMemory = SlmModelCompatibilityEvaluator.Assess(
            CreateHardware(availableMemory: 3 * Gibibyte, availableStorage: 4 * Gibibyte),
            SlmModelCatalog.Granite41Alternative);

        Assert.IsTrue(eligible.IsEligible);
        Assert.IsFalse(lowMemory.IsEligible);
    }

    [TestMethod]
    public void RequiredAvailableMemoryIncludesThirtyPercentAndFixedReserve()
    {
        var required = SlmModelCompatibilityEvaluator.CalculateRequiredAvailableMemory(
            2 * Gibibyte);

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
        double qualityScore,
        long peakWorkingSetBytes,
        double measuredTokensPerSecond,
        long expectedFileSize) => CreateProfile(
            id,
            new SlmModelRequirements
            {
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
            AdapterId = SlmRuntimeProfileIds.Qwen25AdapterId,
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
