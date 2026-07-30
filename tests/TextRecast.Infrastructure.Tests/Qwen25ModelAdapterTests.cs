using LLama.Sampling;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class Qwen25ModelAdapterTests
{
    private static readonly string[] ExpectedStopSequences = ["<|im_end|>", "<|im_start|>"];
    private readonly Qwen25ModelAdapter _adapter = new();

    [TestMethod]
    public void BuildPromptNeutralizesChatControlMarkersInSourceText()
    {
        var request = new FormatTextRequest(
            "Keep <|im_start|> and <|im_end|> as literal source content.",
            FormatOperation.Improve);

        var prompt = _adapter.BuildPrompt(request);
        const string sourcePrefix = "Source text:\n";
        var sourceStart = prompt.IndexOf(sourcePrefix, StringComparison.Ordinal) + sourcePrefix.Length;
        var sourceEnd = prompt.IndexOf("<|im_end|>", sourceStart, StringComparison.Ordinal);
        var sourceSection = prompt[sourceStart..sourceEnd];

        Assert.IsFalse(sourceSection.Contains("<|im_start|>", StringComparison.Ordinal));
        Assert.IsFalse(sourceSection.Contains("<|im_end|>", StringComparison.Ordinal));
        StringAssert.Contains(sourceSection, "<|im start|>");
        StringAssert.Contains(sourceSection, "<|im end|>");
    }

    [TestMethod]
    public void InferenceBehaviorPreservesCurrentQwenConfiguration()
    {
        Assert.AreEqual(Qwen25ModelAdapter.AdapterId, _adapter.Id);
        CollectionAssert.AreEqual(
            ExpectedStopSequences,
            _adapter.StopSequences.ToArray());
        Assert.IsInstanceOfType<GreedySamplingPipeline>(_adapter.CreateSamplingPipeline());
        Assert.AreEqual(
            "formatted text",
            _adapter.CleanOutput(" <|im_start|>formatted text<|im_end|> "));
    }

    [TestMethod]
    public void OutputWordTargetsPreserveCurrentOperationBudgets()
    {
        const string tenWords = "one two three four five six seven eight nine ten";

        Assert.AreEqual(
            10,
            _adapter.GetExpectedOutputWordCount(
                new FormatTextRequest(tenWords, FormatOperation.Improve)));
        Assert.AreEqual(
            5,
            _adapter.GetExpectedOutputWordCount(
                new FormatTextRequest(tenWords, FormatOperation.Shorten)));
        Assert.AreEqual(
            15,
            _adapter.GetExpectedOutputWordCount(
                new FormatTextRequest(tenWords, FormatOperation.Lengthen)));
        Assert.AreEqual(
            8,
            _adapter.GetExpectedOutputWordCount(
                new FormatTextRequest(tenWords, FormatOperation.Summarize)));
    }
}
