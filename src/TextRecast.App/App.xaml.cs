using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Security.Principal;
using System.Windows;
using TextRecast.App.Presentation;
using TextRecast.Core.Application;
using TextRecast.Deployment.Setup;
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
        ApplicationTheme.Initialize();
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
            var activeModelId = setupState.ActiveModel?.ModelId;
            var activeModelPath = migrationResult.VerifiedLegacyModelPath;
            if (activeModelId is not null && activeModelPath is null)
            {
                activeModelPath = await installations
                    .GetInstaller(activeModelId)
                    .FindVerifiedInstalledModelAsync();
            }

            if (SetupRecoveryLauncher.IsRecoveryRequired(
                    setupState,
                    activeModelId,
                    activeModelPath))
            {
                if (!SetupRecoveryLauncher.TryLaunch(out var recoveryError))
                {
                    MessageBox.Show(
                        recoveryError,
                        "Complete TextRecast setup",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                Shutdown();
                return;
            }

            StartMainWindow(
                SlmModelCatalog.GetById(activeModelId!),
                activeModelPath!);
        }
        catch (Exception exception) when (exception is
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
