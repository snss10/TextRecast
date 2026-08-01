using LLama.Sampling;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public sealed class Granite41ModelAdapter : ISlmModelAdapter
{
    public const string AdapterId = "granite4.1-chat";
    public const string BalancedPromptProfileId = "granite41-3b-balanced-v3";
    public const string GreedySamplingProfileId = "greedy-v1";

    private const string SystemInstruction =
        "Perform the task on the source rather than describing the task. Preserve factual status, actors, exact role labels, recurrence, conditions, causes, negation, names, numbers, relative-time phrases, and deadline wording. Wording may change as requested, but facts may not. Keep unknown referents general, add no rationale, and return only the revised text.";

    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|end_of_text|>", "<|start_of_role|>"]);

    public Granite41ModelAdapter(SlmModelProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!profile.AdapterId.Equals(AdapterId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Model profile '{profile.Id}' does not use adapter '{AdapterId}'.",
                nameof(profile));
        }

        if (!profile.PromptProfileId.Equals(BalancedPromptProfileId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported Granite 4.1 prompt profile '{profile.PromptProfileId}'.",
                nameof(profile));
        }

        if (!profile.SamplingProfileId.Equals(GreedySamplingProfileId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Unsupported Granite 4.1 sampling profile '{profile.SamplingProfileId}'.",
                nameof(profile));
        }
    }

    public string Id => AdapterId;

    public IReadOnlyList<string> StopSequences => Stops;

    public string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeControlTokens(request.Text);
        var task = SlmPromptBuilder.BuildGranite41BalancedTask(request);
        var userContent = SlmPromptBuilder.BuildUserContent(task, source);
        return "<|start_of_role|>system<|end_of_role|>" +
               $"{SystemInstruction}<|end_of_text|>\n" +
               "<|start_of_role|>user<|end_of_role|>" +
               $"{userContent}<|end_of_text|>\n" +
               "<|start_of_role|>assistant<|end_of_role|>";
    }

    public ISamplingPipeline CreateSamplingPipeline() => new GreedySamplingPipeline();

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
            .Replace("<|end_of_text|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|start_of_role|>", string.Empty, StringComparison.Ordinal)
            .Replace("<|end_of_role|>", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string EscapeControlTokens(string text)
    {
        return text
            .Replace("<|start_of_role|>", "< start_of_role >", StringComparison.Ordinal)
            .Replace("<|end_of_role|>", "< end_of_role >", StringComparison.Ordinal)
            .Replace("<|end_of_text|>", "< end_of_text >", StringComparison.Ordinal);
    }
}
