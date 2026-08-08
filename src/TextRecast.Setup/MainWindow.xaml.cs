using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TextRecast.Infrastructure.Hardware;

namespace TextRecast.Setup;

public partial class MainWindow : Window, IDisposable
{
    private readonly IInstallerPackageEngine engine;
    private readonly GuidedModelSetupService modelService;
    private readonly GuidedSetupCoordinator coordinator;
    private CancellationTokenSource? installCancellation;
    private bool canCancelModelDownload;
    private bool disposed;
    private bool isBusy;
    private bool setupIncomplete;

    internal MainWindow(IInstallerPackageEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        modelService = new GuidedModelSetupService();
        coordinator = new GuidedSetupCoordinator(
            engine,
            modelService,
            GetApplicationVersion());

        InitializeComponent();
        SetupTheme.Apply(this);
        InstallDirectoryTextBox.Text = ToDisplayPath(coordinator.Destination);
        WelcomeVersionTextBlock.Text =
            $"This will install TextRecast version {GetApplicationVersion()} on your computer.";
        LicensePreviewTextBox.Text = BuildLicensePreview();
        AcceptRadioButton.Content = GuidedSetupCoordinator.AcceptText;
        DeclineRadioButton.Content = GuidedSetupCoordinator.DeclineText;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        SetNavigationBusy(true);
        try
        {
            await coordinator.InitializeAsync();
            ModelChoicesItemsControl.ItemsSource = coordinator.Models;
            if (coordinator.IsRecovery)
            {
                RecoveryNoticeTextBlock.Text =
                    $"An interrupted setup for {coordinator.SelectedModel?.Name} was found. " +
                    "Your previous model choice will be resumed after you review the setup pages.";
                RecoveryNoticeTextBlock.Visibility = Visibility.Visible;
            }
            else if (coordinator.PreparationNotice is not null)
            {
                RecoveryNoticeTextBlock.Text = coordinator.PreparationNotice;
                RecoveryNoticeTextBlock.Visibility = Visibility.Visible;
            }
        }
        catch (Exception exception) when (IsExpectedSetupException(exception))
        {
            ShowError(exception.Message);
            Close();
            return;
        }
        finally
        {
            SetNavigationBusy(false);
        }

        RefreshPage();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (coordinator.CurrentPage == GuidedSetupPage.Complete)
        {
            FinishSetup();
            return;
        }

        if (setupIncomplete)
        {
            Close();
            return;
        }

        if (coordinator.CurrentPage == GuidedSetupPage.Ready)
        {
            await InstallAsync();
            return;
        }

        SetNavigationBusy(true);
        try
        {
            if (coordinator.CurrentPage == GuidedSetupPage.Destination)
            {
                coordinator.SetDestination(ExpandDisplayPath(InstallDirectoryTextBox.Text));
                DestinationErrorTextBlock.Text = string.Empty;
            }

            await coordinator.MoveNextAsync();
        }
        catch (Exception exception) when (IsExpectedSetupException(exception))
        {
            if (coordinator.CurrentPage == GuidedSetupPage.Destination)
            {
                DestinationErrorTextBlock.Text = exception.Message;
            }
            else
            {
                ShowError(exception.Message);
            }
        }
        finally
        {
            SetNavigationBusy(false);
            RefreshPage();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            coordinator.MoveBack();
            RefreshPage();
        }
        catch (InvalidOperationException exception)
        {
            ShowError(exception.Message);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (isBusy)
        {
            if (!canCancelModelDownload)
            {
                return;
            }

            RequestModelCancellation();
            return;
        }

        Close();
    }

    private void AcceptRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        coordinator.AcceptLegal();
        CancelButton.Content = "Cancel";
        RefreshNavigation();
    }

