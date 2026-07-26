using TextRecast.Core.Formatting;

namespace TextRecast.Infrastructure.SLM;

public interface ISlmPromptBuilder
{
    string Build(FormatTextRequest request);
}
