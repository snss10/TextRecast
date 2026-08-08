using TextRecast.Infrastructure.SLM;

namespace TextRecast.Setup;

internal enum GuidedSetupPage
{
    Welcome,
    Legal,
    Destination,
    ModelChoice,
    Ready,
    Installing,
    Complete
}

internal sealed record SetupModelChoice(
    string Id,
    string Name,
    string Profile,
    string Size,
    string Details,
    bool IsCompatible,
    string CompatibilityText)
{
    public string AccessibleDetails => $"{CompatibilityText}\n{Details}";
}

internal sealed record SetupPreparation(
    IReadOnlyList<SetupModelChoice> Models,
    string? PendingModelId,
    string? Notice = null);

internal sealed record SetupProgress(
    SetupProgressStage Stage,
    long BytesDownloaded = 0,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    TimeSpan? EstimatedTimeRemaining = null,
    bool IsResuming = false);

internal enum SetupProgressStage
{
    InstallingApplication,
    ApplicationInstalled,
    DownloadingModel,
    VerifyingModel,
    Complete
}

internal sealed record GuidedSetupResult(bool Succeeded, bool Cancelled, string Message)
{
    public static GuidedSetupResult Success(string message) => new(true, false, message);

    public static GuidedSetupResult Failure(string message) => new(false, false, message);

    public static GuidedSetupResult Incomplete(string message) => new(false, true, message);
}

internal interface IGuidedModelSetupService
{
    Task<SetupPreparation> PrepareAsync(CancellationToken cancellationToken);

    Task<bool> IsVerifiedAsync(string modelId, CancellationToken cancellationToken);

    Task BeginPendingAsync(
        string modelId,
        string applicationVersion,
        CancellationToken cancellationToken);

    Task CompleteSelectedModelAsync(
        string modelId,
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken);
}

internal sealed class GuidedSetupCoordinator
{
    public const string AcceptText =
        "I accept the Apache License 2.0 terms and acknowledge the privacy notice.";
    public const string DeclineText = "I do not accept and want to exit Setup.";

    private readonly IInstallerPackageEngine packageEngine;
    private readonly IGuidedModelSetupService modelService;
    private readonly string applicationVersion;

    public GuidedSetupCoordinator(
        IInstallerPackageEngine packageEngine,
        IGuidedModelSetupService modelService,
        string applicationVersion)
    {
        this.packageEngine = packageEngine ??
            throw new ArgumentNullException(nameof(packageEngine));
        this.modelService = modelService ??
            throw new ArgumentNullException(nameof(modelService));
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        this.applicationVersion = applicationVersion;
        Destination = packageEngine.ResolveInstalledDirectory();
    }

    public GuidedSetupPage CurrentPage { get; private set; } = GuidedSetupPage.Welcome;

    public IReadOnlyList<SetupModelChoice> Models { get; private set; } = [];

    public string Destination { get; private set; }

    public string? SelectedModelId { get; private set; }

    public bool HasAcceptedLegal { get; private set; }

    public bool IsExistingModelVerified { get; private set; }

    public bool IsRecovery { get; private set; }

    public string? PreparationNotice { get; private set; }

    public SetupModelChoice? SelectedModel => Models.SingleOrDefault(
        model => model.Id.Equals(SelectedModelId, StringComparison.Ordinal));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var preparation = await modelService.PrepareAsync(cancellationToken)
            .ConfigureAwait(false);
        Models = preparation.Models;
        PreparationNotice = preparation.Notice;

