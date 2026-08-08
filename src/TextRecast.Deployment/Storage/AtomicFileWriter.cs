using System.IO;
using System.Text;

namespace TextRecast.Deployment.Storage;

internal static class AtomicFileWriter
{
    private static readonly Encoding Utf8WithoutByteOrderMark = new UTF8Encoding(false);

    public static async Task WriteAsync(
        string destinationPath,
        string content,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(destinationDirectory))
        {
            throw new ArgumentException(
                "The destination path must include a directory.",
                nameof(destinationPath));
        }

        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var bytes = Utf8WithoutByteOrderMark.GetBytes(content);
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
