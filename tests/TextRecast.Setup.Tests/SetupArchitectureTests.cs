using System.IO;
using TextRecast.Deployment.Installer;

namespace TextRecast.Setup.Tests;

[TestClass]
public sealed class SetupArchitectureTests
{
    [TestMethod]
    public void SetupAndDeploymentAssembliesDoNotReferenceInference()
    {
        var setupReferences = typeof(App).Assembly.GetReferencedAssemblies();
        var deploymentReferences = typeof(WindowsInstallerIdentity)
            .Assembly
            .GetReferencedAssemblies();

        Assert.IsFalse(setupReferences.Any(IsInferenceAssembly));
        Assert.IsFalse(deploymentReferences.Any(IsInferenceAssembly));
        Assert.IsTrue(setupReferences.Any(reference =>
            reference.Name == "TextRecast.Deployment"));
    }

    [TestMethod]
    public void DefaultInstallDirectoryUsesTheStablePerUserIdentity()
    {
        var engine = new EmbeddedPackageEngine();

        Assert.AreEqual(
            Path.Combine(
                engine.LocalApplicationDataDirectory,
                WindowsInstallerIdentity.PerUserInstallPath),
            engine.DefaultInstallDirectory);
        Assert.AreEqual(
            engine.DefaultInstallDirectory,
            engine.ValidateInstallDirectory(engine.DefaultInstallDirectory));
    }

    [TestMethod]
    public void InstallDirectoryCannotOverlapUserModelData()
    {
        var engine = new EmbeddedPackageEngine();
        var modelDirectory = Path.Combine(
            engine.LocalApplicationDataDirectory,
            "TextRecast",
            "Models");

        Assert.ThrowsExactly<ArgumentException>(() =>
            engine.ValidateInstallDirectory(modelDirectory));
    }

    [TestMethod]
    public void InstallDirectoryCannotEscapeLocalApplicationData()
    {
        var engine = new EmbeddedPackageEngine();
        var outsideDirectory = Path.Combine(
            Path.GetPathRoot(engine.LocalApplicationDataDirectory) ?? @"C:\",
            "TextRecast-outside-local-data");

        Assert.ThrowsExactly<ArgumentException>(() =>
            engine.ValidateInstallDirectory(outsideDirectory));
    }

    [TestMethod]
    public void InstallDirectoryMustBeAProductOwnedLeaf()
    {
        var engine = new EmbeddedPackageEngine();
        var sharedDirectory = Path.Combine(
            engine.LocalApplicationDataDirectory,
            "Programs");

        Assert.ThrowsExactly<ArgumentException>(() =>
            engine.ValidateInstallDirectory(sharedDirectory));
    }

    [TestMethod]
    public void NonEmptyUnownedInstallDirectoryIsRejected()
    {
        var engine = new EmbeddedPackageEngine();
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.Setup.Tests.{Guid.NewGuid():N}");
        var installDirectory = Path.Combine(testRoot, "TextRecast");
        try
        {
            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(
                Path.Combine(installDirectory, "unrelated-user-file.txt"),
                "preserve");

            Assert.ThrowsExactly<InvalidDataException>(() =>
                engine.EnsureInstallTargetIsOwnedOrEmpty(installDirectory));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void InstalledStateRequiresTheOwnedStagedPayload()
    {
        var engine = new EmbeddedPackageEngine();
        var testRoot = Path.Combine(
            engine.LocalApplicationDataDirectory,
            "Temp",
            $"TextRecast.Setup.Tests.{Guid.NewGuid():N}");
        var installDirectory = Path.Combine(testRoot, "TextRecast");
        var applicationDirectory = Path.Combine(installDirectory, "Application");
        try
        {
            Directory.CreateDirectory(applicationDirectory);
            File.WriteAllText(
                Path.Combine(installDirectory, ".textrecast-install"),
                WindowsInstallerIdentity.ProductId);
            File.WriteAllText(
                Path.Combine(applicationDirectory, "TextRecast.exe"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(applicationDirectory, ".textrecast-payload-complete"),
                "0.3.0");
            File.WriteAllText(
                Path.Combine(installDirectory, WindowsInstallerIdentity.UninstallerName),
                string.Empty);

            Assert.IsTrue(engine.IsInstalled(installDirectory));

            File.WriteAllText(
                Path.Combine(applicationDirectory, ".textrecast-payload-complete"),
                string.Empty);
            Assert.IsFalse(engine.IsInstalled(installDirectory));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task OwnedIncompleteInstallWithoutUninstallerRequiresRepair()
    {
        var engine = new EmbeddedPackageEngine();
        var testRoot = Path.Combine(
            engine.LocalApplicationDataDirectory,
            "Temp",
            $"TextRecast.Setup.Tests.{Guid.NewGuid():N}");
        var installDirectory = Path.Combine(testRoot, "TextRecast");
        try
        {
            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(
                Path.Combine(installDirectory, ".textrecast-install"),
                WindowsInstallerIdentity.ProductId);

            var result = await engine.UninstallAsync(installDirectory);

            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains(result.Message, "repair");
            Assert.IsTrue(Directory.Exists(installDirectory));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ShellIntegrationCopiesAndRemovesOnlyTheRequestedShortcuts()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.Shortcut.Tests.{Guid.NewGuid():N}");
        var sourceShortcut = Path.Combine(testRoot, "Programs", "TextRecast.lnk");
        var desktopShortcut = Path.Combine(testRoot, "Desktop", "TextRecast.lnk");
        var startupShortcut = Path.Combine(testRoot, "Startup", "TextRecast.lnk");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sourceShortcut)!);
            File.WriteAllText(sourceShortcut, "TextRecast shortcut fixture");

            EmbeddedPackageEngine.ConfigureShortcutFiles(
                sourceShortcut,
                desktopShortcut,
                startupShortcut,
                createDesktopShortcut: true,
                launchAtStartup: true);

            Assert.AreEqual(
                File.ReadAllText(sourceShortcut),
                File.ReadAllText(desktopShortcut));
            Assert.AreEqual(
                File.ReadAllText(sourceShortcut),
                File.ReadAllText(startupShortcut));

            EmbeddedPackageEngine.ConfigureShortcutFiles(
                sourceShortcut,
                desktopShortcut,
                startupShortcut,
                createDesktopShortcut: false,
                launchAtStartup: true);

            Assert.IsFalse(File.Exists(desktopShortcut));
            Assert.IsTrue(File.Exists(startupShortcut));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void ShellIntegrationRequiresTheOwnedStartMenuShortcut()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.Shortcut.Tests.{Guid.NewGuid():N}");

        Assert.ThrowsExactly<FileNotFoundException>(() =>
            EmbeddedPackageEngine.ConfigureShortcutFiles(
                Path.Combine(testRoot, "missing.lnk"),
                Path.Combine(testRoot, "desktop.lnk"),
                Path.Combine(testRoot, "startup.lnk"),
                createDesktopShortcut: true,
                launchAtStartup: true));
    }

    private static bool IsInferenceAssembly(System.Reflection.AssemblyName reference)
    {
        return reference.Name is not null &&
            (reference.Name.Equals(
                    "TextRecast.Infrastructure",
                    StringComparison.OrdinalIgnoreCase) ||
                reference.Name.StartsWith(
                    "LLamaSharp",
                    StringComparison.OrdinalIgnoreCase));
    }
}
