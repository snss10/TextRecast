using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelInstaller
{
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(125);
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private readonly SlmModelProfile _profile;

    public SlmModelInstaller(SlmModelProfile profile)
    {
        _profile = profile;
    }

    public string PackagedModelPath =>
        Path.Combine(AppContext.BaseDirectory, "Models", _profile.FileName);

    public string UserModelPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TextRecast",
            "Models",
            _profile.FileName);

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
            $"{_profile.FileName}.{Guid.NewGuid():N}.partial");

        try
        {
            using var response = await HttpClient.GetAsync(
                _profile.DownloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var reportedLength = response.Content.Headers.ContentLength;
            if (reportedLength is long length && length != _profile.ExpectedFileSize)
            {
                throw new InvalidDataException(
                    "The model server returned an unexpected file size. Please try again later.");
            }

            await using var source = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 1024,
                useAsync: true);

            var buffer = new byte[1024 * 1024];
            var progressTimer = Stopwatch.StartNew();
            var totalBytes = reportedLength ?? _profile.ExpectedFileSize;
            long downloaded = 0;
            long lastReportedBytes = -1;
            while (true)
            {
                var bytesRead = await source
                    .ReadAsync(buffer, cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    break;
                }

                await destination
                    .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
                downloaded += bytesRead;

                if (progress is not null && progressTimer.Elapsed >= ProgressReportInterval)
                {
                    progress.Report(new SlmModelDownloadProgress(downloaded, totalBytes));
                    lastReportedBytes = downloaded;
                    progressTimer.Restart();
                }
            }

            if (progress is not null && lastReportedBytes != downloaded)
            {
                progress.Report(new SlmModelDownloadProgress(downloaded, totalBytes));
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            destination.Close();

            if (!HasExpectedSize(partialPath))
            {
                throw new InvalidDataException(
                    "The downloaded model is incomplete. Check your connection and try again.");
            }

            progress?.Report(new SlmModelDownloadProgress(
                downloaded,
                totalBytes,
                SlmModelInstallationStage.Verifying));
            await VerifyHashAsync(partialPath, cancellationToken).ConfigureAwait(false);
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
        return File.Exists(path) && new FileInfo(path).Length == _profile.ExpectedFileSize;
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
        var hash = await SHA256
            .HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        if (!Convert.ToHexStringLower(hash).Equals(_profile.ExpectedSha256, StringComparison.Ordinal))
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

public enum SlmModelInstallationStage
{
    Downloading,
    Verifying
}

public sealed record SlmModelDownloadProgress(
    long BytesDownloaded,
    long? TotalBytes,
    SlmModelInstallationStage Stage = SlmModelInstallationStage.Downloading)
{
    public double? Percentage => TotalBytes > 0
        ? Math.Clamp((double)BytesDownloaded / TotalBytes.Value * 100, 0, 100)
        : null;
}
