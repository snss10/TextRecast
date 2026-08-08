using System.Windows;
using System.Windows.Controls;
using TextRecast.App.Presentation;
using TextRecast.Core.Abstractions;
using TextRecast.Core.Application;
using TextRecast.Core.Application.Results;
using TextRecast.Core.Formatting;
using TextRecast.Core.Models;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.App.Tests;

[TestClass]
public sealed class ApplicationWindowConstructionTests
{
    [TestMethod]
    public void ApplicationWindowsLoadAndReviewWaitsForOperationOnStaThread()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new global::System.Windows.Application();
                application.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/TextRecast;component/Presentation/Themes/ApplicationResources.xaml",
                        UriKind.Absolute)
                });
                var workflow = new FormatTextWorkflow(
                    new CaptureService(),
                    new Formatter(),
                    new ReplacementService());
                var launcher = new MainWindow(workflow, SlmModelCatalog.Default);
                var information = new InformationWindow(
                    ApplicationInformation.CreateAbout());
                var legalInformation = new InformationWindow(
                    ApplicationInformation.CreateLegalAndPrivacy());
                var generationCount = 0;
                var replacementCount = 0;
                var selection = new SelectionContext(
                    "Original text",
                    IntPtr.Zero,
                    0,
                    DateTimeOffset.UtcNow);
                var result = new ResultWindow(
                    "Original text",
                    selection,
                    SlmModelCatalog.Qwen35Balanced.DisplayName,
                    (_, _, _, _, _) =>
                    {
                        generationCount++;
                        return Task.FromResult(
                            new FormatTextOutcome(true, "Revised text", "Generated locally."));
                    },
                    (_, _, _) =>
                    {
                        replacementCount++;
                        return Task.FromResult(TextReplacementResult.Ok());
                    });

                Assert.AreEqual(52, launcher.Width);
                Assert.AreEqual(560, information.Width);
                Assert.AreEqual(440, information.Height);
                Assert.IsFalse(information.ShowInTaskbar);
                Assert.IsTrue(information.Topmost);
                Assert.AreEqual(
                    Visibility.Collapsed,
                    ((TabControl)information.FindName("PagesTabControl")).Visibility);
                Assert.AreEqual(
                    Visibility.Visible,
                    ((Border)information.FindName("SinglePagePanel")).Visibility);
                Assert.AreEqual(
                    Visibility.Visible,
                    ((TabControl)legalInformation.FindName("PagesTabControl")).Visibility);
                Assert.AreEqual(
                    Visibility.Collapsed,
                    ((Border)legalInformation.FindName("SinglePagePanel")).Visibility);
                Assert.AreEqual(560, result.Width);
                Assert.AreEqual(440, result.Height);
                Assert.IsFalse(result.ShowInTaskbar);
                Assert.IsTrue(result.Topmost);

                result.Show();
                var operationChoices = (Grid)result.FindName("OperationChoices");
                var improve = operationChoices.Children.OfType<RadioButton>().First();
                var regenerate = (Button)result.FindName("RegenerateButton");
                var copy = (Button)result.FindName("CopyButton");
                var replace = (Button)result.FindName("ReplaceButton");
                var rootLayout = (Grid)result.FindName("RootLayout");
                var originalPanel = (Border)result.FindName("OriginalPanelBorder");
                var revisedPanel = (Border)result.FindName("RevisedPanelBorder");
                var statusIcon = (Control)result.FindName("StatusIcon");
                var statusRow = (Grid)statusIcon.Parent;
                var originalText = (TextBox)result.FindName("OriginalTextBox");
                var footerCaptureMessage = (StackPanel)result.FindName(
                    "FooterCaptureMessagePanel");
                var footerCaptureMessageText = (TextBlock)result.FindName(
                    "FooterCaptureMessageTextBlock");

                Assert.AreEqual(0, generationCount);
                Assert.IsFalse(operationChoices.Children.OfType<RadioButton>()
                    .Any(button => button.IsChecked == true));
                Assert.IsFalse(regenerate.IsEnabled);
                Assert.IsFalse(replace.IsEnabled);
                Assert.AreEqual(Visibility.Collapsed, footerCaptureMessage.Visibility);
                Assert.AreEqual(3, Grid.GetRow(originalPanel));
                Assert.AreEqual(4, Grid.GetRow(revisedPanel));
                Assert.AreEqual(5, Grid.GetRow(statusRow));
                Assert.AreEqual(
                    rootLayout.RowDefinitions[3].Height,
                    rootLayout.RowDefinitions[4].Height);
                Assert.AreEqual(
                    application.FindResource("DividerBrush"),
                    revisedPanel.BorderBrush);

                ApplicationTheme.Apply(result, dark: true);
                Assert.AreEqual(
                    application.FindResource("DisabledSurfaceBrush"),
                    regenerate.Background);
                Assert.AreEqual(
                    application.FindResource("DisabledSurfaceBrush"),
                    copy.Background);
                Assert.AreEqual(
                    application.FindResource("DisabledSurfaceBrush"),
                    replace.Background);

                result.UpdateResult("No selectable text was copied.", selection: null);
                Assert.AreEqual(string.Empty, originalText.Text);
                Assert.AreEqual(Visibility.Visible, footerCaptureMessage.Visibility);
                Assert.AreEqual(
                    "No selectable text was copied.",
                    footerCaptureMessageText.Text);
                Assert.AreEqual(
                    application.FindResource("DisabledTextBrush"),
                    improve.Foreground);

                result.UpdateResult("Original text", selection);
                Assert.AreEqual(Visibility.Collapsed, footerCaptureMessage.Visibility);

                improve.IsChecked = true;

                Assert.AreEqual(1, generationCount);
                Assert.IsTrue(regenerate.IsEnabled);
                Assert.IsTrue(replace.IsEnabled);
                Assert.AreEqual(new Thickness(-1), improve.Margin);
                Assert.AreEqual(1, Panel.GetZIndex(improve));
                Assert.AreEqual(
                    application.FindResource("AccentBrush"),
                    revisedPanel.BorderBrush);
                Assert.AreEqual(
                    application.FindResource("SuccessBrush"),
                    statusIcon.Foreground);

                regenerate.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                replace.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.AreEqual(2, generationCount);
                Assert.AreEqual(1, replacementCount);

                result.Close();
                information.Close();
                legalInformation.Close();
                launcher.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)));
        Assert.IsNull(failure, failure?.ToString());
    }

    private sealed class CaptureService : ISelectionCaptureService
    {
        public Task<SelectionCaptureResult> CaptureSelectedTextAsync(IntPtr targetWindow) =>
            Task.FromResult(SelectionCaptureResult.Fail("Test"));
    }

    private sealed class Formatter : ITextFormatter
    {
        public void Dispose()
        {
        }

        public Task<string> FormatAsync(
            FormatTextRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(request.Text);
    }

    private sealed class ReplacementService : ITextReplacementService
    {
        public Task<TextReplacementResult> ReplaceAsync(
            SelectionContext selection,
            string replacementText,
            CancellationToken cancellationToken) =>
            Task.FromResult(TextReplacementResult.Ok());
    }
}
