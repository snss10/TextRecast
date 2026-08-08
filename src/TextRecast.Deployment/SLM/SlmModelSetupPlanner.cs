using TextRecast.Infrastructure.Hardware;

namespace TextRecast.Infrastructure.SLM;

public static class SlmModelSetupPlanner
{
    public static SlmModelSetupChoice[] CreateChoices(
        IReadOnlyList<SlmModelProfile> profiles,
        HardwareProfile? hardware,
        IEnumerable<string> installedModelIds,
        string fallbackModelId)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(installedModelIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackModelId);

        if (!profiles.Any(profile =>
            profile.Id.Equals(fallbackModelId, StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "The fallback model must be present in the supplied profiles.",
                nameof(fallbackModelId));
        }

        var installedIds = new HashSet<string>(installedModelIds, StringComparer.Ordinal);
        var recommendedId = hardware is null
            ? fallbackModelId
            : SlmModelRecommender.Recommend(hardware, profiles, installedIds)
                .RecommendedProfile?.Id ?? fallbackModelId;

        return profiles.Select(profile => CreateChoice(
            profile,
            hardware,
            installedIds.Contains(profile.Id),
            profile.Id.Equals(recommendedId, StringComparison.Ordinal)))
            .ToArray();
    }

    private static SlmModelSetupChoice CreateChoice(
        SlmModelProfile profile,
        HardwareProfile? hardware,
        bool isInstalled,
        bool isRecommended)
    {
        if (profile.Requirements is null)
        {
            var compatibility = hardware is null
                ? "Hardware details are unavailable; the established default remains selectable."
                : "Established default. Review every replacement in the source application.";
            return new SlmModelSetupChoice(
                profile,
                IsCompatible: true,
                isInstalled,
                isRecommended,
                compatibility);
        }

        if (hardware is null)
        {
            return new SlmModelSetupChoice(
                profile,
                IsCompatible: false,
                isInstalled,
                IsRecommended: false,
                "Unavailable because TextRecast could not inspect memory, CPU, and storage requirements.");
        }

        var assessment = SlmModelRecommender.Assess(hardware, profile, isInstalled);
        var compatibilityText = assessment.IsEligible
            ? "Compatible with the currently available memory, CPU, and model storage."
            : "Currently unavailable: " + string.Join(" ", assessment.RejectionReasons);
        return new SlmModelSetupChoice(
            profile,
            assessment.IsEligible,
            isInstalled,
            isRecommended && assessment.IsEligible,
            compatibilityText);
    }
}

public sealed record SlmModelSetupChoice(
    SlmModelProfile Profile,
    bool IsCompatible,
    bool IsInstalled,
    bool IsRecommended,
    string CompatibilityText);
