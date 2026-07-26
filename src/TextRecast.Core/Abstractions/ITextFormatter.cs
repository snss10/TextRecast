using TextRecast.Core.Formatting;

namespace TextRecast.Core.Abstractions;

public interface ITextFormatter : IDisposable
{
    Task<string> FormatAsync(FormatTextRequest request, CancellationToken cancellationToken);
}
