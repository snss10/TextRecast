using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Setup.Tests;

[TestClass]
public sealed class SetupModelCatalogPresentationTests
{
    [TestMethod]
    public void InstallerShowsTheApprovedFourRowsInCatalogOrder()
    {
        var hardware = new HardwareProfile(
            TotalPhysicalMemoryBytes: 32L * 1024 * 1024 * 1024,
            AvailablePhysicalMemoryBytes: 24L * 1024 * 1024 * 1024,
            LogicalProcessorCount: 16,
            ProcessArchitecture: Architecture.X64,
            SupportsAvx2: true,
            AvailableModelStorageBytes: 100L * 1024 * 1024 * 1024,
            ModelStorageRoot: @"C:\");
        var planned = SlmModelSetupPlanner.CreateChoices(
            SlmModelCatalog.All,
            hardware,
            installedModelIds: []);

        var rows = SetupModelCatalogPresentation.CreateChoices(planned);

        Assert.AreEqual(4, rows.Length);
        AssertRow(rows[0], "Qwen 2.5 1.5B", "Fast/default", "1.04 GiB");
        AssertRow(rows[1], "Qwen 3.5 2B", "Balanced", "1.34 GiB");
        AssertRow(rows[2], "Qwen 3.5 4B", "Best quality", "2.93 GiB");
        AssertRow(rows[3], "Granite 4.1 3B", "Alternative", "2.27 GiB");
        Assert.IsTrue(rows.All(row => row.IsCompatible));
        Assert.IsTrue(rows.All(row => row.Details.Contains("Source:", StringComparison.Ordinal)));
        Assert.IsTrue(rows.All(row => row.Details.Contains("License:", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PresentationDoesNotExposeRemovedTechnicalColumns()
    {
        var properties = typeof(SetupModelChoice).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsFalse(properties.Contains("Quantization"));
        Assert.IsFalse(properties.Contains("Experimental"));
        Assert.IsFalse(properties.Contains("Language"));
        Assert.IsFalse(properties.Contains("Recommended"));
    }

    [TestMethod]
    public void SetupContainsOfflineLegalAndPrivacyDocuments()
    {
        StringAssert.Contains(SetupLegalDocuments.License, "Apache License");
        StringAssert.Contains(SetupLegalDocuments.Privacy, "Privacy");
        Assert.IsFalse(string.IsNullOrWhiteSpace(SetupLegalDocuments.Notice));
        Assert.IsFalse(string.IsNullOrWhiteSpace(SetupLegalDocuments.ThirdPartyNotices));
    }

    [TestMethod]
    public void ThirdPartyNoticesCoverProductionPackagesAndCatalogModels()
    {
        var requiredPackages = new[]
        {
            "CommunityToolkit.HighPerformance | 8.4.2",
            "LLamaSharp | 0.27.0",
            "LLamaSharp.Backend.Cpu | 0.27.0",
            "MahApps.Metro.IconPacks.Core | 6.2.1",
            "MahApps.Metro.IconPacks.Lucide | 6.2.1",
            "Microsoft.Bcl.AsyncInterfaces | 10.0.5",
            "Microsoft.Bcl.Memory | 10.0.5",
            "Microsoft.Extensions.AI.Abstractions | 10.4.1",
            "Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5",
            "Microsoft.Extensions.Logging.Abstractions | 10.0.5",
            "System.Interactive.Async | 7.0.0",
            "System.Linq.Async | 7.0.0",
            "System.Numerics.Tensors | 10.0.5"
        };

        foreach (var package in requiredPackages)
        {
            StringAssert.Contains(SetupLegalDocuments.ThirdPartyNotices, package);
        }

        StringAssert.Contains(SetupLegalDocuments.ThirdPartyNotices, "Lucide ISC License");
        StringAssert.Contains(SetupLegalDocuments.ThirdPartyNotices, "Feather MIT License");
        StringAssert.Contains(SetupLegalDocuments.ThirdPartyNotices, "NSIS 3.12");

        foreach (var profile in SlmModelCatalog.All)
        {
            StringAssert.Contains(
                SetupLegalDocuments.ThirdPartyNotices,
                profile.SourceRepository);
            StringAssert.Contains(
                SetupLegalDocuments.ThirdPartyNotices,
                profile.LicenseExpression);
        }
    }

    [STATestMethod]
    public void SetupDefinesCompleteLightAndDarkThemeResources()
    {
        var light = new Grid();
        var dark = new Grid();

        SetupTheme.Apply(light, dark: false);
        SetupTheme.Apply(dark, dark: true);

        Assert.AreEqual(
            Color.FromRgb(255, 255, 255),
            ((SolidColorBrush)light.Resources["WindowBackgroundBrush"]).Color);
        Assert.AreEqual(
            Color.FromRgb(23, 25, 28),
            ((SolidColorBrush)dark.Resources["WindowBackgroundBrush"]).Color);
        Assert.IsNotNull(light.Resources["AccentBrush"]);
        Assert.IsNotNull(dark.Resources["ProgressBrush"]);
        Assert.IsNotNull(dark.Resources["PrimaryTextBrush"]);
        Assert.IsNotNull(dark.Resources["BorderBrush"]);
    }

    [STATestMethod]
    public void CompletionDefaultsEnableLaunchDesktopAndSignInShortcuts()
    {
        using var window = new MainWindow(new CompletionPackageEngine());

        Assert.IsTrue(((CheckBox)window.FindName("LaunchCheckBox")).IsChecked);
        Assert.IsTrue(((CheckBox)window.FindName("DesktopShortcutCheckBox")).IsChecked);
        Assert.IsTrue(((CheckBox)window.FindName("StartupCheckBox")).IsChecked);

        window.Close();
    }

    private static void AssertRow(
        SetupModelChoice row,
        string expectedName,
        string expectedProfile,
        string expectedSize)
    {
        Assert.AreEqual(expectedName, row.Name);
        Assert.AreEqual(expectedProfile, row.Profile);
        Assert.AreEqual(expectedSize, row.Size);
    }

    private sealed class CompletionPackageEngine : IInstallerPackageEngine
    {
        public string DefaultInstallDirectory =>
            Path.Combine(Path.GetTempPath(), "TextRecast");

        public string ResolveInstalledDirectory() => DefaultInstallDirectory;

        public bool IsInstalled(string installDirectory) => false;

        public string ValidateInstallDirectory(string installDirectory) => installDirectory;

        public string GetInstalledApplicationPath(string installDirectory) =>
            Path.Combine(installDirectory, "Application", "TextRecast.exe");

        public void ConfigureShellIntegration(
            string installDirectory,
            bool createDesktopShortcut,
            bool launchAtStartup)
        {
        }

        public Task<PackageOperationResult> InstallAsync(string installDirectory) =>
            Task.FromResult(new PackageOperationResult(0, "Installed."));

        public Task<PackageOperationResult> UninstallAsync(string installDirectory) =>
            Task.FromResult(new PackageOperationResult(0, "Removed."));
    }
}
