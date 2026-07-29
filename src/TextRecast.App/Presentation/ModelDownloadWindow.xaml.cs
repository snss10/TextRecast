using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Presentation;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF owns the window lifecycle; each token source is disposed when its download attempt completes.")]
public partial class ModelDownloadWindow : Window
{
    private readonly SlmModelInstaller _installer;
    private CancellationTokenSource? _downloadCancellation;
    private bool _downloadStarted;
    private bool _downloadSucceeded;
    private bool _isDownloadInProgress;
    private bool _isClosing;

    public ModelDownloadWindow(SlmModelInstaller installer)
    {
        _installer = installer;
        InitializeComponent();
    }

    public string? InstalledModelPath { get; private set; }

    private async void Window_ContentRendered(object? sender, EventArgs e)
    {
        if (_downloadStarted)
        {
            return;
        }

        _downloadStarted = true;
        await StartDownloadAsync();
    }

    private async Task StartDownloadAsync()
    {
        if (_isDownloadInProgress || _isClosing)
        {
            return;
        }

        _isDownloadInProgress = true;
        using var downloadCancellation = new CancellationTokenSource();
        _downloadCancellation = downloadCancellation;
        InstalledModelPath = null;
        RetryButton.IsEnabled = false;
        RetryButton.Visibility = Visibility.Collapsed;
        CancelButton.Content = "Cancel";
        CancelButton.IsEnabled = true;
        AutomationProperties.SetName(CancelButton, "Cancel model download");
        DownloadProgressBar.IsIndeterminate = true;
        DownloadProgressBar.Value = 0;
        StatusTextBlock.Text = "Checking download status...";
        DetailsTextBlock.Text = "Looking for an earlier download that can be resumed.";

        var progress = new Progress<SlmModelDownloadProgress>(UpdateProgress);
        try
        {
            InstalledModelPath = await _installer.DownloadAsync(
                progress,
                downloadCancellation.Token);
            _downloadSucceeded = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                ShowCancelled();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
        catch (Exception)
        {
            ShowError("An unexpected error interrupted the download. Please try again.");
        }
        finally
        {
            if (ReferenceEquals(_downloadCancellation, downloadCancellation))
            {
                _downloadCancellation = null;
            }

            _isDownloadInProgress = false;
            if (!_isClosing)
            {
                RetryButton.IsEnabled = RetryButton.Visibility == Visibility.Visible;
            }
        }
    }

    private void UpdateProgress(SlmModelDownloadProgress progress)
    {
        if (progress.Stage is SlmModelInstallationStage.Verifying)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 100;
            StatusTextBlock.Text = "Verifying model integrity...";
            DetailsTextBlock.Text = "Checking the downloaded model before installation.";
            return;
        }

        if (progress.Stage is SlmModelInstallationStage.Installing)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 100;
            StatusTextBlock.Text = "Installing local model...";
            DetailsTextBlock.Text = "Finishing setup. This should only take a moment.";
            return;
        }

        if (progress.Percentage is double percentage)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = percentage;
            var action = progress.IsResuming
                ? "Resuming local model download"
                : progress.BytesDownloaded == 0
                    ? "Starting local model download"
                    : "Downloading local model";
            StatusTextBlock.Text = $"{action}... {percentage:F0}%";
        }
        else
        {
            DownloadProgressBar.IsIndeterminate = true;
            StatusTextBlock.Text = progress.IsResuming
                ? "Resuming local model download..."
                : "Starting local model download...";
        }

        var details = new List<string>
        {
            progress.TotalBytes is long total
                ? $"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(total)}"
                : $"{FormatBytes(progress.BytesDownloaded)} downloaded"
        };
        if (progress.BytesPerSecond is double bytesPerSecond && bytesPerSecond > 0)
        {
            details.Add($"{FormatBytes(bytesPerSecond)}/s");
        }

        if (progress.EstimatedTimeRemaining is TimeSpan remaining && remaining > TimeSpan.Zero)
        {
            details.Add($"{FormatDuration(remaining)} remaining");
        }

        DetailsTextBlock.Text = string.Join("  •  ", details);
    }

    private void ShowError(string message)
    {
        DownloadProgressBar.IsIndeterminate = false;
        StatusTextBlock.Text = "The model could not be downloaded.";
        DetailsTextBlock.Text = message;
        RetryButton.Visibility = Visibility.Visible;
        RetryButton.IsEnabled = true;
        CancelButton.Content = "Close";
        CancelButton.IsEnabled = true;
        AutomationProperties.SetName(CancelButton, "Close model setup");
    }

    private void ShowCancelled()
    {
        DownloadProgressBar.IsIndeterminate = false;
        StatusTextBlock.Text = "Model download paused.";
        DetailsTextBlock.Text = File.Exists(_installer.PartialModelPath)
            ? "Your progress was saved. Select Retry when you are ready to resume."
            : "No model data was lost. Select Retry when you are ready.";
        RetryButton.Visibility = Visibility.Visible;
        RetryButton.IsEnabled = true;
        CancelButton.Content = "Close";
        CancelButton.IsEnabled = true;
        AutomationProperties.SetName(CancelButton, "Close model setup");
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        await StartDownloadAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloadInProgress && _downloadCancellation is not null)
        {
            StatusTextBlock.Text = "Pausing model download...";
            DetailsTextBlock.Text = "Keeping the downloaded data so setup can resume later.";
            CancelButton.IsEnabled = false;
            _downloadCancellation.Cancel();
            return;
        }

        DialogResult = false;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        if (!_downloadSucceeded)
        {
            _downloadCancellation?.Cancel();
        }
    }

    private static string FormatBytes(double bytes)
    {
        const double bytesPerKilobyte = 1024;
        const double bytesPerMegabyte = bytesPerKilobyte * 1024;
        const double bytesPerGigabyte = bytesPerMegabyte * 1024;

        return bytes switch
        {
            >= bytesPerGigabyte => $"{bytes / bytesPerGigabyte:N2} GB",
            >= bytesPerMegabyte => $"{bytes / bytesPerMegabyte:N1} MB",
            >= bytesPerKilobyte => $"{bytes / bytesPerKilobyte:N0} KB",
            _ => $"{bytes:N0} B"
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }

        return $"{Math.Max((int)Math.Ceiling(duration.TotalSeconds), 1)}s";
    }
}
