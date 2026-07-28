using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MahApps.Metro.IconPacks;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Formatting;
using TextRecast.Core.Models;

namespace TextRecast.App.Presentation;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "WPF owns the window lifecycle; OnClosed cancels and disposes its token sources.")]
public partial class ResultWindow : Window
{
    private const int SuccessCloseDelayMilliseconds = 700;
    private static FormatOperation _lastOperation = FormatOperation.Improve;
    private static ToneStyle _lastTone = ToneStyle.Professional;
    private readonly Func<SelectionContext, FormatOperation, ToneStyle?, IProgress<FormatTextStage>?, CancellationToken, Task<FormatTextOutcome>> _applyAsync;
    private readonly Func<SelectionContext, string, CancellationToken, Task<TextReplacementResult>> _retryReplacementAsync;
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _successCloseCancellation;
    private SelectionContext? _selection;
    private string? _generatedText;
    private int _contentVersion;
    private bool _isClosed;

    internal ResultWindow(
        string displayText,
        SelectionContext? selection,
        Func<SelectionContext, FormatOperation, ToneStyle?, IProgress<FormatTextStage>?, CancellationToken, Task<FormatTextOutcome>> applyAsync,
        Func<SelectionContext, string, CancellationToken, Task<TextReplacementResult>> retryReplacementAsync)
    {
        InitializeComponent();
        _applyAsync = applyAsync;
        _retryReplacementAsync = retryReplacementAsync;
        SelectRememberedChoices();
        UpdateResult(displayText, selection);
    }

    internal void UpdateResult(string displayText, SelectionContext? selection)
    {
        if (_isClosed)
        {
            return;
        }

        _contentVersion++;
        CancelOperation();
        CancelSuccessClose();
        _selection = selection;
        _generatedText = null;
        ResultTextBox.Text = displayText;
        PreviewLabel.Text = selection is null ? "Capture" : "Selected text";
        SetApplyButtonMode(isRetry: false);
        ApplyButton.IsEnabled = selection is not null;
        SetBusy(false);
        SetStatus(
            selection is null
                ? "Select text and click TextRecast again"
                : $"Ready to format - {selection.Text.Length:N0} characters",
            StatusKind.Neutral);
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosed || _selection is null)
        {
            return;
        }

