using System.IO;
using System.Windows;
using TextRecast.Infrastructure.Windows.Clipboard;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
[DoNotParallelize]
public sealed class WindowsClipboardIntegrationTests
{
    private const string CustomBinaryFormat = "TextRecast.Tests.BinarySnapshot";

    [STATestMethod(UseSTASynchronizationContext = true)]
    public async Task CaptureSetAndRestorePreservesIndependentClipboardFormats()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());

        var userClipboard = await WindowsClipboard.CaptureSnapshotAsync();
        if (!userClipboard.WasCaptured)
        {
            Assert.Inconclusive("The Windows clipboard was unavailable before the test.");
        }

        try
        {
            const string originalText = "TextRecast clipboard snapshot";
            byte[] originalBinary = [0x00, 0x19, 0x7F, 0x80, 0xFF];
            var originalData = new DataObject();
            originalData.SetData(DataFormats.UnicodeText, originalText, autoConvert: false);
            using (var binaryStream = new MemoryStream(originalBinary, writable: false))
            {
                originalData.SetData(CustomBinaryFormat, binaryStream, autoConvert: false);
                global::System.Windows.Clipboard.SetDataObject(originalData, copy: true);
            }

            var snapshot = await WindowsClipboard.CaptureSnapshotAsync();
            Assert.IsTrue(snapshot.WasCaptured);

            Assert.IsTrue(await WindowsClipboard.SetTextAsync(
                "Temporary replacement text",
                CancellationToken.None));
            var temporaryText = await WindowsClipboard.ReadTextAsync(1_024, CancellationToken.None);
            Assert.IsTrue(temporaryText.Success);
            Assert.AreEqual("Temporary replacement text", temporaryText.Text);

            Assert.IsTrue(await WindowsClipboard.RestoreAsync(snapshot));

            var restoredData = global::System.Windows.Clipboard.GetDataObject();
            Assert.IsNotNull(restoredData);
            Assert.AreEqual(
                originalText,
                restoredData.GetData(DataFormats.UnicodeText, autoConvert: false));

            var restoredBinary = restoredData.GetData(CustomBinaryFormat, autoConvert: false);
            Assert.IsInstanceOfType<Stream>(restoredBinary);
            using var restoredStream = (Stream)restoredBinary!;
            using var restoredBytes = new MemoryStream();
            restoredStream.CopyTo(restoredBytes);
            CollectionAssert.AreEqual(originalBinary, restoredBytes.ToArray());
        }
        finally
        {
            Assert.IsTrue(
                await WindowsClipboard.RestoreAsync(userClipboard),
                "The clipboard used before the test could not be restored.");
        }
    }
}
