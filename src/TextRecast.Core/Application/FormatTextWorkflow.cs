using TextRecast.Core.Abstractions;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Formatting;
using TextRecast.Core.Models;

namespace TextRecast.Core.Application;

public sealed class FormatTextWorkflow
{
    private const int SafetyMaxReplacementCharacters = 128_000;
    private readonly ISelectionCaptureService _selectionCapture;
    private readonly ITextFormatter _formatter;
    private readonly ITextReplacementService _replacement;

    public FormatTextWorkflow(
        ISelectionCaptureService selectionCapture,
        ITextFormatter formatter,
        ITextReplacementService replacement)
    {
        _selectionCapture = selectionCapture;
        _formatter = formatter;
        _replacement = replacement;
    }

    public Task<SelectionCaptureResult> CaptureAsync(IntPtr targetWindow) =>
        _selectionCapture.CaptureSelectedTextAsync(targetWindow);

    public async Task<FormatTextOutcome> ApplyAsync(
        SelectionContext selection,
        FormatOperation operation,
        ToneStyle? tone,
        IProgress<FormatTextStage>? progress,
        CancellationToken cancellationToken)
    {
        if (operation == FormatOperation.ChangeTone && tone is null)
        {
            return new FormatTextOutcome(false, string.Empty, "Choose a tone before applying.");
        }

        progress?.Report(FormatTextStage.Formatting);
        var generatedText = await _formatter.FormatAsync(
            new FormatTextRequest(selection.Text, operation, tone),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(generatedText))
        {
            return new FormatTextOutcome(false, string.Empty, "The formatter returned no replacement text.");
        }

        if (generatedText.Contains('\0'))
        {
            return new FormatTextOutcome(false, generatedText, "The generated text contains unsupported control data.");
        }

        if (generatedText.Length > SafetyMaxReplacementCharacters)
        {
            return new FormatTextOutcome(
                false,
                generatedText,
                $"The generated text exceeds the {SafetyMaxReplacementCharacters:N0}-character replacement safety ceiling.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(FormatTextStage.Replacing);
        var replacement = await _replacement.ReplaceAsync(selection, generatedText, cancellationToken);
        return new FormatTextOutcome(replacement.Success, generatedText, replacement.Message, replacement.Warning);
    }

    public Task<TextReplacementResult> RetryReplacementAsync(
        SelectionContext selection,
        string generatedText,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(generatedText) ||
            generatedText.Length > SafetyMaxReplacementCharacters ||
            generatedText.Contains('\0'))
        {
            return Task.FromResult(TextReplacementResult.Fail("The generated text is not safe to replace."));
        }

        return _replacement.ReplaceAsync(selection, generatedText, cancellationToken);
    }
}
