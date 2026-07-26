namespace TextRecast.Core.Formatting;

public sealed record FormatTextRequest(string Text, FormatOperation Operation, ToneStyle? Tone = null);
