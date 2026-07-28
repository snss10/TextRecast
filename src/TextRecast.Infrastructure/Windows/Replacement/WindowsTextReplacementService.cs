using TextRecast.Core.Abstractions;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Models;
using TextRecast.Infrastructure.Windows.Clipboard;
using TextRecast.Infrastructure.Windows.Native;
using TextRecast.Infrastructure.Windows.Selection;

namespace TextRecast.Infrastructure.Windows.Replacement;

public sealed class WindowsTextReplacementService : ITextReplacementService
{
    private const int FocusDelayMs = 60;
    private const int ClipboardPollMs = 20;
    private const int MinimumClipboardTimeoutMs = 750;
    private const int MaximumClipboardTimeoutMs = 3_000;
    private const int MinimumPasteSettleDelayMs = 100;
    private const int MaximumPasteSettleDelayMs = 2_000;
    private const string ClipboardRestorationWarning =
        "The previous clipboard content could not be restored.";
    private const string ReplacementClipboardRestorationWarning =
        "The text was replaced, but the previous clipboard content could not be restored.";
    private static readonly TimeSpan SelectionLifetime = TimeSpan.FromMinutes(15);
    private readonly UiAutomationSelectionReader _selectionReader;

    public WindowsTextReplacementService(UiAutomationSelectionReader selectionReader)
    {
        _selectionReader = selectionReader;
    }

    public async Task<TextReplacementResult> ReplaceAsync(
        SelectionContext selection,
        string replacementText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(replacementText))
        {
            return TextReplacementResult.Fail("The formatter returned no replacement text.");
        }

        if (!NativeMethods.IsWindowHandle(selection.TargetWindow))
        {
            return TextReplacementResult.Fail("The source window is no longer available.");
        }

        if (DateTimeOffset.UtcNow - selection.CapturedAt > SelectionLifetime)
        {
            return TextReplacementResult.Fail("The captured selection expired. Select the text again.");
        }

        if (selection.TargetProcessId == 0 ||
            NativeMethods.GetWindowProcessId(selection.TargetWindow) != selection.TargetProcessId)
        {
            return TextReplacementResult.Fail("The source window identity changed, so nothing was replaced.");
        }

        var previousClipboard = await WindowsClipboard.CaptureSnapshotAsync(cancellationToken);
        if (!previousClipboard.WasCaptured)
        {
            return TextReplacementResult.Fail("The clipboard is unavailable, so the text was not replaced.");
        }

        if (!NativeMethods.TrySetForegroundWindowHandle(selection.TargetWindow))
        {
            return TextReplacementResult.Fail("Could not return to the source application.");
        }

        await Task.Delay(FocusDelayMs, cancellationToken);
        if (NativeMethods.GetForegroundWindowHandle() != selection.TargetWindow)
        {
            return TextReplacementResult.Fail("The source application did not accept focus.");
        }

        var validation = await ValidateSelectionAsync(selection, previousClipboard, cancellationToken);
        if (!validation.Success)
        {
            return validation;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await WindowsClipboard.SetTextAsync(replacementText, cancellationToken))
        {
            var restoreSucceeded = await WindowsClipboard.RestoreAsync(previousClipboard);
            return TextReplacementResult.Fail(
                "Could not prepare the replacement text on the clipboard.",
                GetRestorationWarning(restoreSucceeded));
        }

        if (!NativeMethods.SendCtrlV())
        {
            var restoreSucceeded = await WindowsClipboard.RestoreAsync(previousClipboard);
            return TextReplacementResult.Fail(
                "Could not paste into the source application.",
                GetRestorationWarning(restoreSucceeded));
        }

        await Task.Delay(GetPasteSettleDelay(replacementText.Length), CancellationToken.None);
        var restored = await WindowsClipboard.RestoreAsync(previousClipboard);
        var warning = restored ? null : ReplacementClipboardRestorationWarning;
        return TextReplacementResult.Ok(warning);
    }

    private async Task<TextReplacementResult> ValidateSelectionAsync(
        SelectionContext selection,
        ClipboardSnapshot previousClipboard,
        CancellationToken cancellationToken)
    {
        var automationText = await _selectionReader.TryReadSelectedTextAsync(
            selection.TargetWindow,
            SelectionLimits.MaximumCharacters,
            GetUiAutomationTimeout(selection.Text.Length),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(automationText))
        {
            return TextMatches(selection.Text, automationText)
                ? TextReplacementResult.Ok()
                : TextReplacementResult.Fail("The selection changed, so nothing was replaced.");
        }

        var selectionMatches = false;
        var verificationMessage = "The original selection could not be verified.";
        var restored = false;
        try
        {
            var previousSequence = NativeMethods.GetClipboardSequence();
            if (NativeMethods.SendCtrlC() &&
                await WaitForClipboardChangeAsync(
                    previousSequence,
                    GetClipboardTimeout(selection.Text.Length),
                    cancellationToken))
            {
                var currentSelection = await WindowsClipboard.ReadTextAsync(
                    SelectionLimits.MaximumCharacters,
                    cancellationToken);
                selectionMatches = currentSelection.Success &&
                                   !currentSelection.ExceededLimit &&
                                   TextMatches(selection.Text, currentSelection.Text);
                if (!selectionMatches)
                {
                    verificationMessage = "The selection changed, so nothing was replaced.";
                }
            }
        }
        finally
        {
            restored = await WindowsClipboard.RestoreAsync(previousClipboard);
        }

        if (!restored)
        {
            return TextReplacementResult.Fail(verificationMessage, ClipboardRestorationWarning);
        }

        return selectionMatches
            ? TextReplacementResult.Ok()
            : TextReplacementResult.Fail(verificationMessage);
    }

    private static bool TextMatches(string expected, string actual)
    {
        return string.Equals(Normalize(expected), Normalize(actual), StringComparison.Ordinal);
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static TimeSpan GetUiAutomationTimeout(int characterCount)
    {
        var milliseconds = Math.Clamp(160 + characterCount / 128, 160, 600);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static int GetClipboardTimeout(int characterCount)
    {
        return Math.Clamp(
            MinimumClipboardTimeoutMs + characterCount / 16,
            MinimumClipboardTimeoutMs,
            MaximumClipboardTimeoutMs);
    }

    private static TimeSpan GetPasteSettleDelay(int characterCount)
    {
        var milliseconds = Math.Clamp(
            MinimumPasteSettleDelayMs + characterCount / 16,
            MinimumPasteSettleDelayMs,
            MaximumPasteSettleDelayMs);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static string? GetRestorationWarning(bool restored)
    {
        return restored ? null : ClipboardRestorationWarning;
    }

    private static async Task<bool> WaitForClipboardChangeAsync(
        uint previousSequence,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var elapsed = 0;
        while (elapsed < timeoutMs)
        {
            await Task.Delay(ClipboardPollMs, cancellationToken);
            elapsed += ClipboardPollMs;
            if (NativeMethods.GetClipboardSequence() != previousSequence)
            {
                return true;
            }
        }

        return false;
    }

}