        if (preparation.PendingModelId is not null &&
            Models.Any(model =>
                model.IsCompatible &&
                model.Id.Equals(preparation.PendingModelId, StringComparison.Ordinal)))
        {
            SelectedModelId = preparation.PendingModelId;
            IsRecovery = true;
        }
    }

    public void AcceptLegal()
    {
        HasAcceptedLegal = true;
    }

    public void DeclineLegal()
    {
        HasAcceptedLegal = false;
    }

    public void SetDestination(string destination)
    {
        Destination = packageEngine.ValidateInstallDirectory(destination);
    }

    public void SelectModel(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        var model = Models.SingleOrDefault(candidate =>
            candidate.Id.Equals(modelId, StringComparison.Ordinal)) ??
            throw new ArgumentException("The selected model is not in the setup catalog.", nameof(modelId));
        if (!model.IsCompatible)
        {
            throw new InvalidOperationException(model.CompatibilityText);
        }

        SelectedModelId = model.Id;
        IsExistingModelVerified = false;
    }

    public async Task MoveNextAsync(CancellationToken cancellationToken = default)
    {
        switch (CurrentPage)
        {
            case GuidedSetupPage.Welcome:
                CurrentPage = GuidedSetupPage.Legal;
                break;
            case GuidedSetupPage.Legal when HasAcceptedLegal:
                CurrentPage = GuidedSetupPage.Destination;
                break;
            case GuidedSetupPage.Destination:
                Destination = packageEngine.ValidateInstallDirectory(Destination);
                CurrentPage = GuidedSetupPage.ModelChoice;
                break;
            case GuidedSetupPage.ModelChoice when SelectedModelId is not null:
                IsExistingModelVerified = await modelService
                    .IsVerifiedAsync(SelectedModelId, cancellationToken)
                    .ConfigureAwait(false);
                CurrentPage = GuidedSetupPage.Ready;
                break;
            default:
                throw new InvalidOperationException(
                    "Setup cannot continue from the current page until its required choice is complete.");
        }
    }

    public void MoveBack()
    {
        CurrentPage = CurrentPage switch
        {
            GuidedSetupPage.Legal => GuidedSetupPage.Welcome,
            GuidedSetupPage.Destination => GuidedSetupPage.Legal,
            GuidedSetupPage.ModelChoice => GuidedSetupPage.Destination,
            GuidedSetupPage.Ready => GuidedSetupPage.ModelChoice,
            _ => throw new InvalidOperationException("Setup cannot move back from this page.")
        };
    }

    public async Task<GuidedSetupResult> InstallAsync(
        IProgress<SetupProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (CurrentPage == GuidedSetupPage.Ready)
        {
            BeginInstallation();
        }

        if (CurrentPage != GuidedSetupPage.Installing || SelectedModelId is null)
        {
            throw new InvalidOperationException("Setup is not ready to install.");
        }

        progress?.Report(new SetupProgress(SetupProgressStage.InstallingApplication));
        var packageResult = await packageEngine.InstallAsync(Destination).ConfigureAwait(false);
        if (!packageResult.Succeeded)
        {
            CurrentPage = GuidedSetupPage.Ready;
            return GuidedSetupResult.Failure(packageResult.Message);
        }

        try
        {
            await modelService.BeginPendingAsync(
                SelectedModelId,
                applicationVersion,
                CancellationToken.None).ConfigureAwait(false);
            progress?.Report(new SetupProgress(SetupProgressStage.ApplicationInstalled));
            await modelService.CompleteSelectedModelAsync(
                SelectedModelId,
                progress,
                cancellationToken).ConfigureAwait(false);
            CurrentPage = GuidedSetupPage.Complete;
            progress?.Report(new SetupProgress(SetupProgressStage.Complete));
            return GuidedSetupResult.Success("TextRecast and the selected model are ready.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return GuidedSetupResult.Incomplete(
                "TextRecast is installed, but model setup is incomplete. Run Setup again to resume the download.");
        }
        catch
        {
            CurrentPage = GuidedSetupPage.Ready;
            throw;
        }
    }

    public void BeginInstallation()
    {
        if (CurrentPage != GuidedSetupPage.Ready || SelectedModelId is null)
        {
            throw new InvalidOperationException("Setup is not ready to install.");
        }

        CurrentPage = GuidedSetupPage.Installing;
    }
}
