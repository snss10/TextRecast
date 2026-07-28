using TextRecast.Core.Abstractions;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Models;
using TextRecast.Infrastructure.Windows.Clipboard;
using TextRecast.Infrastructure.Windows.Native;

namespace TextRecast.Infrastructure.Windows.Selection;

public sealed class SelectionCaptureService : ISelectionCaptureService
{
    private const int FocusSettleDelayMs = 35;
    private static readonly TimeSpan UiAutomationTimeout = TimeSpan.FromMilliseconds(120);
    private const int ClipboardPollIntervalMs = 20;
    private const int ClipboardTimeoutMs = 2_000;
    private readonly UiAutomationSelectionReader _uiAutomationSelectionReader;

    public SelectionCaptureService(UiAutomationSelectionReader uiAutomationSelectionReader)
    {
        _uiAutomationSelectionReader = uiAutomationSelectionReader;
    }

    public async Task<SelectionCaptureResult> CaptureSelectedTextAsync(IntPtr targetWindow)
    {
        if (!NativeMethods.IsWindowHandle(targetWindow))
        {
            return SelectionCaptureResult.Fail("The source application is no longer available.");
        }

        if (!NativeMethods.TrySetForegroundWindowHandle(targetWindow))
        {
            return SelectionCaptureResult.Fail("Could not return to the source application.");
        }

        await Task.Delay(FocusSettleDelayMs);
        var uiAutomationText = await TryReadUiAutomationTextAsync(targetWindow);
        if (!string.IsNullOrWhiteSpace(uiAutomationText))
        {
            if (uiAutomationText.Length > SelectionLimits.MaximumCharacters)
            {
                return SelectionCaptureResult.Fail(BuildSelectionTooLongMessage());
            }

            return SelectionCaptureResult.Ok(CreateSelection(uiAutomationText, targetWindow));
        }

        var previousClipboard = await WindowsClipboard.CaptureSnapshotAsync();
        var previousSequence = NativeMethods.GetClipboardSequence();

        if (!NativeMethods.SendCtrlC())
        {
            return SelectionCaptureResult.Fail("Could not copy the selected text.");
        }

        if (!await WaitForClipboardChangeAsync(previousSequence))
        {
            return SelectionCaptureResult.Fail("No selectable text was copied.");
        }

        var clipboardText = await WindowsClipboard.ReadTextAsync(
            SelectionLimits.MaximumCharacters,
            CancellationToken.None);
        var restored = await WindowsClipboard.RestoreAsync(previousClipboard);

        if (!clipboardText.Success || string.IsNullOrWhiteSpace(clipboardText.Text))
        {
            return SelectionCaptureResult.Fail("No selectable text was copied.");
        }

        if (clipboardText.ExceededLimit)
        {
            return SelectionCaptureResult.Fail(BuildSelectionTooLongMessage());
        }

        var warning = restored ? null : "The selected text was copied, but the previous clipboard content could not be restored.";
        return SelectionCaptureResult.Ok(CreateSelection(clipboardText.Text, targetWindow), warning);
    }

    private async Task<string> TryReadUiAutomationTextAsync(IntPtr targetWindow)
    {
        return await _uiAutomationSelectionReader.TryReadSelectedTextAsync(
            targetWindow,
            SelectionLimits.MaximumCharacters,
            UiAutomationTimeout,
            CancellationToken.None);
    }

    private static async Task<bool> WaitForClipboardChangeAsync(uint previousSequence)
    {
        var waited = 0;
        while (waited < ClipboardTimeoutMs)
        {
            await Task.Delay(ClipboardPollIntervalMs);
            waited += ClipboardPollIntervalMs;

            if (NativeMethods.GetClipboardSequence() != previousSequence)
            {
                return true;
            }
        }

        return false;
    }

    private static SelectionContext CreateSelection(string text, IntPtr targetWindow)
    {
        return new SelectionContext(
            text.Trim(),
            targetWindow,
            NativeMethods.GetWindowProcessId(targetWindow),
            DateTimeOffset.UtcNow);
    }

    private static string BuildSelectionTooLongMessage()
    {
        return $"This selection exceeds the {SelectionLimits.MaximumCharacters:N0}-character safety ceiling.";
    }

}
