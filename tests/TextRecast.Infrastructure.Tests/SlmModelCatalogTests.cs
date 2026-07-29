using System.IO;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelCatalogTests
{
    [TestMethod]
    public void DefaultPreservesVersion010ModelProfile()
    {
        var profile = SlmModelCatalog.Default;

        Assert.AreEqual("Qwen2.5-1.5B-Instruct-Q4_K_M", profile.Id);
        Assert.AreEqual("qwen2.5-1.5b-instruct-q4_k_m.gguf", profile.FileName);
        Assert.AreEqual(
            "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf?download=true",
            profile.DownloadUri.AbsoluteUri);
        Assert.AreEqual(1117320736L, profile.ExpectedFileSize);
        Assert.AreEqual(
            "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
            profile.ExpectedSha256);
        Assert.AreEqual(4096U, profile.ContextSize);
        Assert.AreEqual(768, profile.MaxOutputTokens);
    }

    [TestMethod]
    public void InstallerUsesDefaultProfileFileName()
    {
        var installer = new SlmModelInstaller(SlmModelCatalog.Default);

        Assert.AreEqual(
            SlmModelCatalog.Default.FileName,
            Path.GetFileName(installer.PackagedModelPath));
        Assert.AreEqual(
            SlmModelCatalog.Default.FileName,
            Path.GetFileName(installer.UserModelPath));
    }
}
