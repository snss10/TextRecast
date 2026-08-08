using System.IO;

namespace TextRecast.Setup.Tests;

[TestClass]
public sealed class GuidedSetupCoordinatorTests
{
    private const string InstallDirectory = @"C:\Users\tester\AppData\Local\Programs\TextRecast";

    [TestMethod]
    public async Task FreshSetupRequiresAcceptanceAndAnExplicitModelSelection()
    {
        var package = new FakePackageEngine();
        var models = new FakeModelSetupService();
        var coordinator = CreateCoordinator(package, models);

        await coordinator.InitializeAsync();

        Assert.AreEqual(GuidedSetupPage.Welcome, coordinator.CurrentPage);
        Assert.IsNull(coordinator.SelectedModelId);
        Assert.AreEqual(4, coordinator.Models.Count);

        await coordinator.MoveNextAsync();
        Assert.AreEqual(GuidedSetupPage.Legal, coordinator.CurrentPage);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.MoveNextAsync());

        coordinator.AcceptLegal();
        await coordinator.MoveNextAsync();
        Assert.AreEqual(GuidedSetupPage.Destination, coordinator.CurrentPage);
        await coordinator.MoveNextAsync();
        Assert.AreEqual(GuidedSetupPage.ModelChoice, coordinator.CurrentPage);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.MoveNextAsync());

        Assert.AreEqual(0, package.InstallCount);
        Assert.AreEqual(0, models.CompleteRequests.Count);
    }

    [TestMethod]
    public async Task DecliningTermsPreventsNavigationAndInstallation()
    {
        var package = new FakePackageEngine();
        var models = new FakeModelSetupService();
        var coordinator = CreateCoordinator(package, models);
        await coordinator.InitializeAsync();
        await coordinator.MoveNextAsync();

        coordinator.AcceptLegal();
        coordinator.DeclineLegal();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => coordinator.MoveNextAsync());
        Assert.AreEqual(0, package.InstallCount);
        Assert.AreEqual(0, models.CompleteRequests.Count);
    }

    [TestMethod]
    public async Task BackNavigationPreservesChoicesWithoutPerformingWork()
    {
        var package = new FakePackageEngine();
        var models = new FakeModelSetupService();
        var coordinator = CreateCoordinator(package, models);
        await NavigateToModelChoiceAsync(coordinator);
        coordinator.SelectModel("balanced");

        coordinator.MoveBack();
        Assert.AreEqual(GuidedSetupPage.Destination, coordinator.CurrentPage);
        coordinator.MoveBack();
        Assert.AreEqual(GuidedSetupPage.Legal, coordinator.CurrentPage);
        coordinator.MoveBack();
        Assert.AreEqual(GuidedSetupPage.Welcome, coordinator.CurrentPage);
        Assert.AreEqual("balanced", coordinator.SelectedModelId);
        Assert.AreEqual(0, package.InstallCount);
        Assert.AreEqual(0, models.CompleteRequests.Count);
    }

    [TestMethod]
    public async Task InvalidDestinationDoesNotAdvanceOrChangeTheComputer()
    {
        var package = new FakePackageEngine { RejectDestination = true };
        var coordinator = CreateCoordinator(package, new FakeModelSetupService());
        await coordinator.InitializeAsync();
        await coordinator.MoveNextAsync();
        coordinator.AcceptLegal();
        await coordinator.MoveNextAsync();

        Assert.ThrowsExactly<ArgumentException>(() =>
            coordinator.SetDestination(@"C:\Windows\TextRecast"));
        await Assert.ThrowsExactlyAsync<ArgumentException>(
            () => coordinator.MoveNextAsync());
        Assert.AreEqual(GuidedSetupPage.Destination, coordinator.CurrentPage);
        Assert.AreEqual(0, package.InstallCount);
    }

    [TestMethod]
    public async Task OnlySelectedModelIsRequestedAfterApplicationInstall()
    {
        var package = new FakePackageEngine();
        var models = new FakeModelSetupService();
        var coordinator = CreateCoordinator(package, models);
        await NavigateToReadyAsync(coordinator, "quality");

        Assert.AreEqual(0, package.InstallCount);
        Assert.AreEqual(0, models.BeginRequests.Count);
        Assert.AreEqual(0, models.CompleteRequests.Count);

        var result = await coordinator.InstallAsync(progress: null, CancellationToken.None);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(GuidedSetupPage.Complete, coordinator.CurrentPage);
        Assert.AreEqual(1, package.InstallCount);
        Assert.AreEqual("quality", models.BeginRequests.Single());
        Assert.AreEqual("quality", models.CompleteRequests.Single());
    }

    [TestMethod]
    public async Task BeginInstallationShowsProgressBeforePackageWorkStarts()
    {
        var package = new FakePackageEngine();
        var coordinator = CreateCoordinator(package, new FakeModelSetupService());
        await NavigateToReadyAsync(coordinator, "balanced");

        coordinator.BeginInstallation();

        Assert.AreEqual(GuidedSetupPage.Installing, coordinator.CurrentPage);
        Assert.AreEqual(0, package.InstallCount);
    }

    [TestMethod]
    public async Task PackageFailureNeverStartsOrActivatesModelSetup()
    {
        var package = new FakePackageEngine
        {
            InstallResult = new PackageOperationResult(1, "Package failed.")
        };
        var models = new FakeModelSetupService();
        var coordinator = CreateCoordinator(package, models);
        await NavigateToReadyAsync(coordinator, "fast");

        var result = await coordinator.InstallAsync(progress: null, CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsFalse(result.Cancelled);
        Assert.AreEqual(GuidedSetupPage.Ready, coordinator.CurrentPage);
        Assert.AreEqual(0, models.BeginRequests.Count);
        Assert.AreEqual(0, models.CompleteRequests.Count);
    }

    [TestMethod]
    public async Task CancelledModelDownloadLeavesPendingSetupAndReportsIncomplete()
    {
        var package = new FakePackageEngine();
        var models = new FakeModelSetupService { CancelCompletion = true };
        var coordinator = CreateCoordinator(package, models);
        await NavigateToReadyAsync(coordinator, "alternative");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await coordinator.InstallAsync(progress: null, cancellation.Token);

        Assert.IsTrue(result.Cancelled);
        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(GuidedSetupPage.Installing, coordinator.CurrentPage);
        Assert.AreEqual(1, package.InstallCount);
        Assert.AreEqual("alternative", models.BeginRequests.Single());
        StringAssert.Contains(result.Message, "incomplete");
    }

    [TestMethod]
    public async Task VerificationFailureCannotReachCompletion()
    {
        var models = new FakeModelSetupService
        {
            CompletionException = new InvalidDataException("Hash mismatch.")
        };
        var coordinator = CreateCoordinator(new FakePackageEngine(), models);
        await NavigateToReadyAsync(coordinator, "balanced");

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            coordinator.InstallAsync(progress: null, CancellationToken.None));

        Assert.AreEqual(GuidedSetupPage.Ready, coordinator.CurrentPage);
    }

    [TestMethod]
    public async Task ExistingVerifiedModelIsRecognizedButRecheckedDuringInstall()
    {
        var models = new FakeModelSetupService { VerifiedModelId = "fast" };
        var coordinator = CreateCoordinator(new FakePackageEngine(), models);
        await NavigateToReadyAsync(coordinator, "fast");

        Assert.IsTrue(coordinator.IsExistingModelVerified);
        Assert.AreEqual(1, models.VerificationRequests.Count);

        await coordinator.InstallAsync(progress: null, CancellationToken.None);

        Assert.AreEqual("fast", models.CompleteRequests.Single());
    }

    [TestMethod]
    public async Task PendingSetupRestoresOnlyItsCompatiblePreviousSelection()
    {
        var models = new FakeModelSetupService { PendingModelId = "balanced" };
        var coordinator = CreateCoordinator(new FakePackageEngine(), models);

        await coordinator.InitializeAsync();

        Assert.IsTrue(coordinator.IsRecovery);
        Assert.AreEqual("balanced", coordinator.SelectedModelId);
    }

    [TestMethod]
    public async Task IncompatibleModelCannotBeSelected()
    {
        var models = new FakeModelSetupService { IncompatibleModelId = "quality" };
        var coordinator = CreateCoordinator(new FakePackageEngine(), models);
        await coordinator.InitializeAsync();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            coordinator.SelectModel("quality"));
        Assert.IsNull(coordinator.SelectedModelId);
    }

    private static GuidedSetupCoordinator CreateCoordinator(
        FakePackageEngine package,
        FakeModelSetupService models)
    {
        return new GuidedSetupCoordinator(package, models, "0.3.0");
    }

    private static async Task NavigateToModelChoiceAsync(GuidedSetupCoordinator coordinator)
    {
        await coordinator.InitializeAsync();
        await coordinator.MoveNextAsync();
        coordinator.AcceptLegal();
        await coordinator.MoveNextAsync();
        await coordinator.MoveNextAsync();
    }

    private static async Task NavigateToReadyAsync(
        GuidedSetupCoordinator coordinator,
        string modelId)
    {
        await NavigateToModelChoiceAsync(coordinator);
        coordinator.SelectModel(modelId);
        await coordinator.MoveNextAsync();
    }

    private sealed class FakePackageEngine : IInstallerPackageEngine
    {
        public string DefaultInstallDirectory => InstallDirectory;

        public int InstallCount { get; private set; }

        public bool RejectDestination { get; init; }

        public PackageOperationResult InstallResult { get; init; } =
            new(0, "Installed.");

        public string ResolveInstalledDirectory() => InstallDirectory;

        public bool IsInstalled(string installDirectory) => false;

        public string ValidateInstallDirectory(string installDirectory)
        {
            if (RejectDestination)
            {
                throw new ArgumentException("Invalid destination.", nameof(installDirectory));
            }

            return installDirectory;
        }

        public string GetInstalledApplicationPath(string installDirectory) =>
            Path.Combine(installDirectory, "Application", "TextRecast.exe");

        public Task<PackageOperationResult> InstallAsync(string installDirectory)
        {
            InstallCount++;
            return Task.FromResult(InstallResult);
        }

        public Task<PackageOperationResult> UninstallAsync(string installDirectory) =>
            Task.FromResult(new PackageOperationResult(0, "Removed."));
    }

    private sealed class FakeModelSetupService : IGuidedModelSetupService
    {
        public List<string> BeginRequests { get; } = [];

        public List<string> CompleteRequests { get; } = [];

        public List<string> VerificationRequests { get; } = [];

        public string? PendingModelId { get; init; }

        public string? VerifiedModelId { get; init; }

        public string? IncompatibleModelId { get; init; }

        public bool CancelCompletion { get; init; }

        public Exception? CompletionException { get; init; }

        public Task<SetupPreparation> PrepareAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var models = new[]
            {
                CreateModel("fast", "Qwen 2.5 1.5B", "Fast/default", "1.04 GiB"),
                CreateModel("balanced", "Qwen 3.5 2B", "Balanced", "1.34 GiB"),
                CreateModel("quality", "Qwen 3.5 4B", "Best quality", "2.93 GiB"),
                CreateModel("alternative", "Granite 4.1 3B", "Alternative", "2.27 GiB")
            };
            return Task.FromResult(new SetupPreparation(models, PendingModelId));
        }

        public Task<bool> IsVerifiedAsync(
            string modelId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            VerificationRequests.Add(modelId);
            return Task.FromResult(modelId.Equals(VerifiedModelId, StringComparison.Ordinal));
        }

        public Task BeginPendingAsync(
            string modelId,
            string applicationVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BeginRequests.Add(modelId);
            return Task.CompletedTask;
        }

        public Task CompleteSelectedModelAsync(
            string modelId,
            IProgress<SetupProgress>? progress,
            CancellationToken cancellationToken)
        {
            CompleteRequests.Add(modelId);
            if (CancelCompletion)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (CompletionException is not null)
            {
                throw CompletionException;
            }

            progress?.Report(new SetupProgress(SetupProgressStage.Complete));
            return Task.CompletedTask;
        }

        private SetupModelChoice CreateModel(
            string id,
            string name,
            string profile,
            string size)
        {
            var compatible = !id.Equals(IncompatibleModelId, StringComparison.Ordinal);
            return new SetupModelChoice(
                id,
                name,
                profile,
                size,
                $"{name} details",
                compatible,
                compatible ? "Compatible." : "Not compatible with this computer.");
        }
    }
}
