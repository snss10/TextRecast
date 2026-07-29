using System.Diagnostics.CodeAnalysis;
using System.Security.Principal;
using System.Windows;
using TextRecast.App.Presentation;
using TextRecast.Core.Application;
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
    private const string SingleInstanceNamePrefix = @"Local\TextRecast-";
    private LocalSlmTextFormatter? _formatter;
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!TryAcquireSingleInstance())
        {
            Shutdown();
            return;
        }

        var modelProfile = SlmModelCatalog.Default;
        var modelInstaller = new SlmModelInstaller(modelProfile);
        var modelPath = modelInstaller.FindInstalledModel();
        if (modelPath is null)
        {
            var downloadWindow = new ModelDownloadWindow(modelInstaller);
            if (downloadWindow.ShowDialog() != true ||
                string.IsNullOrWhiteSpace(downloadWindow.InstalledModelPath))
            {
                Shutdown();
                return;
            }

            modelPath = downloadWindow.InstalledModelPath;
        }

        var selectionReader = new UiAutomationSelectionReader();
        var selectionCapture = new SelectionCaptureService(selectionReader);
        var replacement = new WindowsTextReplacementService(selectionReader);
        _formatter = new LocalSlmTextFormatter(new SlmModelOptions
        {
            Profile = modelProfile,
            ModelPath = modelPath
        });
        var workflow = new FormatTextWorkflow(selectionCapture, _formatter, replacement);

        MainWindow = new MainWindow(workflow);
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _formatter?.Dispose();
        ReleaseSingleInstance();
        base.OnExit(e);
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
