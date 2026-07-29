namespace TextRecast.Infrastructure.SLM;

public sealed record SlmModelOptions
{
    public required SlmModelProfile Profile { get; init; }
    public required string ModelPath { get; init; }
    public int ThreadCount { get; init; } = Math.Clamp(Environment.ProcessorCount - 1, 1, 8);
}
