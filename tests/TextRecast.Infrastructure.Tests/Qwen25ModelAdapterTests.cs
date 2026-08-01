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
    public void BuildPromptUsesConciseSharedContractExactlyOnce()
    {
        const string source = "Send the report before Friday.";
        var prompt = _adapter.BuildPrompt(
            new FormatTextRequest(source, FormatOperation.Improve));
        const string systemPrefix = "<|im_start|>system\n";
        var systemStart = prompt.IndexOf(systemPrefix, StringComparison.Ordinal) + systemPrefix.Length;
        var systemEnd = prompt.IndexOf("<|im_end|>", systemStart, StringComparison.Ordinal);

        Assert.AreEqual(
            SlmPromptBuilder.SharedSystemInstruction,
            prompt[systemStart..systemEnd]);
        Assert.AreEqual(1, CountOccurrences(prompt, SlmPromptBuilder.SharedSystemInstruction));
        Assert.IsGreaterThan(systemEnd, prompt.IndexOf(source, StringComparison.Ordinal));
    }

    [TestMethod]
    public void OperationPromptsDescribeIntentWithoutFixedOutputShapes()
    {
        const string source = "one two three four five six seven eight nine ten";

        var shorten = SlmPromptBuilder.BuildTask(
            new FormatTextRequest(source, FormatOperation.Shorten));
        var lengthen = SlmPromptBuilder.BuildTask(
            new FormatTextRequest(source, FormatOperation.Lengthen));
        var summarize = SlmPromptBuilder.BuildTask(
            new FormatTextRequest(source, FormatOperation.Summarize));

        StringAssert.Contains(shorten, "same message more concisely");
        StringAssert.Contains(lengthen, "same message more fully and explicitly");
        StringAssert.Contains(summarize, "essential meaning");
        StringAssert.Contains(summarize, "without commentary, labels");
        Assert.IsFalse(shorten.Any(char.IsDigit));
        Assert.IsFalse(lengthen.Any(char.IsDigit));
        Assert.IsFalse(summarize.Any(char.IsDigit));
        Assert.IsFalse(shorten.Contains("half", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(summarize.Contains("one sentence", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ChangeToneRequiresToneSelection()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => SlmPromptBuilder.BuildTask(
                new FormatTextRequest("Source", FormatOperation.ChangeTone)));
    }

    [TestMethod]
    public void EveryTonePromptDescribesIntentWithoutFixedOutputShape()
    {
        var prompts = Enum.GetValues<ToneStyle>()
            .Select(tone => SlmPromptBuilder.BuildTask(
                new FormatTextRequest("Source", FormatOperation.ChangeTone, tone)))
            .ToArray();

        Assert.HasCount(Enum.GetValues<ToneStyle>().Length, prompts);
        Assert.IsTrue(prompts.All(prompt => prompt.Contains("Rewrite", StringComparison.Ordinal)));
        Assert.IsTrue(prompts.All(prompt => !prompt.Any(char.IsDigit)));
        Assert.IsTrue(prompts.All(prompt =>
            !prompt.Contains("one sentence", StringComparison.OrdinalIgnoreCase) &&
            !prompt.Contains("half", StringComparison.OrdinalIgnoreCase) &&
            !prompt.Contains("word limit", StringComparison.OrdinalIgnoreCase)));
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
    public void OutputWordCapacityDoesNotForceCompressionOrSummaryLength()
    {
        const string tenWords = "one two three four five six seven eight nine ten";

        Assert.AreEqual(
            10,
            _adapter.GetOutputWordCapacity(
                new FormatTextRequest(tenWords, FormatOperation.Improve)));
        Assert.AreEqual(
            10,
            _adapter.GetOutputWordCapacity(
                new FormatTextRequest(tenWords, FormatOperation.Shorten)));
        Assert.AreEqual(
            15,
            _adapter.GetOutputWordCapacity(
                new FormatTextRequest(tenWords, FormatOperation.Lengthen)));
        Assert.AreEqual(
            10,
            _adapter.GetOutputWordCapacity(
                new FormatTextRequest(tenWords, FormatOperation.Summarize)));
    }

    private static int CountOccurrences(string value, string expected)
    {
        return value.Split(expected, StringSplitOptions.None).Length - 1;
    }
}
