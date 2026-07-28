using System.Windows;
using System.Windows.Threading;
using TextRecast.Infrastructure.Windows.Native;

namespace TextRecast.Infrastructure.Windows.Clipboard;

internal static class WindowsClipboard
{
    private const int RetryCount = 3;
    private const int RetryDelayMs = 50;
    private static readonly Task<Dispatcher> ClipboardDispatcher = StartClipboardDispatcher();

    public static async Task<ClipboardSnapshot> CaptureSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await InvokeAsync(
                () => new ClipboardSnapshot(
                    true,
                    global::System.Windows.Clipboard.GetDataObject()),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
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
            var result = await InvokeAsync(
                () =>
                {
                    var success = NativeMethods.TryReadClipboardUnicodeText(
                        maxCharacters,
                        out var text,
                        out var exceededLimit);
                    return new ClipboardTextResult(success, text, exceededLimit);
                },
                cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                return result;
            }

            await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
        }

        return new ClipboardTextResult(false, string.Empty);
    }

    public static async Task<bool> SetTextAsync(string text, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RetryCount; attempt++)
        {
            try
            {
                await InvokeAsync(
                    () => global::System.Windows.Clipboard.SetText(text, TextDataFormat.UnicodeText),
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(RetryDelayMs, cancellationToken).ConfigureAwait(false);
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
                await InvokeAsync(
                    () =>
                    {
                        if (snapshot.Data is null)
                        {
                            global::System.Windows.Clipboard.Clear();
                        }
                        else
                        {
                            global::System.Windows.Clipboard.SetDataObject(snapshot.Data, true);
                        }
                    },
                    CancellationToken.None).ConfigureAwait(false);

                return true;
            }
            catch
            {
                await Task.Delay(RetryDelayMs).ConfigureAwait(false);
            }
        }

        return false;
    }

    private static async Task<T> InvokeAsync<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        var dispatcher = await ClipboardDispatcher.ConfigureAwait(false);
        return await dispatcher.InvokeAsync(
            operation,
            DispatcherPriority.Normal,
            cancellationToken).Task.ConfigureAwait(false);
    }

    private static async Task InvokeAsync(Action operation, CancellationToken cancellationToken)
    {
        var dispatcher = await ClipboardDispatcher.ConfigureAwait(false);
        await dispatcher.InvokeAsync(
            operation,
            DispatcherPriority.Normal,
            cancellationToken).Task.ConfigureAwait(false);
    }

    private static Task<Dispatcher> StartClipboardDispatcher()
    {
        var started = new TaskCompletionSource<Dispatcher>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(dispatcher));
            started.SetResult(dispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "TextRecast Clipboard STA"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return started.Task;
    }
}

internal sealed record ClipboardSnapshot(bool WasCaptured, IDataObject? Data);

internal sealed record ClipboardTextResult(bool Success, string Text, bool ExceededLimit = false);
