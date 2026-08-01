using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelAdapterRegistryTests
{
    [TestMethod]
    public void DefaultResolvesAdapterFromModelProfile()
    {
        var adapter = SlmModelAdapterRegistry.Default.Resolve(SlmModelCatalog.Default);

        Assert.IsInstanceOfType<Qwen25ModelAdapter>(adapter);
        Assert.AreEqual(SlmModelCatalog.Default.AdapterId, adapter.Id);
    }

    [TestMethod]
    public void ResolveRejectsUnknownAdapterBeforeModelLoading()
    {
        var profile = SlmModelCatalog.Default with { AdapterId = "unsupported-adapter" };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => SlmModelAdapterRegistry.Default.Resolve(profile));

        StringAssert.Contains(exception.Message, profile.Id);
        StringAssert.Contains(exception.Message, profile.AdapterId);
    }

    [TestMethod]
    public void DefaultResolvesQwen35AdapterFromBalancedProfile()
    {
        var adapter = SlmModelAdapterRegistry.Default.Resolve(SlmModelCatalog.Qwen35Balanced);

        Assert.IsInstanceOfType<Qwen35ModelAdapter>(adapter);
        Assert.AreEqual(Qwen35ModelAdapter.AdapterId, adapter.Id);
    }

    [TestMethod]
    public void DefaultResolvesQwen35AdapterFromQualityProfile()
    {
        var adapter = SlmModelAdapterRegistry.Default.Resolve(SlmModelCatalog.Qwen35Quality);

        Assert.IsInstanceOfType<Qwen35ModelAdapter>(adapter);
        Assert.AreEqual(Qwen35ModelAdapter.AdapterId, adapter.Id);
    }
}
