using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
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
                CaptureSnapshot,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return ClipboardSnapshot.Unavailable;
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
                var restored = await InvokeAsync(
                    () => RestoreAndVerify(snapshot),
                    CancellationToken.None).ConfigureAwait(false);
                if (restored)
                {
                    return true;
                }
            }
            catch
            {
            }

            if (attempt < RetryCount - 1)
            {
                await Task.Delay(RetryDelayMs).ConfigureAwait(false);
            }
        }

        return false;
    }

    private static ClipboardSnapshot CaptureSnapshot()
    {
        var source = global::System.Windows.Clipboard.GetDataObject();
        if (source is null)
        {
            return ClipboardSnapshot.Empty;
        }

        var formats = new List<ClipboardFormatSnapshot>();
        foreach (var format in source.GetFormats(autoConvert: false).Distinct(StringComparer.Ordinal))
        {
            var value = source.GetData(format, autoConvert: false)
                ?? throw new InvalidOperationException($"Clipboard format '{format}' returned no data.");
            formats.Add(new ClipboardFormatSnapshot(format, CloneClipboardValue(value)));
        }

        return new ClipboardSnapshot(true, formats.AsReadOnly());
    }

    private static bool RestoreAndVerify(ClipboardSnapshot snapshot)
    {
        if (snapshot.Formats.Count == 0)
        {
            global::System.Windows.Clipboard.Clear();
        }
        else
        {
            var dataObject = new DataObject();
            foreach (var format in snapshot.Formats)
            {
                dataObject.SetData(
                    format.Format,
                    CloneClipboardValue(format.Data),
                    autoConvert: false);
            }

            global::System.Windows.Clipboard.SetDataObject(dataObject, copy: true);
        }

        return ClipboardMatches(snapshot);
    }

    private static bool ClipboardMatches(ClipboardSnapshot snapshot)
    {
        var current = global::System.Windows.Clipboard.GetDataObject();
        if (snapshot.Formats.Count == 0)
        {
            return current is null || current.GetFormats(autoConvert: false).Length == 0;
        }

        if (current is null)
        {
            return false;
        }

        foreach (var format in snapshot.Formats)
        {
            if (!current.GetDataPresent(format.Format, autoConvert: false))
            {
                return false;
            }

            var restoredValue = current.GetData(format.Format, autoConvert: false);
            if (restoredValue is null || !ClipboardValuesMatch(format.Data, restoredValue))
            {
                return false;
            }
        }

        return true;
    }

    private static object CloneClipboardValue(object value)
    {
        switch (value)
        {
            case string:
            case Uri:
                return value;
            case byte[] bytes:
                return bytes.ToArray();
            case ReadOnlyMemory<byte> readOnlyMemory:
                return readOnlyMemory.ToArray();
            case Memory<byte> memory:
                return memory.ToArray();
            case Stream stream:
                return new MemoryStream(ReadAllBytes(stream), writable: false);
            case StringCollection strings:
                {
                    var copy = new StringCollection();
                    copy.AddRange(strings.Cast<string>().ToArray());
                    return copy;
                }
            case Freezable freezable:
                {
                    var copy = freezable.CloneCurrentValue();
                    if (copy.CanFreeze)
                    {
                        copy.Freeze();
                    }

                    return copy;
                }
            case Array array when IsImmutableArray(array):
                return array.Clone();
            case ICloneable cloneable:
                return cloneable.Clone()
                    ?? throw new InvalidOperationException("Clipboard data could not be cloned.");
            default:
                {
                    var type = value.GetType();
                    if (type.IsValueType)
                    {
                        return value;
                    }

                    throw new NotSupportedException(
                        $"Clipboard format data of type '{type.FullName}' cannot be copied safely.");
                }
        }
    }

    private static bool ClipboardValuesMatch(object expected, object actual)
    {
        if (ReferenceEquals(expected, actual))
        {
            return true;
        }

        if (TryGetBinaryData(expected, out var expectedBytes) &&
            TryGetBinaryData(actual, out var actualBytes))
        {
            return expectedBytes.AsSpan().SequenceEqual(actualBytes);
        }

        if (expected is BitmapSource expectedBitmap && actual is BitmapSource actualBitmap)
        {
            return BitmapsMatch(expectedBitmap, actualBitmap);
        }

        if (expected is StringCollection expectedStrings && actual is StringCollection actualStrings)
        {
            return expectedStrings.Cast<string>().SequenceEqual(
                actualStrings.Cast<string>(),
                StringComparer.Ordinal);
        }

        if (expected is Array expectedArray && actual is Array actualArray)
        {
            return ArraysMatch(expectedArray, actualArray);
        }

        return expected.Equals(actual);
    }

    private static bool TryGetBinaryData(object value, out byte[] bytes)
    {
        switch (value)
        {
            case byte[] byteArray:
                bytes = byteArray;
                return true;
            case ReadOnlyMemory<byte> readOnlyMemory:
                bytes = readOnlyMemory.ToArray();
                return true;
            case Memory<byte> memory:
                bytes = memory.ToArray();
                return true;
            case Stream stream:
                bytes = ReadAllBytes(stream);
                return true;
            default:
                bytes = [];
                return false;
        }
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        var originalPosition = stream.CanSeek ? stream.Position : 0;
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = originalPosition;
            }
        }
    }

    private static bool BitmapsMatch(BitmapSource expected, BitmapSource actual)
    {
        if (expected.PixelWidth != actual.PixelWidth ||
            expected.PixelHeight != actual.PixelHeight ||
            expected.Format != actual.Format)
        {
            return false;
        }

        var stride = (expected.PixelWidth * expected.Format.BitsPerPixel + 7) / 8;
        var expectedPixels = new byte[stride * expected.PixelHeight];
        var actualPixels = new byte[stride * actual.PixelHeight];
        expected.CopyPixels(expectedPixels, stride, 0);
        actual.CopyPixels(actualPixels, stride, 0);
        return expectedPixels.AsSpan().SequenceEqual(actualPixels);
    }

    private static bool ArraysMatch(Array expected, Array actual)
    {
        if (expected.Rank != actual.Rank || expected.Length != actual.Length)
        {
            return false;
        }

        for (var dimension = 0; dimension < expected.Rank; dimension++)
        {
            if (expected.GetLength(dimension) != actual.GetLength(dimension))
            {
                return false;
            }
        }

        var expectedValues = expected.Cast<object?>();
        var actualValues = actual.Cast<object?>();
        return expectedValues.Zip(actualValues).All(pair =>
            pair.First is null
                ? pair.Second is null
                : pair.Second is not null && ClipboardValuesMatch(pair.First, pair.Second));
    }

    private static bool IsImmutableArray(Array array)
    {
        var elementType = array.GetType().GetElementType();
        return elementType is not null &&
               (elementType.IsValueType || elementType == typeof(string));
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

internal sealed record ClipboardSnapshot(
    bool WasCaptured,
    IReadOnlyList<ClipboardFormatSnapshot> Formats)
{
    public static ClipboardSnapshot Empty { get; } = new(true, []);

    public static ClipboardSnapshot Unavailable { get; } = new(false, []);
}

internal sealed record ClipboardFormatSnapshot(string Format, object Data);

internal sealed record ClipboardTextResult(bool Success, string Text, bool ExceededLimit = false);
