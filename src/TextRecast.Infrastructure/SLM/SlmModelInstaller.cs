using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace TextRecast.Infrastructure.SLM;

public sealed class SlmModelInstaller
{
    private const int BufferSize = 1024 * 1024;
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan ProgressReportInterval = TimeSpan.FromMilliseconds(125);
    private readonly Func<string, long> _availableDiskSpaceProvider;
    private readonly HttpClient _httpClient;
    private readonly string _packagedModelDirectory;
    private readonly SlmModelProfile _profile;
    private readonly Func<TimeSpan, CancellationToken, Task> _retryDelayAsync;
    private readonly string _userModelDirectory;

    public SlmModelInstaller(
        SlmModelProfile profile,
        HttpClient httpClient,
        string packagedModelDirectory,
        string userModelDirectory,
        Func<string, long>? availableDiskSpaceProvider = null,
        Func<TimeSpan, CancellationToken, Task>? retryDelayAsync = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagedModelDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userModelDirectory);

        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _packagedModelDirectory = packagedModelDirectory;
        _userModelDirectory = userModelDirectory;
        _availableDiskSpaceProvider = availableDiskSpaceProvider ?? GetAvailableDiskSpace;
        _retryDelayAsync = retryDelayAsync ?? Task.Delay;
    }

    public string PackagedModelPath =>
        Path.Combine(_packagedModelDirectory, _profile.FileName);

    public string UserModelPath =>
        Path.Combine(_userModelDirectory, _profile.FileName);

    public string PartialModelPath => $"{UserModelPath}.partial";

    public string PartialMetadataPath => $"{PartialModelPath}.metadata.json";

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
        Directory.CreateDirectory(_userModelDirectory);

        for (var retryCount = 0; ; retryCount++)
        {
            try
            {
                return await DownloadOnceAsync(progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                IsRetryable(ex) &&
                retryCount < MaxRetryCount &&
                !cancellationToken.IsCancellationRequested)
            {
                await _retryDelayAsync(
                    GetRetryDelay(retryCount),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> DownloadOnceAsync(
        IProgress<SlmModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var partialState = GetPartialDownloadState();

        while (true)
        {
            EnsureSufficientDiskSpace(partialState?.Length ?? 0);
            using var request = CreateDownloadRequest(partialState);
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (partialState is not null)
            {
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable &&
                    IsCompletedRangeResponse(response, partialState))
                {
                    return await VerifyAndInstallAsync(
                        progress,
                        cancellationToken).ConfigureAwait(false);
                }

                if (response.StatusCode == HttpStatusCode.PartialContent)
                {
                    if (!IsValidResumeResponse(response, partialState))
                    {
                        ResetPartialDownload();
                        partialState = null;
                        continue;
                    }

                    await CopyResponseToPartialFileAsync(
                        response,
                        append: true,
                        partialState.Length,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    return await VerifyAndInstallAsync(
                        progress,
                        cancellationToken).ConfigureAwait(false);
                }

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    ValidateFullResponse(response);
                    ResetPartialDownload();
                    await SavePartialMetadataAsync(response, cancellationToken).ConfigureAwait(false);
                    await CopyResponseToPartialFileAsync(
                        response,
                        append: false,
                        initialBytesDownloaded: 0,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    return await VerifyAndInstallAsync(
                        progress,
                        cancellationToken).ConfigureAwait(false);
                }

                response.EnsureSuccessStatusCode();
                throw new InvalidDataException("The model server returned an invalid resume response.");
            }

            response.EnsureSuccessStatusCode();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidDataException("The model server returned an invalid download response.");
            }

            ValidateFullResponse(response);
            await SavePartialMetadataAsync(response, cancellationToken).ConfigureAwait(false);
            await CopyResponseToPartialFileAsync(
                response,
                append: false,
                initialBytesDownloaded: 0,
                progress,
                cancellationToken).ConfigureAwait(false);
            return await VerifyAndInstallAsync(
                progress,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        if (exception is IncompleteDownloadException or HttpIOException)
        {
            return true;
        }

        if (exception is not HttpRequestException httpException)
        {
            return false;
        }

        return httpException.StatusCode is null ||
            httpException.StatusCode == HttpStatusCode.RequestTimeout ||
            httpException.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)httpException.StatusCode >= 500;
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        return TimeSpan.FromMilliseconds(250 * (1 << retryCount));
    }

    private HttpRequestMessage CreateDownloadRequest(PartialDownloadState? partialState)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, _profile.DownloadUri);
        if (partialState is null)
        {
            return request;
        }

        request.Headers.Range = new RangeHeaderValue(partialState.Length, to: null);
        if (partialState.Metadata.EntityTag is string entityTagValue &&
            EntityTagHeaderValue.TryParse(entityTagValue, out var entityTag))
        {
            request.Headers.IfRange = new RangeConditionHeaderValue(entityTag);
        }
        else if (partialState.Metadata.LastModified is DateTimeOffset lastModified)
        {
            request.Headers.IfRange = new RangeConditionHeaderValue(lastModified);
        }

        return request;
    }

    private PartialDownloadState? GetPartialDownloadState()
    {
        var partialExists = File.Exists(PartialModelPath);
        var metadataExists = File.Exists(PartialMetadataPath);
        if (!partialExists && !metadataExists)
        {
            return null;
        }

        if (!partialExists || !metadataExists)
        {
            ResetPartialDownload();
            return null;
        }

        PartialDownloadMetadata? metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<PartialDownloadMetadata>(
                File.ReadAllText(PartialMetadataPath));
        }
        catch (JsonException)
        {
            ResetPartialDownload();
            return null;
        }

        var partialLength = new FileInfo(PartialModelPath).Length;
        if (metadata is null ||
            partialLength <= 0 ||
            partialLength > _profile.ExpectedFileSize ||
            metadata.ExpectedFileSize != _profile.ExpectedFileSize ||
            !string.Equals(
                metadata.DownloadUri,
                _profile.DownloadUri.AbsoluteUri,
                StringComparison.Ordinal) ||
            !HasUsableValidator(metadata))
        {
            ResetPartialDownload();
            return null;
        }

        return new PartialDownloadState(partialLength, metadata);
    }

    private static bool HasUsableValidator(PartialDownloadMetadata metadata)
    {
        if (metadata.EntityTag is string entityTagValue &&
            EntityTagHeaderValue.TryParse(entityTagValue, out var entityTag) &&
            !entityTag.IsWeak)
        {
            return true;
        }

        return metadata.LastModified is not null;
    }

    private bool IsValidResumeResponse(
        HttpResponseMessage response,
        PartialDownloadState partialState)
    {
        var contentRange = response.Content.Headers.ContentRange;
        if (contentRange is null ||
            !string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase) ||
            contentRange.From != partialState.Length ||
            contentRange.To != _profile.ExpectedFileSize - 1 ||
            contentRange.Length != _profile.ExpectedFileSize)
        {
            return false;
        }

        var expectedRemainingBytes = _profile.ExpectedFileSize - partialState.Length;
        if (response.Content.Headers.ContentLength is long contentLength &&
            contentLength != expectedRemainingBytes)
        {
            return false;
        }

        return HasSameRemoteIdentity(response, partialState.Metadata);
    }

    private bool IsCompletedRangeResponse(
        HttpResponseMessage response,
        PartialDownloadState partialState)
    {
        var contentRange = response.Content.Headers.ContentRange;
        return partialState.Length == _profile.ExpectedFileSize &&
            contentRange is not null &&
            string.Equals(contentRange.Unit, "bytes", StringComparison.OrdinalIgnoreCase) &&
            contentRange.From is null &&
            contentRange.To is null &&
            contentRange.Length == _profile.ExpectedFileSize;
    }

    private static bool HasSameRemoteIdentity(
        HttpResponseMessage response,
        PartialDownloadMetadata metadata)
    {
        if (metadata.EntityTag is string entityTag)
        {
            return string.Equals(
                response.Headers.ETag?.ToString(),
                entityTag,
                StringComparison.Ordinal);
        }

        return metadata.LastModified is DateTimeOffset lastModified &&
            response.Content.Headers.LastModified == lastModified;
    }

    private void ValidateFullResponse(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength is long contentLength &&
            contentLength != _profile.ExpectedFileSize)
        {
            throw new InvalidDataException(
                "The model server returned an unexpected file size. Please try again later.");
        }
    }

    private async Task SavePartialMetadataAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var entityTag = response.Headers.ETag is { IsWeak: false } strongEntityTag
            ? strongEntityTag.ToString()
            : null;
        var metadata = new PartialDownloadMetadata(
            _profile.DownloadUri.AbsoluteUri,
            _profile.ExpectedFileSize,
            entityTag,
            response.Content.Headers.LastModified);
        var json = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(
            PartialMetadataPath,
            json,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task CopyResponseToPartialFileAsync(
        HttpResponseMessage response,
        bool append,
        long initialBytesDownloaded,
        IProgress<SlmModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var destination = new FileStream(
            PartialModelPath,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: BufferSize,
            useAsync: true);

        var buffer = new byte[BufferSize];
        var progressTimer = Stopwatch.StartNew();
        var downloaded = initialBytesDownloaded;
        var lastReportedBytes = -1L;
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
                progress.Report(new SlmModelDownloadProgress(
                    downloaded,
                    _profile.ExpectedFileSize));
                lastReportedBytes = downloaded;
                progressTimer.Restart();
            }
        }

        if (progress is not null && lastReportedBytes != downloaded)
        {
            progress.Report(new SlmModelDownloadProgress(
                downloaded,
                _profile.ExpectedFileSize));
        }

        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        destination.Close();

        var actualLength = new FileInfo(PartialModelPath).Length;
        if (actualLength > _profile.ExpectedFileSize)
        {
            ResetPartialDownload();
            throw new InvalidDataException(
                "The downloaded model has an unexpected file size. Please try again.");
        }

        if (actualLength < _profile.ExpectedFileSize)
        {
            throw new IncompleteDownloadException(
                "The downloaded model is incomplete. Check your connection and try again.");
        }
    }

    private async Task<string> VerifyAndInstallAsync(
        IProgress<SlmModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new SlmModelDownloadProgress(
            _profile.ExpectedFileSize,
            _profile.ExpectedFileSize,
            SlmModelInstallationStage.Verifying));
        try
        {
            await VerifyHashAsync(PartialModelPath, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            ResetPartialDownload();
            throw;
        }

        File.Move(PartialModelPath, UserModelPath, overwrite: true);
        TryDeleteFile(PartialMetadataPath);
        return UserModelPath;
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
            bufferSize: BufferSize,
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

    private void ResetPartialDownload()
    {
        TryDeleteFile(PartialModelPath);
        TryDeleteFile(PartialMetadataPath);
    }

    private void EnsureSufficientDiskSpace(long existingPartialBytes)
    {
        var requiredBytes = _profile.ExpectedFileSize - existingPartialBytes;
        var availableBytes = _availableDiskSpaceProvider(_userModelDirectory);
        if (availableBytes < requiredBytes)
        {
            throw new IOException(
                "There is not enough free disk space to download the local model.");
        }
    }

    private static long GetAvailableDiskSpace(string directory)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new IOException("The model storage drive could not be determined.");
        }

        return new DriveInfo(root).AvailableFreeSpace;
    }

    private static void TryDeleteFile(string path)
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

    private sealed record PartialDownloadState(
        long Length,
        PartialDownloadMetadata Metadata);

    private sealed record PartialDownloadMetadata(
        string DownloadUri,
        long ExpectedFileSize,
        string? EntityTag,
        DateTimeOffset? LastModified);

    private sealed class IncompleteDownloadException(string message) : IOException(message);
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