    private void DeclineRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        coordinator.DeclineLegal();
        CancelButton.Content = "Exit";
        RefreshNavigation();
    }

    private void ModelRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string modelId })
        {
            return;
        }

        coordinator.SelectModel(modelId);
        var selected = coordinator.SelectedModel!;
        ModelGuidanceTextBlock.Text = selected.Details.Replace("\n\n", "  ", StringComparison.Ordinal);
        RefreshNavigation();
    }

    private void ModelRadioButton_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string modelId } radioButton &&
            modelId.Equals(coordinator.SelectedModelId, StringComparison.Ordinal))
        {
            radioButton.IsChecked = true;
        }
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select the TextRecast installation folder",
            InitialDirectory = Directory.Exists(ExpandDisplayPath(InstallDirectoryTextBox.Text))
                ? ExpandDisplayPath(InstallDirectoryTextBox.Text)
                : Path.GetDirectoryName(ExpandDisplayPath(InstallDirectoryTextBox.Text)),
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            InstallDirectoryTextBox.Text = ToDisplayPath(dialog.FolderName);
            DestinationErrorTextBlock.Text = string.Empty;
        }
    }

    private void LicenseLink_Click(object sender, RoutedEventArgs e)
    {
        new LegalDocumentWindow(
            this,
            "Third-party notices",
            SetupLegalDocuments.ThirdPartyNotices).ShowDialog();
    }

    private void PrivacyLink_Click(object sender, RoutedEventArgs e)
    {
        new LegalDocumentWindow(
            this,
            "TextRecast privacy notice",
            SetupLegalDocuments.Privacy).ShowDialog();
    }

    private void LegalAndPrivacyLink_Click(object sender, RoutedEventArgs e)
    {
        var content = SetupLegalDocuments.Privacy + "\n\n" +
            SetupLegalDocuments.License + "\n\n" +
            SetupLegalDocuments.Notice + "\n\n" +
            SetupLegalDocuments.ThirdPartyNotices;
        new LegalDocumentWindow(this, "Legal & Privacy", content).ShowDialog();
    }

    private async Task InstallAsync()
    {
        installCancellation = new CancellationTokenSource();
        setupIncomplete = false;
        canCancelModelDownload = false;
        coordinator.BeginInstallation();
        SetNavigationBusy(true);
        RefreshPage();
        await Dispatcher.Yield(DispatcherPriority.Render);
        var progress = new Progress<SetupProgress>(UpdateProgress);
        try
        {
            var result = await coordinator.InstallAsync(
                progress,
                installCancellation.Token);
            if (result.Cancelled)
            {
                setupIncomplete = true;
                InstallStatusTextBlock.Text = result.Message;
                NextButton.Content = "Close";
                NextButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
            }
            else if (!result.Succeeded)
            {
                ShowError(result.Message);
            }
        }
        catch (Exception exception) when (IsExpectedSetupException(exception))
        {
            ShowError(exception.Message);
        }
        finally
        {
            installCancellation.Dispose();
            installCancellation = null;
            canCancelModelDownload = false;
            SetNavigationBusy(false);
            RefreshPage();
        }
    }

    private void UpdateProgress(SetupProgress progress)
    {
        canCancelModelDownload = progress.Stage is
            SetupProgressStage.ApplicationInstalled or
            SetupProgressStage.DownloadingModel or
            SetupProgressStage.VerifyingModel;
        CancelButton.IsEnabled = canCancelModelDownload;
        ResetStageIndicators();

        switch (progress.Stage)
        {
            case SetupProgressStage.InstallingApplication:
                ProgressTitleTextBlock.Text = "Installing application files...";
                DownloadProgressBar.IsIndeterminate = true;
                SetStageActive(ApplicationStageIndicator);
                break;
            case SetupProgressStage.ApplicationInstalled:
                ProgressTitleTextBlock.Text = $"Preparing {coordinator.SelectedModel?.Name}...";
                DownloadProgressBar.IsIndeterminate = true;
                SetStageComplete(ApplicationStageIndicator, ApplicationStageCheck);
                SetStageActive(DownloadStageIndicator);
                break;
            case SetupProgressStage.DownloadingModel:
                ProgressTitleTextBlock.Text =
                    $"Downloading {coordinator.SelectedModel?.Name}... {FormatPercentage(progress)}";
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = GetPercentage(progress);
                SetStageComplete(ApplicationStageIndicator, ApplicationStageCheck);
                SetStageActive(DownloadStageIndicator);
                TransferMetricsTextBlock.Text = FormatTransferMetrics(progress);
                break;
            case SetupProgressStage.VerifyingModel:
                ProgressTitleTextBlock.Text = "Verifying selected model...";
                DownloadProgressBar.IsIndeterminate = true;
                SetStageComplete(ApplicationStageIndicator, ApplicationStageCheck);
                SetStageComplete(DownloadStageIndicator, DownloadStageCheck);
                SetStageActive(VerifyStageIndicator);
                break;
            case SetupProgressStage.Complete:
                ProgressTitleTextBlock.Text = "Setup complete.";
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = 100;
                SetStageComplete(ApplicationStageIndicator, ApplicationStageCheck);
                SetStageComplete(DownloadStageIndicator, DownloadStageCheck);
                SetStageComplete(VerifyStageIndicator, VerifyStageCheck);
                SetStageComplete(CompleteStageIndicator, CompleteStageCheck);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(progress));
        }
    }

    private void ResetStageIndicators()
    {
        SetStagePending(ApplicationStageIndicator, ApplicationStageCheck);
        SetStagePending(DownloadStageIndicator, DownloadStageCheck);
        SetStagePending(VerifyStageIndicator, VerifyStageCheck);
        SetStagePending(CompleteStageIndicator, CompleteStageCheck);
    }

    private void SetStagePending(Border indicator, TextBlock check)
    {
        indicator.Background = Brushes.Transparent;
        indicator.BorderBrush = (Brush)FindResource("SecondaryTextBrush");
        check.Visibility = Visibility.Collapsed;
    }

    private void SetStageActive(Border indicator)
    {
        indicator.Background = Brushes.Transparent;
        indicator.BorderBrush = (Brush)FindResource("AccentBrush");
    }

    private void SetStageComplete(Border indicator, TextBlock check)
    {
        var progressBrush = (Brush)FindResource("ProgressBrush");
        indicator.Background = progressBrush;
        indicator.BorderBrush = progressBrush;
        check.Visibility = Visibility.Visible;
    }

    private void RefreshPage()
    {
        WelcomePage.Visibility = Visibility.Collapsed;
        WizardPage.Visibility = Visibility.Collapsed;
        CompletePage.Visibility = Visibility.Collapsed;
        LegalPage.Visibility = Visibility.Collapsed;
        DestinationPage.Visibility = Visibility.Collapsed;
        ModelChoicePage.Visibility = Visibility.Collapsed;
        ReadyPage.Visibility = Visibility.Collapsed;
        InstallingPage.Visibility = Visibility.Collapsed;

        switch (coordinator.CurrentPage)
        {
            case GuidedSetupPage.Welcome:
                WelcomePage.Visibility = Visibility.Visible;
                break;
            case GuidedSetupPage.Legal:
                ShowWizardPage(LegalPage, "License and privacy",
                    "Please review the following information before continuing.");
                break;
            case GuidedSetupPage.Destination:
                ShowWizardPage(DestinationPage, "Select destination location",
                    "Where should TextRecast be installed?");
                break;
            case GuidedSetupPage.ModelChoice:
                ShowWizardPage(ModelChoicePage, "Choose a local language model",
                    "Select the model TextRecast will download and use.");
                break;
            case GuidedSetupPage.Ready:
                PopulateReadyPage();
                ShowWizardPage(ReadyPage, "Ready to install",
                    "Setup is ready to install TextRecast on your computer.");
                break;
            case GuidedSetupPage.Installing:
                ShowWizardPage(InstallingPage, "Installing",
                    "Please wait while Setup installs TextRecast on your computer.");
                break;
            case GuidedSetupPage.Complete:
                CompleteModelTextBlock.Text = $"Selected model: {coordinator.SelectedModel?.Name}";
                CompletePage.Visibility = Visibility.Visible;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        RefreshNavigation();
    }

    private void ShowWizardPage(Grid page, string title, string subtitle)
    {
        WizardPage.Visibility = Visibility.Visible;
        page.Visibility = Visibility.Visible;
        PageTitleTextBlock.Text = title;
        PageSubtitleTextBlock.Text = subtitle;
    }

    private void PopulateReadyPage()
    {
        ReadyVersionTextBlock.Text = GetApplicationVersion();
        ReadyDestinationTextBlock.Text = ToDisplayPath(coordinator.Destination);
        ReadyModelTextBlock.Text = coordinator.SelectedModel?.Name ?? string.Empty;
        ReadyDownloadTextBlock.Text = coordinator.IsExistingModelVerified
            ? "Existing verified model - no download"
            : coordinator.SelectedModel?.Size;
    }

    private void RefreshNavigation()
    {
        var page = coordinator.CurrentPage;
        BackButton.Visibility = page is
            GuidedSetupPage.Welcome or
            GuidedSetupPage.Installing or
            GuidedSetupPage.Complete
            ? Visibility.Collapsed
            : Visibility.Visible;
        CancelButton.Visibility = page == GuidedSetupPage.Complete ||
                                  (page == GuidedSetupPage.Installing && setupIncomplete)
            ? Visibility.Collapsed
            : Visibility.Visible;
        NextButton.Visibility = page == GuidedSetupPage.Installing && !setupIncomplete
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (isBusy)
        {
            BackButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            CancelButton.IsEnabled = canCancelModelDownload;
            return;
        }

        BackButton.IsEnabled = true;
        CancelButton.IsEnabled = true;
        NextButton.Content = page switch
        {
            GuidedSetupPage.Ready => "Install",
            GuidedSetupPage.Complete => "Finish",
            GuidedSetupPage.Installing when setupIncomplete => "Close",
            _ => "Next >"
        };
        NextButton.IsEnabled = page switch
        {
            GuidedSetupPage.Legal => coordinator.HasAcceptedLegal,
            GuidedSetupPage.ModelChoice => coordinator.SelectedModelId is not null,
            GuidedSetupPage.Installing => setupIncomplete,
            _ => true
        };
        AutomationProperties.SetName(NextButton, NextButton.Content.ToString() ?? "Next");
    }

    private void SetNavigationBusy(bool busy)
    {
        isBusy = busy;
        BackButton.IsEnabled = !busy;
        NextButton.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy || canCancelModelDownload;
    }

    private void RequestModelCancellation()
    {
        if (installCancellation is null || installCancellation.IsCancellationRequested)
        {
            return;
        }

        installCancellation.Cancel();
        CancelButton.IsEnabled = false;
        InstallStatusTextBlock.Text = "Cancelling model setup safely...";
    }

    private void FinishSetup()
    {
        if (LaunchCheckBox.IsChecked == true)
        {
            var executablePath = engine.GetInstalledApplicationPath(coordinator.Destination);
            try
            {
                Process.Start(new ProcessStartInfo(executablePath)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(executablePath)
                });
            }
            catch (Exception exception) when (exception is Win32Exception or IOException)
            {
                ShowError($"TextRecast was installed, but it could not be launched. {exception.Message}");
                return;
            }
        }

        Close();
    }

    private static string GetApplicationVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.2.0";
    }

    private static string BuildLicensePreview()
    {
        const string summary =
            "Apache License 2.0\n" +
            "Copyright 2026 snss10\n" +
            "Licensed under the Apache License, Version 2.0 (the \"License\"); " +
            "you may not use this file except in compliance with the License.";
        return summary + "\n\n" + SetupLegalDocuments.License;
    }

    private static string ExpandDisplayPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Environment.ExpandEnvironmentVariables(path);
    }

    private static string ToDisplayPath(string path)
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localApplicationData) &&
            path.StartsWith(localApplicationData, StringComparison.OrdinalIgnoreCase))
        {
            return "%LOCALAPPDATA%" + path[localApplicationData.Length..];
        }

        return path;
    }

    private static double GetPercentage(SetupProgress progress)
    {
        return progress.TotalBytes > 0
            ? Math.Clamp((double)progress.BytesDownloaded / progress.TotalBytes.Value * 100, 0, 100)
            : 0;
    }

    private static string FormatPercentage(SetupProgress progress)
    {
        return progress.TotalBytes > 0
            ? $"{GetPercentage(progress):F0}%"
            : string.Empty;
    }

    private static string FormatTransferMetrics(SetupProgress progress)
    {
        var pieces = new List<string>();
        if (progress.TotalBytes is long total)
        {
            pieces.Add($"{FormatBytes(progress.BytesDownloaded)} of {FormatBytes(total)}");
        }

        if (progress.BytesPerSecond is double rate)
        {
            pieces.Add($"{FormatBytes((long)rate)}/s");
        }

        if (progress.EstimatedTimeRemaining is TimeSpan remaining)
        {
            if (remaining.TotalMinutes >= 1)
            {
                var minutes = Math.Ceiling(remaining.TotalMinutes);
                pieces.Add($"About {minutes:F0} {(minutes == 1 ? "minute" : "minutes")} remaining");
            }
            else
            {
                var seconds = Math.Max(1, Math.Ceiling(remaining.TotalSeconds));
                pieces.Add($"About {seconds:F0} {(seconds == 1 ? "second" : "seconds")} remaining");
            }
        }

        if (progress.IsResuming)
        {
            pieces.Add("Resumed");
        }

        return string.Join("  ·  ", pieces);
    }

    private static string FormatBytes(long bytes)
    {
        const double mib = 1024d * 1024;
        const double gib = 1024d * 1024 * 1024;
        return bytes >= gib
            ? string.Create(CultureInfo.InvariantCulture, $"{bytes / gib:F2} GiB")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / mib:F0} MiB");
    }

    private void ShowError(string message)
    {
        MessageBox.Show(
            this,
            message,
            "TextRecast Setup",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static bool IsExpectedSetupException(Exception exception)
    {
        return exception is
            Win32Exception or
            IOException or
            InvalidDataException or
            InvalidOperationException or
            UnauthorizedAccessException or
            ArgumentException or
            HttpRequestException or
            HardwareInspectionException;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        installCancellation?.Dispose();
        installCancellation = null;
        modelService.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (isBusy)
        {
            if (!canCancelModelDownload)
            {
                e.Cancel = true;
                return;
            }

            var decision = MessageBox.Show(
                this,
                "Cancel the model download? TextRecast will remain installed and Setup can resume the partial download later.",
                "TextRecast Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (decision == MessageBoxResult.Yes)
            {
                RequestModelCancellation();
            }

            e.Cancel = true;
            return;
        }

        Dispose();
        base.OnClosing(e);
    }
}
