using TextRecast.Core.Formatting;
using TextRecast.ModelBenchmarks;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class QualificationModelAdapterTests
{
    private static readonly FormatTextRequest Request = new(
        "Send the report before Friday.",
        FormatOperation.Improve);

    [TestMethod]
    public void Qwen35AdapterDisablesThinkingAndRemovesReasoningBlocks()
    {
        var adapter = new Qwen35QualificationAdapter();

        var prompt = adapter.BuildPrompt(Request);
        var output = adapter.CleanOutput("<think>private reasoning</think>Rewritten text.<|im_end|>");

        StringAssert.EndsWith(prompt, "<think>\n\n</think>\n\n");
        Assert.AreEqual("Rewritten text.", output);
    }

    [TestMethod]
    public void CandidateAdaptersApplyTheirDocumentedChatTemplates()
    {
        StringAssert.Contains(
            new Phi4MiniQualificationAdapter().BuildPrompt(Request),
            "<|assistant|>");
        StringAssert.Contains(
            new Ministral3QualificationAdapter().BuildPrompt(Request),
            "[INST]");
        StringAssert.Contains(
            new Granite41QualificationAdapter().BuildPrompt(Request),
            "<|start_of_role|>assistant<|end_of_role|>");
    }

    [TestMethod]
    public void ResolverRejectsUnknownQualificationAdapter()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => QualificationModelAdapters.Resolve("unknown"));
    }
}
