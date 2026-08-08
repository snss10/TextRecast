using System.IO;
using System.Net.Http;
using TextRecast.Deployment.Setup;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Setup;

internal sealed class GuidedModelSetupService : IGuidedModelSetupService, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly IReadOnlyList<SlmModelProfile> profiles;
    private readonly SlmModelInstallationSet installations;
    private readonly ModelSetupStateStore stateStore;
    private readonly ModelSelectionSettingsStore settingsStore;
    private readonly HardwareInspector hardwareInspector;
    private bool disposed;

    public GuidedModelSetupService()
    {
        profiles = SlmModelCatalog.All;
        httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The local application-data directory is unavailable.");
        }

        var modelDirectory = Path.Combine(localApplicationData, "TextRecast", "Models");
        installations = new SlmModelInstallationSet(
            profiles,
            httpClient,
            Path.Combine(AppContext.BaseDirectory, "Models"),
            modelDirectory);
        stateStore = new ModelSetupStateStore(profiles);
        settingsStore = new ModelSelectionSettingsStore(profiles.Select(profile => profile.Id));
        hardwareInspector = new HardwareInspector();
        ModelDirectory = modelDirectory;
    }

    internal string ModelDirectory { get; }

    public async Task<SetupPreparation> PrepareAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        HardwareProfile? hardware = null;
        string? notice = null;
        try
        {
            hardware = hardwareInspector.Inspect(ModelDirectory);
        }
        catch (HardwareInspectionException exception)
        {
            notice = exception.Message;
        }

        var candidates = installations.FindModelCandidatesByExpectedSize();
        var planned = SlmModelSetupPlanner.CreateChoices(
            profiles,
            hardware,
            candidates.Keys);
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var choices = SetupModelCatalogPresentation.CreateChoices(planned);
        return new SetupPreparation(choices, state.PendingSetup?.ModelId, notice);
    }

    public Task<bool> IsVerifiedAsync(string modelId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return VerifyAsync(modelId, cancellationToken);
    }

    public async Task BeginPendingAsync(
        string modelId,
        string applicationVersion,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (state.PendingSetup?.ModelId.Equals(modelId, StringComparison.Ordinal) == true)
        {
            return;
        }

        state = state.BeginPendingSetup(
            modelId,
            applicationVersion,
            DateTimeOffset.UtcNow);
        await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
    }

    public async Task CompleteSelectedModelAsync(
        string modelId,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var profile = profiles.Single(candidate =>
            candidate.Id.Equals(modelId, StringComparison.Ordinal));
        var installer = installations.GetInstaller(modelId);
        var verifiedPath = await installer
            .FindVerifiedInstalledModelAsync(cancellationToken)
            .ConfigureAwait(false);

        if (verifiedPath is null)
        {
            await AdvancePendingAsync(
                PendingModelSetupStage.DownloadingModel,
                cancellationToken).ConfigureAwait(false);
            var downloadProgress = new InlineProgress<SlmModelDownloadProgress>(value =>
                progress?.Report(MapProgress(value)));
            await installer.DownloadAsync(downloadProgress, cancellationToken)
                .ConfigureAwait(false);
            await AdvancePendingAsync(
                PendingModelSetupStage.VerifyingModel,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new SetupProgress(SetupProgressStage.VerifyingModel));
        }
        else
        {
            await AdvancePendingAsync(
                PendingModelSetupStage.VerifyingModel,
                cancellationToken).ConfigureAwait(false);
            progress?.Report(new SetupProgress(SetupProgressStage.VerifyingModel));
        }

        // Both branches establish a verification proof in this operation:
        // existing files are hashed above; DownloadAsync checks exact size and SHA-256
        // before atomically moving the partial file into its active path.
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state = state.ActivateVerifiedModel(profile, DateTimeOffset.UtcNow);
        await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        await settingsStore.SaveAsync(
            new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = modelId
            },
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        httpClient.Dispose();
        disposed = true;
    }

    private async Task<bool> VerifyAsync(string modelId, CancellationToken cancellationToken)
    {
        return await installations.GetInstaller(modelId)
            .FindVerifiedInstalledModelAsync(cancellationToken)
            .ConfigureAwait(false) is not null;
    }

    private async Task AdvancePendingAsync(
        PendingModelSetupStage stage,
        CancellationToken cancellationToken)
    {
        var state = await stateStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        state = state.AdvancePendingSetup(stage, DateTimeOffset.UtcNow);
        await stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private static SetupProgress MapProgress(SlmModelDownloadProgress value)
    {
        var stage = value.Stage == SlmModelInstallationStage.Downloading
            ? SetupProgressStage.DownloadingModel
            : SetupProgressStage.VerifyingModel;
        return new SetupProgress(
            stage,
            value.BytesDownloaded,
            value.TotalBytes,
            value.BytesPerSecond,
            value.EstimatedTimeRemaining,
            value.IsResuming);
    }

}

internal static class SetupModelCatalogPresentation
{
    public static SetupModelChoice[] CreateChoices(
        IEnumerable<SlmModelSetupChoice> plannedChoices)
    {
        ArgumentNullException.ThrowIfNull(plannedChoices);
        return plannedChoices.Select(choice => new SetupModelChoice(
            choice.Profile.Id,
            GetInstallerName(choice.Profile),
            GetProfileName(choice.Profile.Role),
            FormatInstallerSize(choice.Profile.ExpectedFileSize),
            BuildDetails(choice.Profile),
            choice.IsCompatible,
            choice.CompatibilityText)).ToArray();
    }

    private static string GetInstallerName(SlmModelProfile profile)
    {
        return profile == SlmModelCatalog.Default
            ? "Qwen 2.5 1.5B"
            : profile.DisplayName;
    }

    private static string GetProfileName(SlmModelRole role) => role switch
    {
        SlmModelRole.Fast => "Fast/default",
        SlmModelRole.Balanced => "Balanced",
        SlmModelRole.Quality => "Best quality",
        SlmModelRole.Alternative => "Alternative",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static string FormatInstallerSize(long bytes)
    {
        const decimal bytesPerGibibyte = 1024m * 1024m * 1024m;
        return $"{bytes / bytesPerGibibyte:F2} GiB";
    }

    private static string BuildDetails(SlmModelProfile profile)
    {
        return $"{profile.Description}\n\n{profile.LimitationNotice}\n\n" +
            $"Source: {profile.SourceRepository}\nLicense: {profile.LicenseExpression}";
    }
}

internal sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    private readonly Action<T> report = report ?? throw new ArgumentNullException(nameof(report));

    public void Report(T value)
    {
        report(value);
    }
}
