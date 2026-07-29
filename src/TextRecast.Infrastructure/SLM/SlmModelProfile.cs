namespace TextRecast.Infrastructure.SLM;

public sealed record SlmModelProfile
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required Uri DownloadUri { get; init; }
    public required string ExpectedSha256 { get; init; }
    public required long ExpectedFileSize { get; init; }
    public uint ContextSize { get; init; } = 4096;
    public int MaxOutputTokens { get; init; } = 768;
}
