namespace TextRecast.Infrastructure.SLM;

public enum SlmModelRole
{
    Fast,
    Balanced,
    Quality,
    Alternative
}

public sealed record SlmModelProfile
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required SlmModelRole Role { get; init; }
    public required string Description { get; init; }
    public required string LanguageSupport { get; init; }
    public required string LimitationNotice { get; init; }
    public required bool IsExperimental { get; init; }
    public required string AdapterId { get; init; }
    public required string PromptProfileId { get; init; }
    public required string SamplingProfileId { get; init; }
    public required string FileName { get; init; }
    public required Uri DownloadUri { get; init; }
    public required string ExpectedSha256 { get; init; }
    public required long ExpectedFileSize { get; init; }
    public required string SourceRepository { get; init; }
    public required string SourceRevision { get; init; }
    public required string LicenseExpression { get; init; }
    public SlmModelRequirements? Requirements { get; init; }
    public uint ContextSize { get; init; } = 4096;
    public int MaxOutputTokens { get; init; } = 768;
}
