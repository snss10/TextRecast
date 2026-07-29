using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelInstallerTests
{
    private const int ResumeOffset = 3;
    private const string Version1EntityTag = "\"version-1\"";
    private const string Version2EntityTag = "\"version-2\"";

    [TestMethod]
    public void FindInstalledModelUsesInjectedStorageDirectories()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var packagedDirectory = Path.Combine(testRoot, "packaged");
            var userDirectory = Path.Combine(testRoot, "user");
            Directory.CreateDirectory(packagedDirectory);
            Directory.CreateDirectory(userDirectory);
            var profile = CreateProfile(modelBytes);
            File.WriteAllBytes(Path.Combine(packagedDirectory, profile.FileName), modelBytes);
            File.WriteAllBytes(Path.Combine(userDirectory, profile.FileName), modelBytes);
            using var client = new HttpClient(new RecordingHttpMessageHandler());
            var installer = new SlmModelInstaller(
                profile,
                client,
                packagedDirectory,
                userDirectory);

            var installedPath = installer.FindInstalledModel();

            Assert.AreEqual(installer.PackagedModelPath, installedPath);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncDownloadsFreshModel()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateFullResponse(modelBytes, Version1EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            Assert.AreEqual(1, handler.Requests.Count);
            Assert.IsNull(handler.Requests[0].RangeStart);
            Assert.IsFalse(File.Exists(installer.PartialModelPath));
            Assert.IsFalse(File.Exists(installer.PartialMetadataPath));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncResumesAfterTransientReadFailure()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateInterruptedResponse(
                    modelBytes,
                    Version1EntityTag,
                    new IOException("Connection interrupted.")),
                () => CreatePartialResponse(
                    modelBytes,
                    ResumeOffset,
                    Version1EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);

            await Assert.ThrowsExactlyAsync<IOException>(
                () => installer.DownloadAsync(null, CancellationToken.None));
            Assert.AreEqual(ResumeOffset, new FileInfo(installer.PartialModelPath).Length);

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            AssertResumeRequest(handler.Requests[1]);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncRestartsWhenServerIgnoresRange()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateInterruptedResponse(
                    modelBytes,
                    Version1EntityTag,
                    new IOException("Connection interrupted.")),
                () => CreateFullResponse(modelBytes, Version1EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);
            await Assert.ThrowsExactlyAsync<IOException>(
                () => installer.DownloadAsync(null, CancellationToken.None));

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            AssertResumeRequest(handler.Requests[1]);
            Assert.AreEqual(2, handler.Requests.Count);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncRestartsAfterMismatchedContentRange()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateInterruptedResponse(
                    modelBytes,
                    Version1EntityTag,
                    new IOException("Connection interrupted.")),
                () => CreatePartialResponse(
                    modelBytes,
                    ResumeOffset,
                    Version1EntityTag,
                    reportedStart: ResumeOffset + 1),
                () => CreateFullResponse(modelBytes, Version1EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);
            await Assert.ThrowsExactlyAsync<IOException>(
                () => installer.DownloadAsync(null, CancellationToken.None));

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            AssertResumeRequest(handler.Requests[1]);
            Assert.IsNull(handler.Requests[2].RangeStart);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncRestartsAfterEntityTagChanges()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateInterruptedResponse(
                    modelBytes,
                    Version1EntityTag,
                    new IOException("Connection interrupted.")),
                () => CreatePartialResponse(
                    modelBytes,
                    ResumeOffset,
                    Version2EntityTag),
                () => CreateFullResponse(modelBytes, Version2EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);
            await Assert.ThrowsExactlyAsync<IOException>(
                () => installer.DownloadAsync(null, CancellationToken.None));

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            AssertResumeRequest(handler.Requests[1]);
            Assert.IsNull(handler.Requests[2].RangeStart);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadAsyncPreservesCancelledDownloadAndResumes()
    {
        var modelBytes = CreateModelBytes();
        var testRoot = CreateTestDirectory();
        using var cancellation = new CancellationTokenSource();
        try
        {
            var handler = new RecordingHttpMessageHandler(
                () => CreateCancelledResponse(
                    modelBytes,
                    Version1EntityTag,
                    cancellation),
                () => CreatePartialResponse(
                    modelBytes,
                    ResumeOffset,
                    Version1EntityTag));
            using var client = new HttpClient(handler);
            var installer = CreateInstaller(modelBytes, client, testRoot);

            try
            {
                await installer.DownloadAsync(null, cancellation.Token);
                Assert.Fail("The interrupted download should have been cancelled.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }

            Assert.IsTrue(File.Exists(installer.PartialMetadataPath));
            Assert.AreEqual(ResumeOffset, new FileInfo(installer.PartialModelPath).Length);

            var installedPath = await installer.DownloadAsync(null, CancellationToken.None);

            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            AssertResumeRequest(handler.Requests[1]);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static byte[] CreateModelBytes()
    {
        return new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
    }

    private static SlmModelInstaller CreateInstaller(
        byte[] modelBytes,
        HttpClient client,
        string testRoot)
    {
        return new SlmModelInstaller(
            CreateProfile(modelBytes),
            client,
            Path.Combine(testRoot, "packaged"),
            Path.Combine(testRoot, "user"));
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static SlmModelProfile CreateProfile(byte[] modelBytes)
    {
        return new SlmModelProfile
        {
            Id = "test-model",
            FileName = "test-model.gguf",
            DownloadUri = new Uri("https://models.example.test/test-model.gguf"),
            ExpectedSha256 = Convert.ToHexStringLower(SHA256.HashData(modelBytes)),
            ExpectedFileSize = modelBytes.LongLength
        };
    }

    private static HttpResponseMessage CreateFullResponse(
        byte[] modelBytes,
        string entityTag)
    {
        return CreateResponse(
            HttpStatusCode.OK,
            new ByteArrayContent(modelBytes),
            entityTag);
    }

    private static HttpResponseMessage CreatePartialResponse(
        byte[] modelBytes,
        int actualStart,
        string entityTag,
        int? reportedStart = null)
    {
        var content = new ByteArrayContent(modelBytes[actualStart..]);
        content.Headers.ContentRange = new ContentRangeHeaderValue(
            reportedStart ?? actualStart,
            modelBytes.LongLength - 1,
            modelBytes.LongLength);
        return CreateResponse(HttpStatusCode.PartialContent, content, entityTag);
    }

    private static HttpResponseMessage CreateInterruptedResponse(
        byte[] modelBytes,
        string entityTag,
        IOException exception)
    {
        var stream = new InterruptingReadStream(
            modelBytes,
            ResumeOffset,
            exception,
            cancellation: null);
        var content = new StreamContent(stream);
        content.Headers.ContentLength = modelBytes.LongLength;
        return CreateResponse(HttpStatusCode.OK, content, entityTag);
    }

    private static HttpResponseMessage CreateCancelledResponse(
        byte[] modelBytes,
        string entityTag,
        CancellationTokenSource cancellation)
    {
        var stream = new InterruptingReadStream(
            modelBytes,
            ResumeOffset,
            failure: null,
            cancellation);
        var content = new StreamContent(stream);
        content.Headers.ContentLength = modelBytes.LongLength;
        return CreateResponse(HttpStatusCode.OK, content, entityTag);
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        HttpContent content,
        string entityTag)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = content
        };
        response.Headers.ETag = EntityTagHeaderValue.Parse(entityTag);
        return response;
    }

    private static void AssertResumeRequest(RecordedRequest request)
    {
        Assert.AreEqual(ResumeOffset, request.RangeStart);
        Assert.IsNull(request.RangeEnd);
        Assert.AreEqual(Version1EntityTag, request.IfRange);
    }

    private sealed class RecordingHttpMessageHandler(
        params Func<HttpResponseMessage>[] responseFactories) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responseFactories =
            new(responseFactories);

        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var range = request.Headers.Range?.Ranges.SingleOrDefault();
            Requests.Add(new RecordedRequest(
                request.RequestUri,
                range?.From,
                range?.To,
                request.Headers.IfRange?.ToString()));

            if (!_responseFactories.TryDequeue(out var responseFactory))
            {
                throw new InvalidOperationException("No fake HTTP response was configured.");
            }

            return Task.FromResult(responseFactory());
        }
    }

    private sealed record RecordedRequest(
        Uri? Uri,
        long? RangeStart,
        long? RangeEnd,
        string? IfRange);

    private sealed class InterruptingReadStream(
        byte[] modelBytes,
        int bytesBeforeInterruption,
        IOException? failure,
        CancellationTokenSource? cancellation) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => modelBytes.LongLength;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadCore(buffer.AsSpan(offset, count), CancellationToken.None);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ReadCore(
                buffer.AsSpan(offset, count),
                cancellationToken));
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReadCore(buffer.Span, cancellationToken));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private int ReadCore(Span<byte> buffer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position >= bytesBeforeInterruption)
            {
                if (cancellation is not null)
                {
                    cancellation.Cancel();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                throw failure ?? new IOException("The fake response was interrupted.");
            }

            var count = Math.Min(buffer.Length, bytesBeforeInterruption - _position);
            modelBytes.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
    }
}
