using TextRecast.Deployment.Installer;

namespace TextRecast.Deployment.Tests;

[TestClass]
public sealed class WindowsInstallerIdentityTests
{
    [TestMethod]
    public void InstallerIdentityMatchesTheEstablishedPerUserProduct()
    {
        Assert.AreEqual(
            "{FF637B7C-6CB2-470E-A9A6-7366AA51A3D6}",
            ReadConstant(nameof(WindowsInstallerIdentity.ProductId)));
        Assert.AreEqual(
            "TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.ProductName)));
        Assert.AreEqual(
            "snss10",
            ReadConstant(nameof(WindowsInstallerIdentity.Publisher)));
        Assert.AreEqual(
            "TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.InstallFolderName)));
        Assert.AreEqual(
            @"Programs\TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.PerUserInstallPath)));
        Assert.AreEqual(
            "TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.StartMenuFolderName)));
        Assert.AreEqual(
            "TextRecast.lnk",
            ReadConstant(nameof(WindowsInstallerIdentity.StartMenuShortcutName)));
        Assert.AreEqual(
            "TextRecast.exe",
            ReadConstant(nameof(WindowsInstallerIdentity.ExecutableName)));
        Assert.AreEqual(
            "Uninstall.exe",
            ReadConstant(nameof(WindowsInstallerIdentity.UninstallerName)));
        Assert.AreEqual(
            @"Software\TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.ApplicationRegistryKey)));
        Assert.AreEqual(
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast",
            ReadConstant(nameof(WindowsInstallerIdentity.UninstallRegistryKey)));
    }

    [TestMethod]
    public void StableIdentityContainsNoReleaseVersion()
    {
        var stableValues = new[]
        {
            ReadConstant(nameof(WindowsInstallerIdentity.ProductId)),
            ReadConstant(nameof(WindowsInstallerIdentity.ProductName)),
            ReadConstant(nameof(WindowsInstallerIdentity.Publisher)),
            ReadConstant(nameof(WindowsInstallerIdentity.InstallFolderName)),
            ReadConstant(nameof(WindowsInstallerIdentity.PerUserInstallPath)),
            ReadConstant(nameof(WindowsInstallerIdentity.StartMenuFolderName)),
            ReadConstant(nameof(WindowsInstallerIdentity.StartMenuShortcutName)),
            ReadConstant(nameof(WindowsInstallerIdentity.ExecutableName)),
            ReadConstant(nameof(WindowsInstallerIdentity.UninstallerName)),
            ReadConstant(nameof(WindowsInstallerIdentity.ApplicationRegistryKey)),
            ReadConstant(nameof(WindowsInstallerIdentity.UninstallRegistryKey))
        };

        Assert.IsFalse(stableValues.Any(value =>
            value.Contains("0.3.0", StringComparison.Ordinal) ||
            value.Contains("v0.3.0", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DefaultInstallDirectoryIsResolvedBelowLocalApplicationData()
    {
        var localApplicationData = Path.Combine(
            Path.GetTempPath(),
            "TextRecast.Identity.Tests");

        Assert.AreEqual(
            Path.Combine(localApplicationData, @"Programs\TextRecast"),
            WindowsInstallerIdentity.GetDefaultInstallDirectory(
                localApplicationData));
    }

    private static string ReadConstant(string fieldName)
    {
        var field = typeof(WindowsInstallerIdentity).GetField(fieldName) ??
            throw new AssertFailedException($"Installer identity field '{fieldName}' was not found.");
        Assert.IsTrue(field.IsLiteral);
        Assert.IsFalse(field.IsInitOnly);
        return (string)(field.GetRawConstantValue() ??
            throw new AssertFailedException($"Installer identity field '{fieldName}' is null."));
    }
}
