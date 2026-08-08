using System.Runtime.InteropServices;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Tests;

[TestClass]
public sealed class SlmModelSetupPlannerTests
{
    private const long Gibibyte = 1024L * 1024 * 1024;

    [TestMethod]
    public void CreateChoicesKeepsDefaultAvailableWhenHardwareInspectionIsUnavailable()
    {
        var choices = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            hardware: null,
            installedModelIds: [],
            SlmModelCatalog.Default.Id);

        var defaultChoice = choices.Single(choice =>
            choice.Profile.Id.Equals(SlmModelCatalog.Default.Id, StringComparison.Ordinal));
        Assert.IsTrue(defaultChoice.IsCompatible);
        Assert.IsTrue(defaultChoice.IsRecommended);
        Assert.IsTrue(choices
            .Where(choice => choice.Profile.Requirements is not null)
            .All(choice => !choice.IsCompatible && !choice.IsRecommended));
    }

    [TestMethod]
    public void CreateChoicesMarksInstalledQualityModelAsRecommendedWithoutStorageGate()
    {
        var choices = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            CreateHardware(availableStorage: 0),
            [SlmModelCatalog.Qwen35Quality.Id],
            SlmModelCatalog.Default.Id);

        var qualityChoice = choices.Single(choice =>
            choice.Profile.Id.Equals(SlmModelCatalog.Qwen35Quality.Id, StringComparison.Ordinal));
        Assert.IsTrue(qualityChoice.IsInstalled);
        Assert.IsTrue(qualityChoice.IsCompatible);
        Assert.IsTrue(qualityChoice.IsRecommended);
    }

    [TestMethod]
    public void CreateChoicesCarriesEveryUserFacingLimitation()
    {
        var choices = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            CreateHardware(availableStorage: 10 * Gibibyte),
            installedModelIds: [],
            SlmModelCatalog.Default.Id);

        Assert.HasCount(SlmModelCatalog.All.Count, choices);
        Assert.IsTrue(choices.All(choice =>
            !string.IsNullOrWhiteSpace(choice.Profile.LimitationNotice)));
        Assert.IsTrue(choices.All(choice =>
            choice.Profile.LanguageSupport.Equals("English", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreateChoicesRejectsFallbackOutsideCatalog()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            SlmModelSetupPlanner.CreateChoices(
                SlmModelCatalog.All,
                hardware: null,
                installedModelIds: [],
                fallbackModelId: "not-in-catalog"));
    }

    private static HardwareProfile CreateHardware(long availableStorage) => new(
        TotalPhysicalMemoryBytes: 16 * Gibibyte,
        AvailablePhysicalMemoryBytes: 12 * Gibibyte,
        LogicalProcessorCount: 8,
        ProcessArchitecture: Architecture.X64,
        SupportsAvx2: true,
        AvailableModelStorageBytes: availableStorage,
        ModelStorageRoot: @"C:\");
}
