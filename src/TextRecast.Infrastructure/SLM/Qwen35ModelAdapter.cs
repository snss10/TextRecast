using LLama.Sampling;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public sealed class Qwen35ModelAdapter : ISlmModelAdapter
{
    public const string AdapterId = "qwen3.5-chatml";
    public const string BalancedPromptProfileId = "qwen35-2b-balanced-v3";
    public const string QualityPromptProfileId = "qwen35-4b-balanced-v3";
    public const string DefaultSamplingProfileId = "qwen3.5-default-v1";

    private const string BalancedSystemInstruction =
        "Carry out the task on the source. Change wording and detail only as the task calls for, while preserving real-world meaning. Do not turn facts into advice, success into failure, or suggestions into requirements. Retain actors, responsibility, causes, conditions, recurrence, negation, names, numbers, and deadline wording. Invent nothing and return only the edit. Do not explain or show reasoning.";
    private const string QualitySystemInstruction =
        "Apply the task to the complete source. Meaning is invariant, while wording and supporting detail may change as the task requires. Preserve actors, exact role labels, recurrence, status, conditions, chronology, causes, negation, names, numbers, relative-time phrases, and deadline wording. Do not infer a rationale or resolve ambiguity. Return only the rewrite. Do not explain or show reasoning.";

    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|im_end|>", "<|im_start|>"]);
    private readonly string _promptProfileId;

    public Qwen35ModelAdapter(SlmModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.AdapterId.Equals(AdapterId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Model profile '{profile.Id}' does not use adapter '{AdapterId}'.",
                nameof(profile));
        }

        if (profile.PromptProfileId is not (BalancedPromptProfileId or QualityPromptProfileId))
        {
            throw new ArgumentException(
                $"Unsupported Qwen 3.5 prompt profile '{profile.PromptProfileId}'.",
                nameof(profile));
        }

        if (!profile.SamplingProfileId.Equals(DefaultSamplingProfileId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported Qwen 3.5 sampling profile '{profile.SamplingProfileId}'.",
                nameof(profile));
        }

        _promptProfileId = profile.PromptProfileId;
    }

    public string Id => AdapterId;

    public IReadOnlyList<string> StopSequences => Stops;

    public string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeChatControlTokens(request.Text);
        var systemInstruction = _promptProfileId == BalancedPromptProfileId
            ? BalancedSystemInstruction
            : QualitySystemInstruction;
        var task = _promptProfileId == BalancedPromptProfileId
            ? SlmPromptBuilder.BuildQwen35SmallBalancedTask(request)
            : SlmPromptBuilder.BuildQwen35LargeBalancedTask(request);
        var userContent = SlmPromptBuilder.BuildUserContent(task, source);
        return $"<|im_start|>system\n{systemInstruction}<|im_end|>\n" +
               $"<|im_start|>user\n{userContent}<|im_end|>\n" +
               "<|im_start|>assistant\n<think>\n\n</think>\n\n";
    }

    public ISamplingPipeline CreateSamplingPipeline()
    {
        return new DefaultSamplingPipeline
        {
            Temperature = 0.7f,
            TopP = 0.8f,
            TopK = 20,
            Seed = 42
        };
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
        output = RemoveReasoning(output);
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

    private static string RemoveReasoning(string output)
    {
        const string start = "<think>";
        const string end = "</think>";
        while (true)
        {
            var startIndex = output.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                return output.Replace(end, string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            var endIndex = output.IndexOf(
                end,
                startIndex + start.Length,
                StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
            {
                return output[..startIndex];
            }

            output = output.Remove(startIndex, endIndex + end.Length - startIndex);
        }
    }
}
