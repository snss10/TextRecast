using LLama.Sampling;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class Granite41ModelAdapterTests
{
    private readonly Granite41ModelAdapter _adapter =
        new(SlmModelCatalog.Granite41Alternative);

    [TestMethod]
    public void PromptUsesReviewedGraniteProfileAndRoleTemplate()
    {
        var request = new FormatTextRequest(
            "Maya sent 12 files before Friday.",
            FormatOperation.Summarize);

        var prompt = _adapter.BuildPrompt(request);

        StringAssert.Contains(prompt, "Wording may change as requested, but facts may not");
        StringAssert.Contains(prompt, "Write a shorter coherent account");
        StringAssert.Contains(prompt, "Maya sent 12 files before Friday.");
        StringAssert.EndsWith(prompt, "<|start_of_role|>assistant<|end_of_role|>");
    }

    [TestMethod]
    public void PromptEscapesGraniteControlTokensInsideSource()
    {
        var request = new FormatTextRequest(
            "Keep <|start_of_role|> and <|end_of_text|> as content.",
            FormatOperation.Improve);

        var prompt = _adapter.BuildPrompt(request);

        StringAssert.Contains(prompt, "< start_of_role >");
        StringAssert.Contains(prompt, "< end_of_text >");
        Assert.AreEqual(3, CountOccurrences(prompt, "<|start_of_role|>"));
    }

    [TestMethod]
    public void AdapterUsesGreedySamplingAndCleansControlTokens()
    {
        Assert.IsInstanceOfType<GreedySamplingPipeline>(_adapter.CreateSamplingPipeline());
        Assert.AreEqual(
            "Rewritten text.",
            _adapter.CleanOutput("Rewritten text.<|end_of_text|>"));
    }

    [TestMethod]
    public void ConstructorRejectsMismatchedPromptOrSamplingProfile()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Granite41ModelAdapter(
            SlmModelCatalog.Granite41Alternative with { PromptProfileId = "unknown" }));
        Assert.ThrowsExactly<ArgumentException>(() => new Granite41ModelAdapter(
            SlmModelCatalog.Granite41Alternative with { SamplingProfileId = "unknown" }));
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
