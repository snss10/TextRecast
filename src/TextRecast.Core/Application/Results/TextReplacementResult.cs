namespace TextRecast.Core.Application.Results;

public sealed record TextReplacementResult(bool Success, string Message, string? Warning = null)
{
    public static TextReplacementResult Ok(string? warning = null) =>
        new(true, "Replaced in the source application.", warning);

    public static TextReplacementResult Fail(string message, string? warning = null) =>
        new(false, message, warning);
}
