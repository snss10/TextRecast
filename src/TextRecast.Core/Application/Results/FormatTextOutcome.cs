namespace TextRecast.Core.Application.Results;

public sealed record FormatTextOutcome(bool Success, string GeneratedText, string Message, string? Warning = null);
