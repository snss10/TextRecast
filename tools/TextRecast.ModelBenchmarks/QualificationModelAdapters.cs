using LLama.Sampling;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.ModelBenchmarks;

internal static class QualificationModelAdapters
{
    public static ISlmModelAdapter Resolve(string adapterId)
    {
        return adapterId switch
        {
            Qwen25ModelAdapter.AdapterId => new Qwen25ModelAdapter(),
            Qwen35QualificationAdapter.AdapterId => new Qwen35QualificationAdapter(),
            Phi4MiniQualificationAdapter.AdapterId => new Phi4MiniQualificationAdapter(),
            Ministral3QualificationAdapter.AdapterId => new Ministral3QualificationAdapter(),
            Granite41QualificationAdapter.AdapterId => new Granite41QualificationAdapter(),
            _ => throw new ArgumentException(
                $"Unknown qualification adapter '{adapterId}'.",
                nameof(adapterId))
        };
    }
}

internal abstract class QualificationModelAdapterBase : ISlmModelAdapter
{
    public abstract string Id { get; }

    public abstract IReadOnlyList<string> StopSequences { get; }

    public abstract string BuildPrompt(FormatTextRequest request);

    public virtual ISamplingPipeline CreateSamplingPipeline() => new GreedySamplingPipeline();

    public int GetExpectedOutputWordCount(FormatTextRequest request)
    {
        var inputWords = Qwen25ModelAdapter.CountWords(request.Text);
        return request.Operation switch
        {
            FormatOperation.Shorten => Qwen25ModelAdapter.GetShorterWordTarget(inputWords),
            FormatOperation.Lengthen => Qwen25ModelAdapter.GetLongerWordTarget(inputWords),
            FormatOperation.Summarize => Qwen25ModelAdapter.GetSummaryWordTarget(inputWords),
            _ => inputWords
        };
    }

    public abstract string CleanOutput(string output);

    protected static string BuildUserContent(FormatTextRequest request, string escapedSource)
    {
        return $"Task: {Qwen25ModelAdapter.BuildTask(request)}\n\nSource text:\n{escapedSource}";
    }

    protected static string RemoveTokens(string output, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            output = output.Replace(token, string.Empty, StringComparison.Ordinal);
        }

        return output.Trim();
    }

    protected static string EscapeTokens(string input, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            input = input.Replace(token, token.Replace('|', ' '), StringComparison.Ordinal);
        }

        return input;
    }
}

internal sealed class Qwen35QualificationAdapter : QualificationModelAdapterBase
{
    public const string AdapterId = "qwen3.5-chatml";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|im_end|>", "<|im_start|>"]);

    public override string Id => AdapterId;

    public override IReadOnlyList<string> StopSequences => Stops;

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(request.Text, "<|im_start|>", "<|im_end|>");
        var user = BuildUserContent(request, source);
        return $"<|im_start|>system\n{Qwen25ModelAdapter.SystemInstruction}<|im_end|>\n" +
               $"<|im_start|>user\n{user}<|im_end|>\n" +
               "<|im_start|>assistant\n<think>\n\n</think>\n\n";
    }

    public override ISamplingPipeline CreateSamplingPipeline()
    {
        return new DefaultSamplingPipeline
        {
            Temperature = 0.7f,
            TopP = 0.8f,
            TopK = 20,
            Seed = 42
        };
    }

    public override string CleanOutput(string output)
    {
        output = RemoveReasoning(output);
        return RemoveTokens(output, "<|im_end|>", "<|im_start|>");
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

internal sealed class Phi4MiniQualificationAdapter : QualificationModelAdapterBase
{
    public const string AdapterId = "phi4-mini-chat";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|end|>", "<|endoftext|>", "<|system|>", "<|user|>"]);

    public override string Id => AdapterId;

    public override IReadOnlyList<string> StopSequences => Stops;

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(
            request.Text,
            "<|system|>",
            "<|user|>",
            "<|assistant|>",
            "<|end|>");
        var user = BuildUserContent(request, source);
        return $"<|system|>{Qwen25ModelAdapter.SystemInstruction}<|end|>" +
               $"<|user|>{user}<|end|><|assistant|>";
    }

    public override string CleanOutput(string output)
    {
        return RemoveTokens(
            output,
            "<|end|>",
            "<|endoftext|>",
            "<|assistant|>",
            "<|user|>",
            "<|system|>");
    }
}

internal sealed class Ministral3QualificationAdapter : QualificationModelAdapterBase
{
    public const string AdapterId = "ministral3-instruct";
    private static readonly IReadOnlyList<string> Stops = Array.AsReadOnly(["</s>"]);

    public override string Id => AdapterId;

    public override IReadOnlyList<string> StopSequences => Stops;

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(
            request.Text,
            "[SYSTEM_PROMPT]",
            "[/SYSTEM_PROMPT]",
            "[INST]",
            "[/INST]");
        var user = BuildUserContent(request, source);
        return $"<s>[SYSTEM_PROMPT]{Qwen25ModelAdapter.SystemInstruction}[/SYSTEM_PROMPT]" +
               $"[INST]{user}[/INST]";
    }

    public override string CleanOutput(string output) => RemoveTokens(output, "</s>", "<s>");
}

internal sealed class Granite41QualificationAdapter : QualificationModelAdapterBase
{
    public const string AdapterId = "granite4.1-chat";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|end_of_text|>", "<|start_of_role|>"]);

    public override string Id => AdapterId;

    public override IReadOnlyList<string> StopSequences => Stops;

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(
            request.Text,
            "<|start_of_role|>",
            "<|end_of_role|>",
            "<|end_of_text|>");
        var user = BuildUserContent(request, source);
        return "<|start_of_role|>system<|end_of_role|>" +
               $"{Qwen25ModelAdapter.SystemInstruction}<|end_of_text|>\n" +
               "<|start_of_role|>user<|end_of_role|>" +
               $"{user}<|end_of_text|>\n" +
               "<|start_of_role|>assistant<|end_of_role|>";
    }

    public override string CleanOutput(string output)
    {
        return RemoveTokens(
            output,
            "<|end_of_text|>",
            "<|start_of_role|>",
            "<|end_of_role|>");
    }
}