        var contentVersion = _contentVersion;
        var operationCancellation = new CancellationTokenSource();
        var formattingHintCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            operationCancellation.Token);
        Task? formattingHintTask = null;
        _operationCancellation = operationCancellation;
        SetBusy(true);

        try
        {
            if (_generatedText is not null)
            {
                SetStatus("Replacing selection...", StatusKind.Replacing);
                var retry = await _retryReplacementAsync(_selection, _generatedText, operationCancellation.Token);
                if (!IsCurrentContent(contentVersion))
                {
                    return;
                }

                await ShowReplacementResultAsync(retry);
                return;
            }

            var operation = GetSelectedOperation();
            ToneStyle? tone = operation == FormatOperation.ChangeTone ? GetSelectedTone() : null;
            formattingHintTask = ShowLongRunningFormattingHintsAsync(
                contentVersion,
                formattingHintCancellation.Token);
            var progress = new Progress<FormatTextStage>(stage =>
            {
                if (IsCurrentContent(contentVersion))
                {
                    if (stage == FormatTextStage.Replacing)
                    {
                        formattingHintCancellation.Cancel();
                    }

                    ShowStage(stage);
                }
            });
            var outcome = await _applyAsync(_selection, operation, tone, progress, operationCancellation.Token);
            formattingHintCancellation.Cancel();
            if (!IsCurrentContent(contentVersion))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(outcome.GeneratedText))
            {
                _generatedText = outcome.GeneratedText;
                ResultTextBox.Text = outcome.GeneratedText;
                PreviewLabel.Text = outcome.Success ? "Formatted text" : "Generated text";
            }

            if (outcome.Success)
            {
                await ShowSuccessfulReplacementAsync(outcome.Message, outcome.Warning);
            }
            else
            {
                SetStatus(BuildStatus(outcome.Message, outcome.Warning), StatusKind.Error);
                if (_generatedText is not null)
                {
                    SetApplyButtonMode(isRetry: true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Cancelled", StatusKind.Warning);
            }
        }
        catch (FileNotFoundException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Local model file is missing", StatusKind.Error);
            }
        }
        catch (InvalidDataException ex)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
        }
        catch (TextFormattingException ex)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus(ex.Message, StatusKind.Error);
            }
        }
        catch (OutOfMemoryException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Not enough memory to run the local model", StatusKind.Error);
            }
        }
        catch (DllNotFoundException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("The local model runtime is unavailable", StatusKind.Error);
            }
        }
        catch (BadImageFormatException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("The local model runtime is incompatible", StatusKind.Error);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Formatting failed with {ex.GetType().FullName}; HResult=0x{ex.HResult:X8}.");
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Formatting failed unexpectedly. Try a smaller selection.", StatusKind.Error);
            }
        }
        finally
        {
            formattingHintCancellation.Cancel();
            if (formattingHintTask is not null)
            {
                try
                {
                    await formattingHintTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            formattingHintCancellation.Dispose();
            operationCancellation.Dispose();
            if (ReferenceEquals(_operationCancellation, operationCancellation))
            {
                _operationCancellation = null;
                if (!_isClosed)
                {
                    SetBusy(false);
                }
            }
        }
    }

    private async Task ShowReplacementResultAsync(TextReplacementResult result)
    {
        if (result.Success)
        {
            await ShowSuccessfulReplacementAsync(result.Message, result.Warning);
            return;
        }

        SetStatus(BuildStatus(result.Message, result.Warning), StatusKind.Error);
    }

    private async Task ShowSuccessfulReplacementAsync(string message, string? warning)
    {
        _selection = null;
        SetBusy(false);

        if (!string.IsNullOrWhiteSpace(warning))
        {
            CancelSuccessClose();
            SetStatus(BuildStatus(message, warning), StatusKind.Warning);
            return;
        }

        SetStatus("Replaced successfully", StatusKind.Success);
        await CloseAfterSuccessAsync();
    }

    private static string BuildStatus(string message, string? warning)
    {
        return string.IsNullOrWhiteSpace(warning) ? message : $"{message} {warning}";
    }

    private FormatOperation GetSelectedOperation()
    {
        var tag = OperationChoices.Children
            .OfType<RadioButton>()
            .Single(button => button.IsChecked == true)
            .Tag?.ToString();
        return Enum.Parse<FormatOperation>(tag!, ignoreCase: false);
    }

    private ToneStyle GetSelectedTone()
    {
        var tag = ToneChoices.Children
            .OfType<RadioButton>()
            .Single(button => button.IsChecked == true)
            .Tag?.ToString();
        return Enum.Parse<ToneStyle>(tag!, ignoreCase: false);
    }

    private void Operation_Checked(object sender, RoutedEventArgs e)
    {
        if (TonePanel is null || sender is not RadioButton button)
        {
            return;
        }

        _lastOperation = Enum.Parse<FormatOperation>(button.Tag!.ToString()!, ignoreCase: false);
        TonePanel.Visibility = _lastOperation == FormatOperation.ChangeTone
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void SetBusy(bool isBusy)
    {
        if (_isClosed)
        {
            return;
        }

        var canChangeFormat = !isBusy && _generatedText is null;
        OperationChoices.IsEnabled = canChangeFormat;
        TonePanel.IsEnabled = canChangeFormat;
        CancelButton.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        ActivityProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        ApplyButton.IsEnabled = !isBusy && _selection is not null;

        if (isBusy)
        {
            StartBusyAnimation();
        }
        else
        {
            StopBusyAnimation();
        }
    }

    private void ShowStage(FormatTextStage stage)
    {
        if (stage == FormatTextStage.Formatting)
        {
            var characterCount = _selection?.Text.Length ?? 0;
            SetStatus(
                characterCount >= 800
                    ? $"Formatting {characterCount:N0} characters locally..."
                    : "Formatting locally...",
                StatusKind.Working);
            return;
        }

        SetStatus("Replacing selection...", StatusKind.Replacing);
    }

    private async Task ShowLongRunningFormattingHintsAsync(
        int contentVersion,
        CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), cancellationToken);
        if (IsCurrentContent(contentVersion))
        {
            SetStatus("Still formatting locally...", StatusKind.Working);
        }

        await Task.Delay(TimeSpan.FromSeconds(17), cancellationToken);
        if (IsCurrentContent(contentVersion))
        {
            SetStatus("Large selection still formatting - Cancel is available", StatusKind.Working);
        }
    }

    private void SetApplyButtonMode(bool isRetry)
    {
        ApplyButtonText.Text = isRetry ? "Retry replace" : "Apply";
        ApplyButtonIcon.Kind = isRetry
            ? PackIconLucideKind.RefreshCw
            : PackIconLucideKind.Check;
        AutomationProperties.SetName(ApplyButton, isRetry ? "Retry replacement" : "Apply");
    }

    private void SetStatus(string message, StatusKind kind)
    {
        if (_isClosed)
        {
            return;
        }

        StatusTextBlock.Text = message;
        StatusIcon.Kind = kind switch
        {
            StatusKind.Working => PackIconLucideKind.LoaderCircle,
            StatusKind.Replacing => PackIconLucideKind.Replace,
            StatusKind.Success => PackIconLucideKind.CircleCheck,
            StatusKind.Warning => PackIconLucideKind.CircleAlert,
            StatusKind.Error => PackIconLucideKind.CircleX,
            _ => PackIconLucideKind.Circle
        };
        // The footer progress bar is the single animated busy indicator.
        StatusIcon.Spin = false;
        StatusIcon.Foreground = kind switch
        {
            StatusKind.Working => (Brush)FindResource("AccentBrush"),
            StatusKind.Replacing => (Brush)FindResource("ReplacingBrush"),
            StatusKind.Success => (Brush)FindResource("SuccessBrush"),
            StatusKind.Warning => (Brush)FindResource("WarningBrush"),
            StatusKind.Error => (Brush)FindResource("ErrorBrush"),
            _ => (Brush)FindResource("SecondaryTextBrush")
        };
    }

    private void SelectRememberedChoices()
    {
        foreach (var button in OperationChoices.Children.OfType<RadioButton>())
        {
            button.IsChecked = button.Tag?.ToString() == _lastOperation.ToString();
        }

        foreach (var button in ToneChoices.Children.OfType<RadioButton>())
        {
            button.IsChecked = button.Tag?.ToString() == _lastTone.ToString();
            button.Checked += (_, _) =>
            {
                _lastTone = Enum.Parse<ToneStyle>(button.Tag!.ToString()!, ignoreCase: false);
            };
        }
    }

    private async Task CloseAfterSuccessAsync()
    {
        if (_isClosed)
        {
            return;
        }

        CancelSuccessClose();
        _successCloseCancellation = new CancellationTokenSource();

        try
        {
            await Task.Delay(SuccessCloseDelayMilliseconds, _successCloseCancellation.Token);
            if (!_isClosed && IsVisible)
            {
                Close();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelOperation();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    private void CancelSuccessClose()
    {
        _successCloseCancellation?.Cancel();
        _successCloseCancellation?.Dispose();
        _successCloseCancellation = null;
    }

    private bool IsCurrentContent(int contentVersion)
    {
        return !_isClosed && contentVersion == _contentVersion;
    }

    private void StartBusyAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = -124,
            To = 250,
            Duration = TimeSpan.FromSeconds(1.15),
            RepeatBehavior = RepeatBehavior.Forever
        };
        BusyIndicatorTransform.BeginAnimation(
            TranslateTransform.XProperty,
            animation,
            HandoffBehavior.SnapshotAndReplace);
    }

    private void StopBusyAnimation()
    {
        BusyIndicatorTransform.BeginAnimation(TranslateTransform.XProperty, null);
        BusyIndicatorTransform.X = 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _contentVersion++;
        CancelOperation();
        CancelSuccessClose();
        StopBusyAnimation();
        base.OnClosed(e);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        CancelOperation();
        CancelSuccessClose();
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_operationCancellation is not null)
            {
                CancelOperation();
                SetStatus("Cancelling...", StatusKind.Warning);
            }
            else
            {
                CancelSuccessClose();
                Close();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ApplyButton.IsEnabled)
        {
            e.Handled = true;
            Apply_Click(ApplyButton, new RoutedEventArgs());
        }
    }

    private enum StatusKind
    {
        Neutral,
        Working,
        Replacing,
        Success,
        Warning,
        Error
    }
}
