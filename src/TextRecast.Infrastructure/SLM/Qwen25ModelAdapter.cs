using LLama.Sampling;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public sealed class Qwen25ModelAdapter : ISlmModelAdapter
{
    public const string AdapterId = SlmRuntimeProfileIds.Qwen25AdapterId;
    private static readonly IReadOnlyList<string> ChatMlStopSequences =
        Array.AsReadOnly(["<|im_end|>", "<|im_start|>"]);

    public string Id => AdapterId;

    public IReadOnlyList<string> StopSequences => ChatMlStopSequences;

    public string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeChatControlTokens(request.Text);
        var userContent = SlmPromptBuilder.BuildUserContent(request, source);
        return $"<|im_start|>system\n{SlmPromptBuilder.BuildSystemInstruction()}<|im_end|>\n" +
               $"<|im_start|>user\n{userContent}<|im_end|>\n<|im_start|>assistant\n";
    }

    public ISamplingPipeline CreateSamplingPipeline()
    {
        return new GreedySamplingPipeline();
    }

    public int GetOutputWordCapacity(FormatTextRequest request)
    {
        var inputWords = SlmPromptBuilder.CountWords(request.Text);
        return request.Operation == FormatOperation.Lengthen
            ? SlmPromptBuilder.GetExpandedWordCapacity(inputWords)
            : inputWords;
    }

    public string CleanOutput(string output)
    {
        return output
            .Replace("<|im_end|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|im_start|>", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string EscapeChatControlTokens(string text)
    {
        return text
            .Replace("<|im_start|>", "<|im start|>", StringComparison.Ordinal)
            .Replace("<|im_end|>", "<|im end|>", StringComparison.Ordinal);
    }

}
