using System.Runtime.InteropServices;

namespace TextRecast.Infrastructure.SLM;

public sealed record SlmModelRequirements
{
    public required double QualityScore { get; init; }
    public required long PeakWorkingSetBytes { get; init; }
    public required double MeasuredTokensPerSecond { get; init; }
    public Architecture RequiredArchitecture { get; init; } = Architecture.X64;
    public bool RequiresAvx2 { get; init; } = true;
}
