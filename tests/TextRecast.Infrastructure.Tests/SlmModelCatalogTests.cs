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
        Assert.AreEqual("Qwen 2.5 1.5B Instruct", profile.DisplayName);
        Assert.AreEqual(SlmModelRole.Fast, profile.Role);
        Assert.AreEqual("English", profile.LanguageSupport);
        Assert.IsFalse(profile.IsExperimental);
        Assert.AreEqual(Qwen25ModelAdapter.AdapterId, profile.AdapterId);
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
    public void CatalogIncludesExactQwen35BalancedProfile()
    {
        var profile = SlmModelCatalog.Qwen35Balanced;

        Assert.HasCount(4, SlmModelCatalog.All);
        Assert.AreSame(profile, SlmModelCatalog.GetById(profile.Id));
        Assert.AreEqual("Qwen3.5-2B-Q5_K_M", profile.Id);
        Assert.AreEqual("Qwen 3.5 2B", profile.DisplayName);
        Assert.AreEqual(SlmModelRole.Balanced, profile.Role);
        Assert.IsTrue(profile.IsExperimental);
        Assert.AreEqual("English", profile.LanguageSupport);
        StringAssert.Contains(profile.LimitationNotice, "Summaries");
        Assert.AreEqual(Qwen35ModelAdapter.AdapterId, profile.AdapterId);
        Assert.AreEqual(Qwen35ModelAdapter.BalancedPromptProfileId, profile.PromptProfileId);
        Assert.AreEqual(Qwen35ModelAdapter.DefaultSamplingProfileId, profile.SamplingProfileId);
        Assert.AreEqual("Qwen3.5-2B-Q5_K_M.gguf", profile.FileName);
        Assert.AreEqual(1435238656L, profile.ExpectedFileSize);
        Assert.AreEqual(
            "1885b3a9195f8cc09da9a7a7a75afdc1e8d5cbf9fc4a499c3961dddea37098ac",
            profile.ExpectedSha256);
        Assert.AreEqual("unsloth/Qwen3.5-2B-GGUF", profile.SourceRepository);
        Assert.AreEqual("f6d5376be1edb4d416d56da11e5397a961aca8ae", profile.SourceRevision);
        Assert.AreEqual("Apache-2.0", profile.LicenseExpression);
        Assert.AreEqual(1708875776L, profile.Requirements!.PeakWorkingSetBytes);
        Assert.AreEqual(14.20, profile.Requirements.MeasuredTokensPerSecond);
    }

    [TestMethod]
    public void CatalogIncludesExactQwen35QualityProfile()
    {
        var profile = SlmModelCatalog.Qwen35Quality;

        Assert.AreSame(profile, SlmModelCatalog.GetById(profile.Id));
        Assert.AreEqual("Qwen3.5-4B-Q5_K_M", profile.Id);
        Assert.AreEqual("Qwen 3.5 4B", profile.DisplayName);
        Assert.AreEqual(SlmModelRole.Quality, profile.Role);
        Assert.IsTrue(profile.IsExperimental);
        StringAssert.Contains(profile.LimitationNotice, "deadline wording");
        Assert.AreEqual(Qwen35ModelAdapter.QualityPromptProfileId, profile.PromptProfileId);
        Assert.AreEqual("Qwen3.5-4B-Q5_K_M.gguf", profile.FileName);
        Assert.AreEqual(3143656608L, profile.ExpectedFileSize);
        Assert.AreEqual(
            "8814232b85594dcd46c50e5b8b29324a7efe9e746edbe8a3d1df3d3fce7aad39",
            profile.ExpectedSha256);
        Assert.AreEqual("unsloth/Qwen3.5-4B-GGUF", profile.SourceRepository);
        Assert.AreEqual("e87f176479d0855a907a41277aca2f8ee7a09523", profile.SourceRevision);
        Assert.AreEqual(3515650048L, profile.Requirements!.PeakWorkingSetBytes);
        Assert.AreEqual(6.27, profile.Requirements.MeasuredTokensPerSecond);
    }

    [TestMethod]
    public void CatalogIncludesExactGraniteAlternativeProfile()
    {
        var profile = SlmModelCatalog.Granite41Alternative;

        Assert.AreSame(profile, SlmModelCatalog.GetById(profile.Id));
        Assert.AreEqual("Granite-4.1-3B-Q5_K_M", profile.Id);
        Assert.AreEqual("Granite 4.1 3B", profile.DisplayName);
        Assert.AreEqual(SlmModelRole.Alternative, profile.Role);
        Assert.IsTrue(profile.IsExperimental);
        StringAssert.Contains(profile.LimitationNotice, "semantic drift");
        Assert.AreEqual(Granite41ModelAdapter.AdapterId, profile.AdapterId);
        Assert.AreEqual(Granite41ModelAdapter.BalancedPromptProfileId, profile.PromptProfileId);
        Assert.AreEqual(Granite41ModelAdapter.GreedySamplingProfileId, profile.SamplingProfileId);
        Assert.AreEqual("granite-4.1-3b-Q5_K_M.gguf", profile.FileName);
        Assert.AreEqual(2437012064L, profile.ExpectedFileSize);
        Assert.AreEqual(
            "f7724d259f29b0edf147144ac530ca26f91c97af8274249f933073c461678a3c",
            profile.ExpectedSha256);
        Assert.AreEqual("ibm-granite/granite-4.1-3b-GGUF", profile.SourceRepository);
        Assert.AreEqual("ab4701481089b58a082ef63cc1cee738887293ff", profile.SourceRevision);
        Assert.AreEqual(2896392192L, profile.Requirements!.PeakWorkingSetBytes);
        Assert.AreEqual(9.13, profile.Requirements.MeasuredTokensPerSecond);
    }

    [TestMethod]
    public void CatalogRejectsUnknownModelIdentifier()
    {
        Assert.ThrowsExactly<ArgumentException>(() => SlmModelCatalog.GetById("unknown"));
    }
}
