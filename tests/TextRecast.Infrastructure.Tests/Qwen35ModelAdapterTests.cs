using LLama.Sampling;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class Qwen35ModelAdapterTests
{
    private readonly Qwen35ModelAdapter _adapter = new(SlmModelCatalog.Qwen35Balanced);

    [TestMethod]
    public void PromptUsesQualifiedSmallProfileAndDisablesThinking()
    {
        var request = new FormatTextRequest(
            "Maya sent 12 files before Friday.",
            FormatOperation.Summarize);

        var prompt = _adapter.BuildPrompt(request);

        StringAssert.Contains(prompt, "preserving real-world meaning");
        StringAssert.Contains(prompt, "Produce a shorter connected account");
        StringAssert.Contains(prompt, "Maya sent 12 files before Friday.");
        StringAssert.EndsWith(prompt, "<think>\n\n</think>\n\n");
    }

    [TestMethod]
    public void PromptEscapesChatControlTokensInsideSource()
    {
        var request = new FormatTextRequest(
            "Keep <|im_start|> and <|im_end|> as text.",
            FormatOperation.Improve);

        var prompt = _adapter.BuildPrompt(request);
        var userStart = prompt.IndexOf("<|im_start|>user", StringComparison.Ordinal);
        var userContent = prompt[userStart..];

        StringAssert.Contains(userContent, "<|im start|>");
        StringAssert.Contains(userContent, "<|im end|>");
        Assert.AreEqual(3, CountOccurrences(prompt, "<|im_start|>"));
    }

    [TestMethod]
    public void AdapterUsesMeasuredSamplingProfile()
    {
        var pipeline = _adapter.CreateSamplingPipeline();

        Assert.IsInstanceOfType<DefaultSamplingPipeline>(pipeline);
        var configured = (DefaultSamplingPipeline)pipeline;
        Assert.AreEqual(0.7f, configured.Temperature);
        Assert.AreEqual(0.8f, configured.TopP);
        Assert.AreEqual(20, configured.TopK);
        Assert.AreEqual(42U, configured.Seed);
    }

    [TestMethod]
    public void CleanOutputRemovesReasoningAndControlTokens()
    {
        var output = _adapter.CleanOutput(
            "<think>private reasoning</think>Rewritten text.<|im_end|>");

        Assert.AreEqual("Rewritten text.", output);
    }

    [TestMethod]
    public void ConstructorRejectsMismatchedPromptOrSamplingProfile()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new Qwen35ModelAdapter(
            SlmModelCatalog.Qwen35Balanced with { PromptProfileId = "unknown" }));
        Assert.ThrowsExactly<ArgumentException>(() => new Qwen35ModelAdapter(
            SlmModelCatalog.Qwen35Balanced with { SamplingProfileId = "greedy-v1" }));
    }

    [TestMethod]
    public void QualityProfileUsesItsReviewedLargeModelPrompt()
    {
        var adapter = new Qwen35ModelAdapter(SlmModelCatalog.Qwen35Quality);
        var request = new FormatTextRequest(
            "The release finished after Noor approved it.",
            FormatOperation.Shorten);

        var prompt = adapter.BuildPrompt(request);

        StringAssert.Contains(prompt, "Meaning is invariant");
        StringAssert.Contains(prompt, "Remove repetition and low-value phrasing");
        StringAssert.Contains(prompt, "relative-time phrases");
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
