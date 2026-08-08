using System.Windows;
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
    public void LauncherAndInformationWindowLoadOnStaThread()
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

                Assert.AreEqual(56, launcher.Width);
                Assert.AreEqual(360, information.Width);
                Assert.AreEqual(360, information.Height);

                information.Close();
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
