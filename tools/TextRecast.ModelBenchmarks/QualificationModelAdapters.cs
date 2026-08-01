using LLama.Sampling;
using TextRecast.Core.Formatting;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.ModelBenchmarks;

internal static class QualificationModelAdapters
{
    public static QualificationModelAdapterBase Resolve(
        string adapterId,
        QualificationPromptProfile promptProfile,
        string? samplingProfileId = null)
    {
        if (!promptProfile.AdapterId.Equals(adapterId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Prompt profile '{promptProfile.Id}' requires adapter '{promptProfile.AdapterId}'.",
                nameof(adapterId));
        }

        return adapterId switch
        {
            Qwen25ModelAdapter.AdapterId => ResolveGreedyOnly(
                new Qwen25QualificationAdapter(promptProfile),
                samplingProfileId),
            Qwen35QualificationAdapter.AdapterId => new Qwen35QualificationAdapter(
                promptProfile,
                samplingProfileId),
            Phi4MiniQualificationAdapter.AdapterId => ResolveGreedyOnly(
                new Phi4MiniQualificationAdapter(promptProfile),
                samplingProfileId),
            Ministral3QualificationAdapter.AdapterId => ResolveGreedyOnly(
                new Ministral3QualificationAdapter(promptProfile),
                samplingProfileId),
            Granite41QualificationAdapter.AdapterId => ResolveGreedyOnly(
                new Granite41QualificationAdapter(promptProfile),
                samplingProfileId),
            _ => throw new ArgumentException(
                $"Unknown qualification adapter '{adapterId}'.",
                nameof(adapterId))
        };
    }

    private static QualificationModelAdapterBase ResolveGreedyOnly(
        QualificationModelAdapterBase adapter,
        string? samplingProfileId)
    {
        if (samplingProfileId is not null &&
            !samplingProfileId.Equals(adapter.SamplingProfileId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Adapter '{adapter.Id}' supports only sampling profile '{adapter.SamplingProfileId}'.",
                nameof(samplingProfileId));
        }

        return adapter;
    }
}

internal abstract class QualificationModelAdapterBase : ISlmModelAdapter
{
    protected QualificationModelAdapterBase(QualificationPromptProfile promptProfile)
    {
        PromptProfile = promptProfile;
    }

    public abstract string Id { get; }

    public QualificationPromptProfile PromptProfile { get; }

    public abstract string ChatTemplateId { get; }

    public abstract string SamplingProfileId { get; }

    public virtual string SamplingPipelineId => nameof(GreedySamplingPipeline);

    public virtual uint? SamplingSeed => null;

    public virtual float? SamplingTemperature => null;

    public virtual float? SamplingTopP => null;

    public virtual int? SamplingTopK => null;

    public abstract IReadOnlyList<string> StopSequences { get; }

    public abstract string BuildPrompt(FormatTextRequest request);

    protected virtual string? ModelInstruction => null;

    public string EffectiveSystemInstruction =>
        PromptProfile.BuildSystemInstruction(ModelInstruction);

    public virtual ISamplingPipeline CreateSamplingPipeline() => new GreedySamplingPipeline();

    public int GetOutputWordCapacity(FormatTextRequest request)
    {
        var inputWords = SlmPromptBuilder.CountWords(request.Text);
        return request.Operation == FormatOperation.Lengthen
            ? SlmPromptBuilder.GetExpandedWordCapacity(inputWords)
            : inputWords;
    }

    public abstract string CleanOutput(string output);

    protected string BuildUserContent(FormatTextRequest request, string escapedSource)
    {
        return PromptProfile.BuildUserContent(request, escapedSource);
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

internal sealed class Qwen25QualificationAdapter(QualificationPromptProfile promptProfile)
    : QualificationModelAdapterBase(promptProfile)
{
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|im_end|>", "<|im_start|>"]);

    public override string Id => Qwen25ModelAdapter.AdapterId;

    public override string ChatTemplateId => "qwen-chatml-v1";

    public override string SamplingProfileId => "greedy-v1";

    public override IReadOnlyList<string> StopSequences => Stops;

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(request.Text, "<|im_start|>", "<|im_end|>");
        var user = BuildUserContent(request, source);
        return $"<|im_start|>system\n{EffectiveSystemInstruction}<|im_end|>\n" +
               $"<|im_start|>user\n{user}<|im_end|>\n<|im_start|>assistant\n";
    }

    public override string CleanOutput(string output)
    {
        return RemoveTokens(output, "<|im_end|>", "<|im_start|>");
    }
}

internal sealed class Qwen35QualificationAdapter : QualificationModelAdapterBase
{
    public const string AdapterId = "qwen3.5-chatml";
    public const string DefaultSamplingProfileId = "qwen3.5-default-v1";
    public const string GreedySamplingProfileId = "greedy-v1";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|im_end|>", "<|im_start|>"]);
    private readonly bool _usesGreedySampling;

