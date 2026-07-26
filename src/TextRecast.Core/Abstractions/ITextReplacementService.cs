using TextRecast.Core.Application.Results;
using TextRecast.Core.Models;

namespace TextRecast.Core.Abstractions;

public interface ITextReplacementService
{
    Task<TextReplacementResult> ReplaceAsync(
        SelectionContext selection,
        string replacementText,
        CancellationToken cancellationToken);
}
