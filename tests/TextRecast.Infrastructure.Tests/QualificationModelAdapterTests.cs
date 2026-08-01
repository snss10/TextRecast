using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;
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
    public void CandidateAdaptersShareContractAndTaskExactlyOnce()
    {
        ISlmModelAdapter[] adapters =
        [
            new Qwen25ModelAdapter(),
            new Qwen35QualificationAdapter(),
            new Phi4MiniQualificationAdapter(),
            new Ministral3QualificationAdapter(),
            new Granite41QualificationAdapter()
        ];
        var task = SlmPromptBuilder.BuildTask(Request);

        foreach (var adapter in adapters)
        {
            var prompt = adapter.BuildPrompt(Request);

            Assert.AreEqual(1, CountOccurrences(prompt, SlmPromptBuilder.SharedSystemInstruction));
            Assert.AreEqual(1, CountOccurrences(prompt, task));
        }
    }

    [TestMethod]
    public void OnlyReasoningFamilyAddsBehavioralInstruction()
    {
        var qwen35 = new Qwen35QualificationAdapter().BuildPrompt(Request);
        var phi = new Phi4MiniQualificationAdapter().BuildPrompt(Request);
        var ministral = new Ministral3QualificationAdapter().BuildPrompt(Request);
        var granite = new Granite41QualificationAdapter().BuildPrompt(Request);

        StringAssert.Contains(qwen35, "Do not explain or show reasoning.");
        Assert.IsFalse(phi.Contains("Do not explain", StringComparison.Ordinal));
        Assert.IsFalse(ministral.Contains("Do not explain", StringComparison.Ordinal));
        Assert.IsFalse(granite.Contains("Do not explain", StringComparison.Ordinal));
        Assert.IsFalse(phi.Contains("<think>", StringComparison.Ordinal));
        Assert.IsFalse(ministral.Contains("<think>", StringComparison.Ordinal));
        Assert.IsFalse(granite.Contains("<think>", StringComparison.Ordinal));
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

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
