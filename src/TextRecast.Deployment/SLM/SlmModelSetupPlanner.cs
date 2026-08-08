using TextRecast.Infrastructure.Hardware;

namespace TextRecast.Infrastructure.SLM;

public static class SlmModelSetupPlanner
{
    public static SlmModelSetupChoice[] CreateChoices(
        IReadOnlyList<SlmModelProfile> profiles,
        HardwareProfile? hardware,
        IEnumerable<string> installedModelIds)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(installedModelIds);
        var installedIds = new HashSet<string>(installedModelIds, StringComparer.Ordinal);
        return profiles.Select(profile => CreateChoice(
            profile,
            hardware,
            installedIds.Contains(profile.Id)))
            .ToArray();
    }

    private static SlmModelSetupChoice CreateChoice(
        SlmModelProfile profile,
        HardwareProfile? hardware,
        bool isInstalled)
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
                compatibility);
        }

        if (hardware is null)
        {
            return new SlmModelSetupChoice(
                profile,
                IsCompatible: false,
                isInstalled,
                "Unavailable because TextRecast could not inspect memory, CPU, and storage requirements.");
        }

        var assessment = SlmModelCompatibilityEvaluator.Assess(
            hardware,
            profile,
            isInstalled);
        var compatibilityText = assessment.IsEligible
            ? "Compatible with the currently available memory, CPU, and model storage."
            : "Currently unavailable: " + string.Join(" ", assessment.RejectionReasons);
        return new SlmModelSetupChoice(
            profile,
            assessment.IsEligible,
            isInstalled,
            compatibilityText);
    }
}

public sealed record SlmModelSetupChoice(
    SlmModelProfile Profile,
    bool IsCompatible,
    bool IsInstalled,
    string CompatibilityText);
