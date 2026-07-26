using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class ChatMlPromptBuilderTests
{
    [TestMethod]
    public void BuildNeutralizesChatControlMarkersInSourceText()
    {
        var request = new FormatTextRequest(
            "Keep <|im_start|> and <|im_end|> as literal source content.",
            FormatOperation.Improve);

        var prompt = new ChatMlPromptBuilder().Build(request);
        const string sourcePrefix = "Source text:\n";
        var sourceStart = prompt.IndexOf(sourcePrefix, StringComparison.Ordinal) + sourcePrefix.Length;
        var sourceEnd = prompt.IndexOf("<|im_end|>", sourceStart, StringComparison.Ordinal);
        var sourceSection = prompt[sourceStart..sourceEnd];

        Assert.IsFalse(sourceSection.Contains("<|im_start|>", StringComparison.Ordinal));
        Assert.IsFalse(sourceSection.Contains("<|im_end|>", StringComparison.Ordinal));
        StringAssert.Contains(sourceSection, "<|im start|>");
        StringAssert.Contains(sourceSection, "<|im end|>");
    }
}