    public Qwen35QualificationAdapter(
        QualificationPromptProfile promptProfile,
        string? samplingProfileId = null)
        : base(promptProfile)
    {
        _usesGreedySampling = samplingProfileId switch
        {
            null or DefaultSamplingProfileId => false,
            GreedySamplingProfileId => true,
            _ => throw new ArgumentException(
                $"Unsupported Qwen 3.5 sampling profile '{samplingProfileId}'.",
                nameof(samplingProfileId))
        };
    }

    public override string Id => AdapterId;

    public override string ChatTemplateId => "qwen3.5-chatml-nonthinking-v1";

    public override string SamplingProfileId => _usesGreedySampling
        ? GreedySamplingProfileId
        : DefaultSamplingProfileId;

    public override string SamplingPipelineId => _usesGreedySampling
        ? nameof(GreedySamplingPipeline)
        : nameof(DefaultSamplingPipeline);

    public override uint? SamplingSeed => _usesGreedySampling ? null : 42;

    public override float? SamplingTemperature => _usesGreedySampling ? null : 0.7f;

    public override float? SamplingTopP => _usesGreedySampling ? null : 0.8f;

    public override int? SamplingTopK => _usesGreedySampling ? null : 20;

    public override IReadOnlyList<string> StopSequences => Stops;

    protected override string ModelInstruction => "Do not explain or show reasoning.";

    public override string BuildPrompt(FormatTextRequest request)
    {
        var source = EscapeTokens(request.Text, "<|im_start|>", "<|im_end|>");
        var user = BuildUserContent(request, source);
        return $"<|im_start|>system\n{EffectiveSystemInstruction}<|im_end|>\n" +
               $"<|im_start|>user\n{user}<|im_end|>\n" +
               "<|im_start|>assistant\n<think>\n\n</think>\n\n";
    }

    public override ISamplingPipeline CreateSamplingPipeline()
    {
        if (_usesGreedySampling)
        {
            return new GreedySamplingPipeline();
        }

        return new DefaultSamplingPipeline
        {
            Temperature = SamplingTemperature ?? throw new InvalidOperationException(
                "The sampling temperature is required."),
            TopP = SamplingTopP ?? throw new InvalidOperationException("Top P is required."),
            TopK = SamplingTopK ?? throw new InvalidOperationException("Top K is required."),
            Seed = SamplingSeed ?? throw new InvalidOperationException("The sampling seed is required.")
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

internal sealed class Phi4MiniQualificationAdapter(QualificationPromptProfile promptProfile)
    : QualificationModelAdapterBase(promptProfile)
{
    public const string AdapterId = "phi4-mini-chat";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|end|>", "<|endoftext|>", "<|system|>", "<|user|>"]);

    public override string Id => AdapterId;

    public override string ChatTemplateId => "phi4-chat-v1";

    public override string SamplingProfileId => "greedy-v1";

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
        return $"<|system|>{EffectiveSystemInstruction}<|end|>" +
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

internal sealed class Ministral3QualificationAdapter(QualificationPromptProfile promptProfile)
    : QualificationModelAdapterBase(promptProfile)
{
    public const string AdapterId = "ministral3-instruct";
    private static readonly IReadOnlyList<string> Stops = Array.AsReadOnly(["</s>"]);

    public override string Id => AdapterId;

    public override string ChatTemplateId => "ministral-system-inst-v1";

    public override string SamplingProfileId => "greedy-v1";

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
        return $"<s>[SYSTEM_PROMPT]{EffectiveSystemInstruction}[/SYSTEM_PROMPT]" +
               $"[INST]{user}[/INST]";
    }

    public override string CleanOutput(string output) => RemoveTokens(output, "</s>", "<s>");
}

internal sealed class Granite41QualificationAdapter(QualificationPromptProfile promptProfile)
    : QualificationModelAdapterBase(promptProfile)
{
    public const string AdapterId = "granite4.1-chat";
    private static readonly IReadOnlyList<string> Stops =
        Array.AsReadOnly(["<|end_of_text|>", "<|start_of_role|>"]);

    public override string Id => AdapterId;

    public override string ChatTemplateId => "granite-role-v1";

    public override string SamplingProfileId => "greedy-v1";

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
               $"{EffectiveSystemInstruction}<|end_of_text|>\n" +
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
