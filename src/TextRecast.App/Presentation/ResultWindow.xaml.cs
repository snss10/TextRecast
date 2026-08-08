using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
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
    Justification = "WPF owns the window lifecycle; OnClosed cancels and disposes its token source.")]
public partial class ResultWindow : Window
{
    private readonly Func<SelectionContext, FormatOperation, ToneStyle?, IProgress<FormatTextStage>?, CancellationToken, Task<FormatTextOutcome>> _generateAsync;
    private readonly Func<SelectionContext, string, CancellationToken, Task<TextReplacementResult>> _replaceAsync;
    private CancellationTokenSource? _operationCancellation;
    private SelectionContext? _selection;
    private int _contentVersion;
    private bool _isBusy;
    private bool _isClosed;

    internal ResultWindow(
        string displayText,
        SelectionContext? selection,
        string modelDisplayName,
        Func<SelectionContext, FormatOperation, ToneStyle?, IProgress<FormatTextStage>?, CancellationToken, Task<FormatTextOutcome>> generateAsync,
        Func<SelectionContext, string, CancellationToken, Task<TextReplacementResult>> replaceAsync)
    {
        InitializeComponent();
        ApplicationTheme.Apply(this);
        _generateAsync = generateAsync;
        _replaceAsync = replaceAsync;
        ModelSummaryTextBlock.Text = $"Generated locally  ·  {modelDisplayName}";
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
        _selection = selection;
        ResetChoices();
        OriginalTextBox.Text = selection is null ? string.Empty : displayText;
        RevisedTextBox.Clear();
        FooterCaptureMessageTextBlock.Text = selection is null ? displayText : string.Empty;
        FooterCaptureMessagePanel.Visibility = selection is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        SetBusy(false);
        SetStatus(
            selection is null
                ? "Source remains unchanged."
                : "Choose an operation to generate a revision.",
            selection is null ? StatusKind.SourceSafe : StatusKind.Neutral);
    }

