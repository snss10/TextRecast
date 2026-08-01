using LLama.Sampling;
using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public interface ISlmModelAdapter
{
    string Id { get; }

    IReadOnlyList<string> StopSequences { get; }

    string BuildPrompt(FormatTextRequest request);

    ISamplingPipeline CreateSamplingPipeline();

    int GetOutputWordCapacity(FormatTextRequest request);

    string CleanOutput(string output);
}
