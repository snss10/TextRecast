namespace TextRecast.Infrastructure.SLM;

public sealed record SlmModelOptions
{
    public required string ModelId { get; init; }
    public required string ModelFileName { get; init; }
    public required Uri DownloadUri { get; init; }
    public required string ModelPath { get; init; }
    public required string ExpectedModelSha256 { get; init; }
    public required long ExpectedModelFileSize { get; init; }
    public uint ContextSize { get; init; } = 4096;
    public int MaxOutputTokens { get; init; } = 768;
    public int ThreadCount { get; init; } = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
}
