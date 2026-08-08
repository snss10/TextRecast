using System.ComponentModel;
using System.IO;
using System.Windows;

namespace TextRecast.Setup;

public partial class App : global::System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        SetupOptions options;
        var quietRequested = SetupCommandLine.IsQuietRequested(e.Args);
        try
        {
            options = SetupCommandLine.Parse(e.Args);
        }
        catch (ArgumentException exception)
        {
            if (!quietRequested)
            {
                MessageBox.Show(
                    exception.Message,
                    "TextRecast Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Shutdown(2);
            return;
        }

        var engine = new EmbeddedPackageEngine();
        if (options.Operation is SetupOperation.Interactive)
        {
            MainWindow = new MainWindow(engine);
            MainWindow.Show();
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            return;
        }

        _ = RunCommandAsync(engine, options);
    }

    private async Task RunCommandAsync(
        EmbeddedPackageEngine engine,
        SetupOptions options)
    {
        try
        {
            var installDirectory = options.InstallDirectory ??
                engine.ResolveInstalledDirectory();
            var result = options.Operation switch
            {
                SetupOperation.Install => await engine.InstallAsync(installDirectory),
                SetupOperation.Uninstall => await engine.UninstallAsync(installDirectory),
                _ => throw new InvalidOperationException("A setup operation was not selected.")
            };

            Environment.ExitCode = result.ExitCode;
            if (!options.Quiet && !result.Succeeded)
            {
                MessageBox.Show(
                    result.Message,
                    "TextRecast Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception exception) when (exception is
            Win32Exception or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            Environment.ExitCode = 1;
            if (!options.Quiet)
            {
                MessageBox.Show(
                    exception.Message,
                    "TextRecast Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        finally
        {
            Shutdown(Environment.ExitCode);
        }
    }
}
