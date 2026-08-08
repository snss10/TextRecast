using System.IO;
using TextRecast.Deployment.Setup;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Tests;

[TestClass]
public sealed class SetupRecoveryLauncherTests
{
    [TestMethod]
    public void CompleteStateWithVerifiedPathStartsApplication()
    {
        var profile = SlmModelCatalog.Default;
        var state = ModelSetupState.Default.ActivateVerifiedModel(
            profile,
            DateTimeOffset.UtcNow);

        var required = SetupRecoveryLauncher.IsRecoveryRequired(
            state,
            profile.Id,
            @"C:\Models\model.gguf");

        Assert.IsFalse(required);
    }

    [TestMethod]
    public void PendingStateRequiresRecoveryEvenWithAnOlderActiveModel()
    {
        var profile = SlmModelCatalog.Default;
        var state = ModelSetupState.Default
            .ActivateVerifiedModel(profile, DateTimeOffset.UtcNow)
            .BeginPendingSetup(
                SlmModelCatalog.Qwen35Balanced.Id,
                "0.3.0",
                DateTimeOffset.UtcNow);

        var required = SetupRecoveryLauncher.IsRecoveryRequired(
            state,
            profile.Id,
            @"C:\Models\model.gguf");

        Assert.IsTrue(required);
    }

    [TestMethod]
    public void MissingVerifiedPathRequiresRecovery()
    {
        var profile = SlmModelCatalog.Default;
        var state = ModelSetupState.Default.ActivateVerifiedModel(
            profile,
            DateTimeOffset.UtcNow);

        Assert.IsTrue(SetupRecoveryLauncher.IsRecoveryRequired(
            state,
            profile.Id,
            verifiedModelPath: null));
    }

    [TestMethod]
    public void RecoveryHostIsFoundBesideTheApplicationDirectory()
    {
        using var directory = new TemporaryDirectory();
        var applicationDirectory = Path.Combine(directory.Path, "Application");
        Directory.CreateDirectory(applicationDirectory);
        var setupPath = Path.Combine(directory.Path, "TextRecast.Setup.exe");
        File.WriteAllText(setupPath, "test setup host");

        var found = SetupRecoveryLauncher.FindSetupHost(applicationDirectory);

        Assert.AreEqual(setupPath, found);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"TextRecast.App.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