    private async Task GenerateRevisionAsync()
    {
        if (_isClosed || _isBusy || _selection is null || !HasRunnableChoice())
        {
            return;
        }

        var contentVersion = _contentVersion;
        var selection = _selection;
        var operation = GetSelectedOperation();
        ToneStyle? tone = operation == FormatOperation.ChangeTone ? GetSelectedTone() : null;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        SetBusy(true);
        SetStatus("Generating locally...", StatusKind.Working);

        try
        {
            var progress = new Progress<FormatTextStage>(stage =>
            {
                if (IsCurrentContent(contentVersion) && stage == FormatTextStage.Formatting)
                {
                    SetStatus("Generating locally...", StatusKind.Working);
                }
            });
            var outcome = await _generateAsync(
                selection,
                operation,
                tone,
                progress,
                cancellation.Token);
            if (!IsCurrentContent(contentVersion))
            {
                return;
            }

            if (!outcome.Success)
            {
                SetStatus(outcome.Message, StatusKind.Error);
                return;
            }

            RevisedTextBox.Text = outcome.GeneratedText;
            RevisedTextBox.CaretIndex = RevisedTextBox.Text.Length;
            SetStatus(
                "Source unchanged until you choose Replace selection",
                StatusKind.SourceSafe);
            RevisedTextBox.Focus();
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Generation cancelled. Source remains unchanged.", StatusKind.Warning);
            }
        }
        catch (FileNotFoundException)
        {
            ShowGenerationFailure(contentVersion, "Local model file is missing.");
        }
        catch (InvalidDataException exception)
        {
            ShowGenerationFailure(contentVersion, exception.Message);
        }
        catch (TextFormattingException exception)
        {
            ShowGenerationFailure(contentVersion, exception.Message);
        }
        catch (OutOfMemoryException)
        {
            ShowGenerationFailure(contentVersion, "Not enough memory to run the local model.");
        }
        catch (DllNotFoundException)
        {
            ShowGenerationFailure(contentVersion, "The local model runtime is unavailable.");
        }
        catch (BadImageFormatException)
        {
            ShowGenerationFailure(contentVersion, "The local model runtime is incompatible.");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Generation failed with {exception.GetType().FullName}; HResult=0x{exception.HResult:X8}.");
            ShowGenerationFailure(
                contentVersion,
                "Generation failed unexpectedly. Try a smaller selection.");
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
                SetBusy(false);
            }
        }
    }

    private async void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (_isClosed || _isBusy || _selection is null || !HasUsableRevision())
        {
            return;
        }

        var contentVersion = _contentVersion;
        var cancellation = new CancellationTokenSource();
        _operationCancellation = cancellation;
        SetBusy(true);
        SetStatus("Replacing selection...", StatusKind.Replacing);

        try
        {
            var result = await _replaceAsync(
                _selection,
                RevisedTextBox.Text,
                cancellation.Token);
            if (!IsCurrentContent(contentVersion))
            {
                return;
            }

            if (!result.Success)
            {
                SetStatus(BuildStatus(result.Message, result.Warning), StatusKind.Error);
                return;
            }

            _selection = null;
            if (!string.IsNullOrWhiteSpace(result.Warning))
            {
                SetStatus(BuildStatus(result.Message, result.Warning), StatusKind.Warning);
                return;
            }

            Close();
        }
        catch (OperationCanceledException)
        {
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Replacement cancelled. Your revision is preserved.", StatusKind.Warning);
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Replacement failed with {exception.GetType().FullName}; HResult=0x{exception.HResult:X8}.");
            if (IsCurrentContent(contentVersion))
            {
                SetStatus("Replacement failed. Your revision is preserved.", StatusKind.Error);
            }
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_operationCancellation, cancellation))
            {
                _operationCancellation = null;
                SetBusy(false);
            }
        }
    }

    private async void Regenerate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateRevisionAsync();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (!HasUsableRevision())
        {
            return;
        }

        try
        {
            Clipboard.SetText(RevisedTextBox.Text);
            SetStatus("Revised text copied. Source remains unchanged.", StatusKind.Success);
        }
        catch (ExternalException)
        {
            SetStatus("The clipboard is busy. Try Copy again.", StatusKind.Error);
        }
    }

    private void RevisedTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateActionAvailability();
    }

    private async void Operation_Checked(object sender, RoutedEventArgs e)
    {
        if (TonePanel is null || sender is not RadioButton button)
        {
            return;
        }

        var operation = Enum.Parse<FormatOperation>(button.Tag!.ToString()!, ignoreCase: false);
        TonePanel.Visibility = operation == FormatOperation.ChangeTone
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateActionAvailability();

        if (IsLoaded && operation != FormatOperation.ChangeTone)
        {
            await GenerateRevisionAsync();
        }
    }

    private async void Tone_Checked(object sender, RoutedEventArgs e)
    {
        UpdateActionAvailability();
        if (IsLoaded && GetSelectedOperationOrNull() == FormatOperation.ChangeTone)
        {
            await GenerateRevisionAsync();
        }
    }

    private FormatOperation GetSelectedOperation()
    {
        var tag = OperationChoices.Children.OfType<RadioButton>()
            .Single(button => button.IsChecked == true).Tag?.ToString();
        return Enum.Parse<FormatOperation>(tag!, ignoreCase: false);
    }

    private FormatOperation? GetSelectedOperationOrNull()
    {
        var button = OperationChoices.Children.OfType<RadioButton>()
            .SingleOrDefault(choice => choice.IsChecked == true);
        return button is null
            ? null
            : Enum.Parse<FormatOperation>(button.Tag!.ToString()!, ignoreCase: false);
    }

    private ToneStyle GetSelectedTone()
    {
        var tag = ToneChoices.Children
            .OfType<RadioButton>()
            .Single(button => button.IsChecked == true)
            .Tag?.ToString();
        return Enum.Parse<ToneStyle>(tag!, ignoreCase: false);
    }

    private void ResetChoices()
    {
        foreach (var button in OperationChoices.Children.OfType<RadioButton>())
        {
            button.IsChecked = false;
        }

        foreach (var button in ToneChoices.Children.OfType<RadioButton>())
        {
            button.IsChecked = false;
        }

        TonePanel.Visibility = Visibility.Collapsed;
    }

    private void SetBusy(bool isBusy)
    {
        if (_isClosed)
        {
            return;
        }

        _isBusy = isBusy;
        OperationChoices.IsEnabled = !isBusy && _selection is not null;
        ToneChoices.IsEnabled = !isBusy && _selection is not null;
        RevisedTextBox.IsReadOnly = isBusy;
        ActivityProgressBar.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
        if (isBusy)
        {
            StartBusyAnimation();
        }
        else
        {
            StopBusyAnimation();
        }

        UpdateActionAvailability();
    }

    private void UpdateActionAvailability()
    {
        if (RegenerateButton is null)
        {
            return;
        }

        RegenerateButton.IsEnabled = !_isBusy && _selection is not null && HasRunnableChoice();
        CopyButton.IsEnabled = !_isBusy && HasUsableRevision();
        ReplaceButton.IsEnabled = !_isBusy && _selection is not null && HasUsableRevision();
    }

    private bool HasUsableRevision()
    {
        return RevisedTextBox is not null &&
            !string.IsNullOrWhiteSpace(RevisedTextBox.Text);
    }

    private bool HasRunnableChoice()
    {
        var operation = GetSelectedOperationOrNull();
        if (operation is null)
        {
            return false;
        }

        return operation != FormatOperation.ChangeTone ||
            ToneChoices.Children.OfType<RadioButton>().Any(button => button.IsChecked == true);
    }

    private void ShowGenerationFailure(int contentVersion, string message)
    {
        if (IsCurrentContent(contentVersion))
        {
            SetStatus(message, StatusKind.Error);
        }
    }

    private static string BuildStatus(string message, string? warning)
    {
        return string.IsNullOrWhiteSpace(warning) ? message : $"{message} {warning}";
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
            StatusKind.SourceSafe => PackIconLucideKind.ShieldCheck,
            StatusKind.Warning => PackIconLucideKind.CircleAlert,
            StatusKind.Error => PackIconLucideKind.CircleX,
            _ => PackIconLucideKind.ShieldCheck
        };
        StatusIcon.Spin = false;
        StatusIcon.Foreground = kind switch
        {
            StatusKind.Working => (Brush)FindResource("AccentBrush"),
            StatusKind.Replacing => (Brush)FindResource("ReplacingBrush"),
            StatusKind.Success => (Brush)FindResource("SuccessBrush"),
            StatusKind.SourceSafe => (Brush)FindResource("SuccessBrush"),
            StatusKind.Warning => (Brush)FindResource("WarningBrush"),
            StatusKind.Error => (Brush)FindResource("ErrorBrush"),
            _ => (Brush)FindResource("SecondaryTextBrush")
        };
    }

    private bool IsCurrentContent(int contentVersion)
    {
        return !_isClosed && contentVersion == _contentVersion;
    }

    private void StartBusyAnimation()
    {
        var animation = new DoubleAnimation
        {
            From = -150,
            To = Math.Max(500, ActualWidth - 80),
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

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            CancelOperation();
            SetStatus("Cancelling...", StatusKind.Warning);
            return;
        }

        Close();
    }

    private void CancelOperation()
    {
        _operationCancellation?.Cancel();
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosed = true;
        _contentVersion++;
        CancelOperation();
        StopBusyAnimation();
        base.OnClosed(e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel_Click(CancelButton, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Enter &&
                 Keyboard.Modifiers == ModifierKeys.Control &&
                 ReplaceButton.IsEnabled)
        {
            Replace_Click(ReplaceButton, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private enum StatusKind
    {
        Neutral,
        Working,
        Replacing,
        Success,
        SourceSafe,
        Warning,
        Error
    }
}
