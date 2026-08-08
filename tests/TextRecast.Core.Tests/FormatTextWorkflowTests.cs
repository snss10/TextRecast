using TextRecast.Core.Abstractions;
using TextRecast.Core.Application;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Formatting;
using TextRecast.Core.Models;

namespace TextRecast.Core.Tests;

[TestClass]
public sealed class FormatTextWorkflowTests
{
    private static readonly SelectionContext Selection = new(
        "Original text",
        new IntPtr(42),
        7,
        DateTimeOffset.UtcNow);

    [TestMethod]
    public async Task CaptureAsyncReturnsCapturedSelection()
    {
        var capture = new StubSelectionCaptureService(
            SelectionCaptureResult.Ok(Selection, "Clipboard warning"));
        using var formatter = new StubFormatter("Formatted text");
        var workflow = new FormatTextWorkflow(capture, formatter, new StubReplacementService());

        var result = await workflow.CaptureAsync(Selection.TargetWindow);

        Assert.IsTrue(result.Success);
        Assert.AreSame(Selection, result.Selection);
        Assert.AreEqual("Clipboard warning", result.Warning);
    }

    [TestMethod]
    public async Task GenerateAsyncRequiresToneForChangeToneOperation()
    {
        using var formatter = new StubFormatter("Formatted text");
        var replacement = new StubReplacementService();
        var workflow = CreateWorkflow(formatter, replacement);

        var result = await workflow.GenerateAsync(
            Selection,
            FormatOperation.ChangeTone,
            tone: null,
            progress: null,
            CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, formatter.CallCount);
        Assert.AreEqual(0, replacement.CallCount);
    }

    [TestMethod]
    public async Task GenerateAsyncRejectsUnsafeGeneratedTextWithoutReplacing()
    {
        using var formatter = new StubFormatter("Unsafe\0text");
        var replacement = new StubReplacementService();
        var workflow = CreateWorkflow(formatter, replacement);

        var result = await workflow.GenerateAsync(
            Selection,
            FormatOperation.Improve,
            tone: null,
            progress: null,
            CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(0, replacement.CallCount);
        StringAssert.Contains(result.Message, "unsupported control data");
    }

    [TestMethod]
    public async Task GenerateAsyncReturnsValidatedTextWithoutReplacing()
    {
        using var formatter = new StubFormatter("Formatted text");
        var replacement = new StubReplacementService(TextReplacementResult.Ok());
        var workflow = CreateWorkflow(formatter, replacement);

        var result = await workflow.GenerateAsync(
            Selection,
            FormatOperation.Improve,
            tone: null,
            progress: null,
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Formatted text", result.GeneratedText);
        Assert.AreEqual(0, replacement.CallCount);
    }

    [TestMethod]
    public async Task ReplaceAsyncSendsEditedTextToReplacementService()
    {
        using var formatter = new StubFormatter("Unused");
        var replacement = new StubReplacementService(TextReplacementResult.Ok());
        var workflow = CreateWorkflow(formatter, replacement);

        var result = await workflow.ReplaceAsync(
            Selection,
            "User-edited revision",
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(1, replacement.CallCount);
        Assert.AreEqual("User-edited revision", replacement.LastReplacementText);
    }

    private static FormatTextWorkflow CreateWorkflow(
        ITextFormatter formatter,
        ITextReplacementService replacement)
    {
        return new FormatTextWorkflow(
            new StubSelectionCaptureService(SelectionCaptureResult.Ok(Selection)),
            formatter,
            replacement);
    }

    private sealed class StubSelectionCaptureService(SelectionCaptureResult result) : ISelectionCaptureService
    {
        public Task<SelectionCaptureResult> CaptureSelectedTextAsync(IntPtr targetWindow) =>
            Task.FromResult(result);
    }

    private sealed class StubFormatter(string output) : ITextFormatter
    {
        public int CallCount { get; private set; }

        public Task<string> FormatAsync(FormatTextRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(output);
        }

        public void Dispose()
        {
        }
    }

    private sealed class StubReplacementService : ITextReplacementService
    {
        private readonly TextReplacementResult _result;

        public StubReplacementService(TextReplacementResult? result = null)
        {
            _result = result ?? TextReplacementResult.Fail("Replacement not configured.");
        }

        public int CallCount { get; private set; }

        public string? LastReplacementText { get; private set; }

        public Task<TextReplacementResult> ReplaceAsync(
            SelectionContext selection,
            string replacementText,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReplacementText = replacementText;
            return Task.FromResult(_result);
        }
    }
}
