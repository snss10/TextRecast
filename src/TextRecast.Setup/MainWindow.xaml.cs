using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Automation;

namespace TextRecast.Setup;

public partial class MainWindow : Window
{
    private readonly IInstallerPackageEngine engine;
    private bool isBusy;
    private bool isInstalled;

    internal MainWindow(IInstallerPackageEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        InitializeComponent();
        InstallDirectoryTextBox.Text = engine.ResolveInstalledDirectory();
        RefreshState();
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        try
        {
            var result = isInstalled
                ? await engine.UninstallAsync(InstallDirectoryTextBox.Text)
                : await engine.InstallAsync(InstallDirectoryTextBox.Text);
            StatusTextBlock.Text = result.Message;
            if (!result.Succeeded)
            {
                MessageBox.Show(
                    this,
                    result.Message,
                    "TextRecast Setup",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            RefreshState();
        }
        catch (Exception exception) when (exception is
            Win32Exception or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException or
            ArgumentException)
        {
            StatusTextBlock.Text = "Setup could not complete.";
            MessageBox.Show(
                this,
                exception.Message,
                "TextRecast Setup",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void RefreshState()
    {
        isInstalled = engine.IsInstalled(InstallDirectoryTextBox.Text);
        HeadingTextBlock.Text = isInstalled
            ? "TextRecast is installed"
            : "Install TextRecast";
        DescriptionTextBlock.Text = isInstalled
            ? "Remove the application files for the current user. Downloaded models and settings are preserved."
            : "Install the self-contained Windows application for the current user. No model is included in this package.";
        ActionButton.Content = isInstalled ? "Remove" : "Install";
        AutomationProperties.SetName(
            ActionButton,
            isInstalled ? "Remove TextRecast" : "Install TextRecast");
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        ActionButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        StatusTextBlock.Text = busy
            ? isInstalled
                ? "Removing application files..."
                : "Installing application files..."
            : StatusTextBlock.Text;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (isBusy)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
