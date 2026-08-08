using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Windows;
using TextRecast.App.Presentation;
using TextRecast.Core.Application;
using TextRecast.Deployment.Setup;
using TextRecast.Infrastructure.Hardware;
using TextRecast.Infrastructure.SLM;
using TextRecast.Infrastructure.Windows.Replacement;
using TextRecast.Infrastructure.Windows.Selection;

namespace TextRecast.App;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF owns the application lifecycle; OnExit releases process-lifetime resources.")]
public partial class App : global::System.Windows.Application
{
    private const string VerifyInstallationArgument = "--verify-installation";
    private const string SingleInstanceNamePrefix = @"Local\TextRecast-";
    private LocalSlmTextFormatter? _formatter;
    private HttpClient? _modelDownloadClient;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (e.Args.Contains(VerifyInstallationArgument, StringComparer.OrdinalIgnoreCase))
        {
            Shutdown(VerifyInstallation());
            return;
        }

        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        _ = StartAsync();
    }

    private static int VerifyInstallation()
    {
        var requiredFiles = new[]
        {
            "LICENSE",
            "NOTICE",
            "PRIVACY.md",
            "THIRD-PARTY-NOTICES.md",
            "NSIS-LICENSE.txt",
            "DOTNET-LICENSE.txt",
            "DOTNET-THIRD-PARTY-NOTICES.txt",
            "WPF-LICENSE.txt"
        };

        if (requiredFiles.Any(file =>
                !File.Exists(Path.Combine(AppContext.BaseDirectory, file))))
        {
            return 1;
        }

        var excludedPayloadPatterns = new[]
        {
            "*.gguf",
            "*.partial",
            "*.partial.metadata.json"
        };

        return excludedPayloadPatterns.Any(pattern =>
            Directory.EnumerateFiles(
                AppContext.BaseDirectory,
                pattern,
                SearchOption.AllDirectories).Any())
            ? 1
            : 0;
    }

    private async Task StartAsync()
    {
        try
        {
            var profiles = SlmModelCatalog.All;
            var packagedModelDirectory = Path.Combine(AppContext.BaseDirectory, "Models");
            var userModelDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TextRecast",
                "Models");

            _modelDownloadClient = CreateModelDownloadClient();
            var installations = new SlmModelInstallationSet(
                profiles,
                _modelDownloadClient,
                packagedModelDirectory,
                userModelDirectory);
            var settingsStore = new ModelSelectionSettingsStore(profiles.Select(profile => profile.Id));
            var setupStateStore = new ModelSetupStateStore(profiles);
            var migrationResult = await new V02ModelSetupStateMigrator(
                    setupStateStore,
                    settingsStore,
                    installations,
                    profiles)
                .LoadOrMigrateWithResultAsync();
            var setupState = migrationResult.State;
            var candidatePaths = installations.FindModelCandidatesByExpectedSize();
            var activeModelId = setupState.ActiveModel?.ModelId;
            var activeModelPath = migrationResult.VerifiedLegacyModelPath;
            if (activeModelId is not null && activeModelPath is null)
            {
                activeModelPath = await installations
                    .GetInstaller(activeModelId)
                    .FindVerifiedInstalledModelAsync();
            }

            var choices = CreateModelChoices(profiles, candidatePaths.Keys, userModelDirectory);
            var selectedChoice = activeModelId is not null && activeModelPath is not null
                ? choices.FirstOrDefault(choice =>
                    choice.IsCompatible &&
                    choice.IsInstalled &&
                    choice.Profile.Id.Equals(activeModelId, StringComparison.Ordinal))
                : null;

            SlmModelProfile modelProfile;
            string modelPath;
            if (selectedChoice is not null)
            {
                modelProfile = selectedChoice.Profile;
                modelPath = activeModelPath!;
            }
            else
            {
                var initialModelId = choices.Any(choice =>
                    choice.IsCompatible &&
                    choice.Profile.Id.Equals(
                        setupState.ActiveModel?.ModelId,
                        StringComparison.Ordinal))
                    ? setupState.ActiveModel!.ModelId
                    : SlmModelCatalog.Default.Id;
                var selectionWindow = new ModelSelectionWindow(choices, initialModelId);
                if (selectionWindow.ShowDialog() != true || selectionWindow.SelectedProfile is null)
                {
                    Shutdown();
                    return;
                }

                modelProfile = selectionWindow.SelectedProfile;
                var modelInstaller = installations.GetInstaller(modelProfile.Id);
                modelPath = modelProfile.Id.Equals(activeModelId, StringComparison.Ordinal)
                    ? activeModelPath ?? string.Empty
                    : await modelInstaller.FindVerifiedInstalledModelAsync() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(modelPath))
                {
                    var downloadWindow = new ModelDownloadWindow(modelInstaller, modelProfile);
                    if (downloadWindow.ShowDialog() != true ||
                        string.IsNullOrWhiteSpace(downloadWindow.InstalledModelPath))
                    {
                        Shutdown();
                        return;
                    }

                    modelPath = downloadWindow.InstalledModelPath;
                }

                setupState = setupState.ActivateVerifiedModel(
                    modelProfile,
                    DateTimeOffset.UtcNow);
                await setupStateStore.SaveAsync(setupState);

                await settingsStore.SaveAsync(new ModelSelectionSettings
                {
                    Mode = ModelSelectionMode.Manual,
                    ActiveModelId = modelProfile.Id
                });
            }

            StartMainWindow(modelProfile, modelPath);
        }
        catch (Exception exception) when (exception is
            HardwareInspectionException or
            IOException or
            InvalidDataException or
            UnauthorizedAccessException or
            InvalidOperationException or
            ArgumentException)
        {
            MessageBox.Show(
                $"TextRecast could not complete model setup.\n\n{exception.Message}",
                "TextRecast setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void StartMainWindow(SlmModelProfile modelProfile, string modelPath)
    {
        var selectionReader = new UiAutomationSelectionReader();
        var selectionCapture = new SelectionCaptureService(selectionReader);
        var replacement = new WindowsTextReplacementService(selectionReader);
        _formatter = new LocalSlmTextFormatter(new SlmModelOptions
        {
            Profile = modelProfile,
            ModelPath = modelPath
        });
        var workflow = new FormatTextWorkflow(selectionCapture, _formatter, replacement);

        MainWindow = new MainWindow(workflow, modelProfile);
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    private static ModelSelectionChoice[] CreateModelChoices(
        IReadOnlyList<SlmModelProfile> profiles,
        IEnumerable<string> installedModelIds,
        string userModelDirectory)
    {
        var installedIds = new HashSet<string>(installedModelIds, StringComparer.Ordinal);
        HardwareProfile? hardware = null;
        try
        {
            hardware = new HardwareInspector().Inspect(userModelDirectory);
        }
        catch (HardwareInspectionException)
        {
        }

        return SlmModelSetupPlanner.CreateChoices(
                profiles,
                hardware,
                installedIds,
                SlmModelCatalog.Default.Id)
            .Select(choice => new ModelSelectionChoice(
                choice.Profile,
                choice.IsCompatible,
                choice.IsInstalled,
                choice.IsRecommended,
                choice.CompatibilityText))
            .ToArray();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _formatter?.Dispose();
        _modelDownloadClient?.Dispose();
        ReleaseSingleInstance();
        base.OnExit(e);
    }

    private static HttpClient CreateModelDownloadClient()
    {
        var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TextRecast/1.0");
        return client;
    }

    private bool TryAcquireSingleInstance()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User?.Value ?? Environment.UserName;
        var mutex = new Mutex(
            initiallyOwned: true,
            $"{SingleInstanceNamePrefix}{userSid}",
            out var createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            return false;
        }

        _singleInstanceMutex = mutex;
        _ownsSingleInstanceMutex = true;
        return true;
    }

    private void ReleaseSingleInstance()
    {
        if (_singleInstanceMutex is null)
        {
            return;
        }

        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex.ReleaseMutex();
            _ownsSingleInstanceMutex = false;
        }

        _singleInstanceMutex.Dispose();
        _singleInstanceMutex = null;
    }
}
