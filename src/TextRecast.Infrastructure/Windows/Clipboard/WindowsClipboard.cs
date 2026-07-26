using System.Windows;
using TextRecast.Infrastructure.Windows.Native;

namespace TextRecast.Infrastructure.Windows.Clipboard;

internal static class WindowsClipboard
{
    private const int RetryCount = 3;
    private const int RetryDelayMs = 50;

    public static ClipboardSnapshot CaptureSnapshot()
    {
        try
        {
            return new ClipboardSnapshot(true, global::System.Windows.Clipboard.GetDataObject());
        }
        catch
        {
            return new ClipboardSnapshot(false, null);
        }
    }

    public static async Task<ClipboardTextResult> ReadTextAsync(
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            if (NativeMethods.TryReadClipboardUnicodeText(maxCharacters, out var text, out var exceededLimit))
            {
                return new ClipboardTextResult(true, text, exceededLimit);
            }

            await Task.Delay(RetryDelayMs, cancellationToken);
        }

        return new ClipboardTextResult(false, string.Empty);
    }

    public static async Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                global::System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText);
                return true;
            }
            catch
            {
                await Task.Delay(RetryDelayMs, cancellationToken);
            }
        }

        return false;
    }

    public static async Task<bool> RestoreAsync(ClipboardSnapshot snapshot)
    {
        if (!snapshot.WasCaptured)
        {
            return false;
        }

        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                if (snapshot.Data is null)
                {
                    global::System.Windows.Clipboard.Clear();
                }
                else
                {
                    global::System.Windows.Clipboard.SetDataObject(snapshot.Data, true);
                }

                return true;
            }
            catch
            {
                await Task.Delay(RetryDelayMs);
            }
        }

        return false;
    }
}

internal sealed record ClipboardSnapshot(bool WasCaptured, IDataObject? Data);

internal sealed record ClipboardTextResult(bool Success, string Text, bool ExceededLimit = false);
