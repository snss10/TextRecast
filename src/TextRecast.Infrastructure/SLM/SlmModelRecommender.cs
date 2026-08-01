using System.Globalization;
using TextRecast.Infrastructure.Hardware;

namespace TextRecast.Infrastructure.SLM;

public static class SlmModelRecommender
{
    public const double MinimumQualityScore = 8.0;
    public const double MinimumTokensPerSecond = 5.0;
    public const long MemoryReserveBytes = 512L * 1024 * 1024;
    public const long StorageReserveBytes = 512L * 1024 * 1024;
    public const double PeakMemoryReserveFactor = 1.30;

    public static SlmModelRecommendation Recommend(
        HardwareProfile hardware,
        IEnumerable<SlmModelProfile> profiles,
        IReadOnlySet<string>? installedModelIds = null)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(profiles);

        installedModelIds ??= new HashSet<string>(StringComparer.Ordinal);
        var assessments = profiles
            .Select(profile => Assess(
                hardware,
                profile,
                installedModelIds.Contains(profile.Id)))
            .ToArray();

        var selected = assessments
            .Where(assessment => assessment.IsEligible)
            .OrderByDescending(assessment => assessment.Profile.Requirements!.Tier)
            .ThenByDescending(assessment => assessment.Profile.Requirements!.QualityScore)
            .ThenByDescending(assessment => assessment.Profile.Requirements!.MeasuredTokensPerSecond)
            .ThenBy(assessment => assessment.Profile.ExpectedFileSize)
            .FirstOrDefault();

        if (selected is null)
        {
            var reason = assessments.Length == 0
                ? "No measured model profiles are available."
                : "No model safely meets the measured hardware, storage, and responsiveness requirements.";
            return new SlmModelRecommendation(null, reason, assessments);
        }

        var requirements = selected.Profile.Requirements!;
        var recommendationReason = string.Create(
            CultureInfo.InvariantCulture,
            $"{selected.Profile.Id} is the highest compatible {requirements.Tier} tier option (measured quality {requirements.QualityScore:F1}/10).");

        return new SlmModelRecommendation(selected.Profile, recommendationReason, assessments);
    }

    public static SlmModelAssessment Assess(
        HardwareProfile hardware,
        SlmModelProfile profile,
        bool modelInstalled = false)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        ArgumentNullException.ThrowIfNull(profile);

        var reasons = new List<string>();
        var requirements = profile.Requirements;
        if (requirements is null)
        {
            reasons.Add("This model has no verified benchmark requirements.");
            return CreateAssessment(profile, reasons);
        }

        if (!double.IsFinite(requirements.QualityScore) ||
            requirements.QualityScore < MinimumQualityScore ||
            requirements.QualityScore > 10)
        {
            reasons.Add($"Its measured quality does not meet the {MinimumQualityScore:F1}/10 acceptance threshold.");
        }

        if (requirements.PeakWorkingSetBytes <= 0)
        {
            reasons.Add("Its measured peak memory requirement is invalid.");
        }
        else
        {
            var requiredAvailableMemory = CalculateRequiredAvailableMemory(
                requirements.PeakWorkingSetBytes);
            if (hardware.AvailablePhysicalMemoryBytes < requiredAvailableMemory)
            {
                reasons.Add(
                    $"It needs {FormatBytes(requiredAvailableMemory)} of currently available memory, including the safety reserve.");
            }
        }

        if (!double.IsFinite(requirements.MeasuredTokensPerSecond) ||
            requirements.MeasuredTokensPerSecond < MinimumTokensPerSecond)
        {
            reasons.Add(
                $"Its measured speed is below the {MinimumTokensPerSecond:F1} tokens/second responsiveness threshold.");
        }

        if (hardware.ProcessArchitecture != requirements.RequiredArchitecture)
        {
            reasons.Add(
                $"It requires the {requirements.RequiredArchitecture} backend, but this process is {hardware.ProcessArchitecture}.");
        }

        if (requirements.RequiresAvx2 && !hardware.SupportsAvx2)
        {
            reasons.Add("It requires AVX2 CPU support.");
        }

        if (!modelInstalled)
        {
            if (profile.ExpectedFileSize <= 0)
            {
                reasons.Add("Its download size is invalid.");
            }
            else
            {
                var requiredStorage = checked(profile.ExpectedFileSize + StorageReserveBytes);
                if (hardware.AvailableModelStorageBytes < requiredStorage)
                {
                    reasons.Add(
                        $"It needs {FormatBytes(requiredStorage)} of free model storage, including the safety reserve.");
                }
            }
        }

        return CreateAssessment(profile, reasons);
    }

    internal static long CalculateRequiredAvailableMemory(long peakWorkingSetBytes)
    {
        if (peakWorkingSetBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(peakWorkingSetBytes),
                "Peak working-set memory must be positive.");
        }

        return checked((long)Math.Ceiling(peakWorkingSetBytes * PeakMemoryReserveFactor) + MemoryReserveBytes);
    }

    private static SlmModelAssessment CreateAssessment(
        SlmModelProfile profile,
        List<string> reasons)
    {
        var warning = reasons.Count == 0
            ? null
            : "Manual selection is not recommended: " + string.Join(" ", reasons);
        return new SlmModelAssessment(profile, reasons.Count == 0, reasons, warning);
    }

    private static string FormatBytes(long bytes)
    {
        const double bytesPerGibibyte = 1024d * 1024 * 1024;
        return string.Create(CultureInfo.InvariantCulture, $"{bytes / bytesPerGibibyte:F1} GiB");
    }
}

public sealed record SlmModelAssessment(
    SlmModelProfile Profile,
    bool IsEligible,
    IReadOnlyList<string> RejectionReasons,
    string? ManualOverrideWarning);

public sealed record SlmModelRecommendation(
    SlmModelProfile? RecommendedProfile,
    string Reason,
    IReadOnlyList<SlmModelAssessment> Assessments);
