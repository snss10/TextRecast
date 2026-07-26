using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelInstaller
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly SlmModelOptions _options;

    public SlmModelInstaller(SlmModelOptions options)
    {
        _options = options;
    }

    public string PackagedModelPath =>
        Path.Combine(AppContext.BaseDirectory, "Models", _options.ModelFileName);

    public string UserModelPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TextRecast",
            "Models",
            _options.ModelFileName);

    public string? FindInstalledModel()
    {
        if (HasExpectedSize(PackagedModelPath))
        {
            return PackagedModelPath;
        }

        return HasExpectedSize(UserModelPath) ? UserModelPath : null;
    }

    public async Task<string> DownloadAsync(
        IProgress<SlmModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var destinationPath = UserModelPath;
        var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
        Directory.CreateDirectory(destinationDirectory);

        var partialPath = Path.Combine(
            destinationDirectory,
            $"{_options.ModelFileName}.{Guid.NewGuid():N}.partial");

        try
        {
            using var response = await HttpClient.GetAsync(
                _options.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var reportedLength = response.Content.Headers.ContentLength;
            if (reportedLength is long length && length != _options.ExpectedModelFileSize)
            {
                throw new InvalidDataException(
                    "The model server returned an unexpected file size. Please try again later.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);

            var buffer = new byte[1024 * 1024];
            long downloaded = 0;
            while (true)
            {
                var bytesRead = await source.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloaded += bytesRead;
                progress?.Report(new SlmModelDownloadProgress(
                    downloaded,
                    reportedLength ?? _options.ExpectedModelFileSize));
            }

            await destination.FlushAsync(cancellationToken);
            destination.Close();

            if (!HasExpectedSize(partialPath))
            {
                throw new InvalidDataException(
                    "The downloaded model is incomplete. Check your connection and try again.");
            }

            await VerifyHashAsync(partialPath, cancellationToken);
            File.Move(partialPath, destinationPath, overwrite: true);
            return destinationPath;
        }
        finally
        {
            TryDeletePartialFile(partialPath);
        }
    }

    private bool HasExpectedSize(string path)
    {
        return File.Exists(path) && new FileInfo(path).Length == _options.ExpectedModelFileSize;
    }

    private async Task VerifyHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (!Convert.ToHexStringLower(hash).Equals(_options.ExpectedModelSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The downloaded model failed its integrity check. Please try again.");
        }
    }

    private static void TryDeletePartialFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TextRecast/1.0");
        return client;
    }
}

public sealed record SlmModelDownloadProgress(long BytesDownloaded, long? TotalBytes)
{
    public double? Percentage => TotalBytes > 0
        ? Math.Clamp((double)BytesDownloaded / TotalBytes.Value * 100, 0, 100)
        : null;
}
