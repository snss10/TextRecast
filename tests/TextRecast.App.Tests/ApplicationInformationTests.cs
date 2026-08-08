using TextRecast.App.Presentation;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Tests;

[TestClass]
public sealed class ApplicationInformationTests
{
    [TestMethod]
    public void ModelInformationUsesCatalogValues()
    {
        var profile = SlmModelCatalog.Qwen35Balanced;

        var content = ApplicationInformation.CreateModel(profile);
        var details = content.Pages.Single().Content;

        Assert.AreEqual("Model information", content.Title);
        Assert.AreEqual(profile.DisplayName, content.Heading);
        StringAssert.Contains(details, "Balanced");
        StringAssert.Contains(details, "1.34 GiB");
        StringAssert.Contains(details, profile.SourceRepository);
        StringAssert.Contains(details, profile.LicenseExpression);
        StringAssert.Contains(details, profile.LimitationNotice);
    }

    [TestMethod]
    public void AboutInformationUsesRuntimeAssemblyVersion()
    {
        var content = ApplicationInformation.CreateAbout();

        Assert.AreEqual("About TextRecast", content.Title);
        StringAssert.Contains(content.Pages.Single().Content, "Version");
        StringAssert.Contains(content.Pages.Single().Content, "Apache License 2.0");
    }
}
