using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows.Automation;

namespace TextRecast.Infrastructure.Windows.Selection;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "The process-lifetime SemaphoreSlim never creates an OS wait handle.")]
public sealed class UiAutomationSelectionReader
{
    private const int MaxTreeDepth = 2;
    private const int MaxVisitedElements = 36;
    private readonly SemaphoreSlim _readGate = new(1, 1);

    public async Task<string> TryReadSelectedTextAsync(
        IntPtr targetWindow,
        int maxCharacters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!await _readGate.WaitAsync(0, cancellationToken))
        {
            return string.Empty;
        }

        var readTask = Task.Run(
            () => TryReadSelectedTextCore(targetWindow, maxCharacters),
            CancellationToken.None);
        var completedTask = await Task.WhenAny(readTask, Task.Delay(timeout, cancellationToken));

        if (completedTask == readTask)
        {
            try
            {
                return await readTask;
            }
            finally
            {
                _readGate.Release();
            }
        }

        _ = ReleaseGateWhenReadCompletesAsync(readTask);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Empty;
    }

    private static string TryReadSelectedTextCore(IntPtr targetWindow, int maxCharacters)
    {
        try
        {
            var focusedText = TryReadFromElement(AutomationElement.FocusedElement, maxCharacters);
            if (!string.IsNullOrWhiteSpace(focusedText))
            {
                return focusedText;
            }

            if (targetWindow == IntPtr.Zero)
            {
                return string.Empty;
            }

            var windowElement = AutomationElement.FromHandle(targetWindow);
            var visited = 0;
            return TryReadFromTree(windowElement, maxCharacters, depth: 0, ref visited);
        }
        catch
        {
            return string.Empty;
        }
    }

    private async Task ReleaseGateWhenReadCompletesAsync(Task<string> readTask)
    {
        try
        {
            _ = await readTask;
        }
        catch
        {
        }
        finally
        {
            _readGate.Release();
        }
    }

    private static string TryReadFromTree(AutomationElement root, int maxCharacters, int depth, ref int visited)
    {
        if (depth > MaxTreeDepth || visited >= MaxVisitedElements)
        {
            return string.Empty;
        }

        visited++;

        var directText = TryReadFromElement(root, maxCharacters);
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        var walker = TreeWalker.ControlViewWalker;
        var child = walker.GetFirstChild(root);

        while (child is not null)
        {
            var text = TryReadFromTree(child, maxCharacters, depth + 1, ref visited);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (visited >= MaxVisitedElements)
            {
                return string.Empty;
            }

            child = walker.GetNextSibling(child);
        }

        return string.Empty;
    }

    private static string TryReadFromElement(AutomationElement? element, int maxCharacters)
    {
        if (element is null)
        {
            return string.Empty;
        }

        if (!element.TryGetCurrentPattern(TextPattern.Pattern, out var pattern))
        {
            return string.Empty;
        }

        var textPattern = (TextPattern)pattern;
        var selections = textPattern.GetSelection();
        if (selections.Length == 0)
        {
            return string.Empty;
        }

        var output = new StringBuilder(Math.Min(maxCharacters + 1, 4096));
        foreach (var selection in selections)
        {
            var separatorLength = output.Length == 0 ? 0 : Environment.NewLine.Length;
            var remaining = maxCharacters + 1 - output.Length - separatorLength;
            if (remaining <= 0)
            {
                break;
            }

            var part = selection.GetText(remaining);
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            if (separatorLength > 0)
            {
                output.AppendLine();
            }

            output.Append(part);
        }

        return output.ToString().Trim();
    }
}
