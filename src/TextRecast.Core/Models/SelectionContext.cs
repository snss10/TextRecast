namespace TextRecast.Core.Models;

public sealed record SelectionContext(
    string Text,
    IntPtr TargetWindow,
    uint TargetProcessId,
    DateTimeOffset CapturedAt);

public static class SelectionLimits
{
    public const int MaximumCharacters = 32_000;
}
