using System.Diagnostics.CodeAnalysis;
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
    Justification = "WPF owns the application lifecycle; OnExit disposes the formatter.")]
public partial class App : global::System.Windows.Application
{
    private LocalSlmTextFormatter? _formatter;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var modelOptions = SlmModelCatalog.CreateDefault();
        var modelInstaller = new SlmModelInstaller(modelOptions);
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
        _formatter = new LocalSlmTextFormatter(modelOptions with { ModelPath = modelPath });
        var workflow = new FormatTextWorkflow(selectionCapture, _formatter, replacement);

        MainWindow = new MainWindow(workflow);
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _formatter?.Dispose();
        base.OnExit(e);
    }
}
