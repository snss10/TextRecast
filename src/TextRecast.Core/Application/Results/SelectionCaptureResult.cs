using TextRecast.Core.Models;

namespace TextRecast.Core.Application.Results;

public sealed record SelectionCaptureResult(bool Success, SelectionContext? Selection, string Message, string? Warning = null)
{
    public static SelectionCaptureResult Ok(SelectionContext selection, string? warning = null) =>
        new(true, selection, string.Empty, warning);

    public static SelectionCaptureResult Fail(string message) => new(false, null, message);
}
