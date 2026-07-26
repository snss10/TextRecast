using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Windows;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Presentation;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF owns the window lifecycle; the token source is disposed when the window closes.")]
public partial class ModelDownloadWindow : Window
{
    private readonly SlmModelInstaller _installer;
    private CancellationTokenSource? _downloadCancellation;
    private bool _downloadStarted;
    private bool _downloadSucceeded;
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
        _downloadCancellation?.Dispose();
        _downloadCancellation = new CancellationTokenSource();
        RetryButton.Visibility = Visibility.Collapsed;
        CancelButton.Content = "Cancel";
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = 0;
        StatusTextBlock.Text = "Downloading local model...";
        DetailsTextBlock.Text = "Keep TextRecast open while the model downloads.";

        var progress = new Progress<SlmModelDownloadProgress>(UpdateProgress);
        try
        {
            InstalledModelPath = await _installer.DownloadAsync(
                progress,
                _downloadCancellation.Token);
            _downloadSucceeded = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            if (!_isClosing)
            {
                DialogResult = false;
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
    }

    private void UpdateProgress(SlmModelDownloadProgress progress)
    {
        if (progress.Percentage is double percentage)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = percentage;
            StatusTextBlock.Text = $"Downloading local model... {percentage:F0}%";
        }
        else
        {
            DownloadProgressBar.IsIndeterminate = true;
        }

        DetailsTextBlock.Text = progress.TotalBytes is long total
            ? $"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(total)}"
            : $"{FormatBytes(progress.BytesDownloaded)} downloaded";
    }

    private void ShowError(string message)
    {
        DownloadProgressBar.IsIndeterminate = false;
        StatusTextBlock.Text = "The model could not be downloaded.";
        DetailsTextBlock.Text = message;
        RetryButton.Visibility = Visibility.Visible;
        CancelButton.Content = "Close";
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        await StartDownloadAsync();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _downloadCancellation?.Cancel();
        if (CancelButton.Content?.ToString() == "Close")
        {
            DialogResult = false;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        if (!_downloadSucceeded)
        {
            _downloadCancellation?.Cancel();
        }

        _downloadCancellation?.Dispose();
    }

    private static string FormatBytes(long bytes)
    {
        const double bytesPerMegabyte = 1024 * 1024;
        return $"{bytes / bytesPerMegabyte:N1} MB";
    }
}
