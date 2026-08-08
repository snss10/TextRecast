using System.IO;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Setup;

public sealed record ModelSetupStateMigrationResult(
    ModelSetupState State,
    string? VerifiedLegacyModelPath);

public sealed class V02ModelSetupStateMigrator
{
    private readonly SlmModelInstallationSet installations;
    private readonly ModelSelectionSettingsStore legacySettingsStore;
    private readonly Dictionary<string, SlmModelProfile> profiles;
    private readonly ModelSetupStateStore stateStore;
    private readonly TimeProvider timeProvider;

    public V02ModelSetupStateMigrator(
        ModelSetupStateStore stateStore,
        ModelSelectionSettingsStore legacySettingsStore,
        SlmModelInstallationSet installations,
        IEnumerable<SlmModelProfile> profiles)
        : this(
            stateStore,
            legacySettingsStore,
            installations,
            profiles,
            TimeProvider.System)
    {
    }

    internal V02ModelSetupStateMigrator(
        ModelSetupStateStore stateStore,
        ModelSelectionSettingsStore legacySettingsStore,
        SlmModelInstallationSet installations,
        IEnumerable<SlmModelProfile> profiles,
        TimeProvider timeProvider)
    {
        this.stateStore = stateStore ??
            throw new ArgumentNullException(nameof(stateStore));
        this.legacySettingsStore = legacySettingsStore ??
            throw new ArgumentNullException(nameof(legacySettingsStore));
        this.installations = installations ??
            throw new ArgumentNullException(nameof(installations));
        ArgumentNullException.ThrowIfNull(profiles);
        this.profiles = profiles.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
        this.timeProvider = timeProvider ??
            throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ModelSetupState> LoadOrMigrateAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await LoadOrMigrateWithResultAsync(cancellationToken)
            .ConfigureAwait(false);
        return result.State;
    }

    public async Task<ModelSetupStateMigrationResult> LoadOrMigrateWithResultAsync(
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(stateStore.StatePath))
        {
            var existingState = await stateStore
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            return new ModelSetupStateMigrationResult(existingState, null);
        }

        var state = ModelSetupState.Default;
        string? verifiedLegacyModelPath = null;
        var legacySettings = await legacySettingsStore
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        if (legacySettings.ActiveModelId is string activeModelId &&
            profiles.TryGetValue(activeModelId, out var profile))
        {
            var verifiedPath = await installations
                .GetInstaller(activeModelId)
                .FindVerifiedInstalledModelAsync(cancellationToken)
                .ConfigureAwait(false);
            if (verifiedPath is not null)
            {
                verifiedLegacyModelPath = verifiedPath;
                state = state.ActivateVerifiedModel(
                    profile,
                    timeProvider.GetUtcNow());
            }
        }

        await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        return new ModelSetupStateMigrationResult(state, verifiedLegacyModelPath);
    }
}
