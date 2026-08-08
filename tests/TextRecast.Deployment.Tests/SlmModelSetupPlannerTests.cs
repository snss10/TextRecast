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
            installedModelIds: []);

        var defaultChoice = choices.Single(choice =>
            choice.Profile.Id.Equals(SlmModelCatalog.Default.Id, StringComparison.Ordinal));
        Assert.IsTrue(defaultChoice.IsCompatible);
        Assert.IsTrue(choices
            .Where(choice => choice.Profile.Requirements is not null)
            .All(choice => !choice.IsCompatible));
    }

    [TestMethod]
    public void CreateChoicesKeepsInstalledQualityModelCompatibleWithoutStorageGate()
    {
        var choices = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            CreateHardware(availableStorage: 0),
            [SlmModelCatalog.Qwen35Quality.Id]);

        var qualityChoice = choices.Single(choice =>
            choice.Profile.Id.Equals(SlmModelCatalog.Qwen35Quality.Id, StringComparison.Ordinal));
        Assert.IsTrue(qualityChoice.IsInstalled);
        Assert.IsTrue(qualityChoice.IsCompatible);
    }

    [TestMethod]
    public void CreateChoicesCarriesEveryUserFacingLimitation()
    {
        var choices = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            CreateHardware(availableStorage: 10 * Gibibyte),
            installedModelIds: []);

        Assert.HasCount(SlmModelCatalog.All.Count, choices);
        Assert.IsTrue(choices.All(choice =>
            !string.IsNullOrWhiteSpace(choice.Profile.LimitationNotice)));
        Assert.IsTrue(choices.All(choice =>
            choice.Profile.LanguageSupport.Equals("English", StringComparison.Ordinal)));
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
