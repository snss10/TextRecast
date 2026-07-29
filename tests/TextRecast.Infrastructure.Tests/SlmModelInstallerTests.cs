using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelInstallerTests
{
    [TestMethod]
    public void FindInstalledModelUsesInjectedStorageDirectories()
    {
        var modelBytes = new byte[] { 1, 2, 3, 4 };
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
            using var client = new HttpClient(new ModelHttpMessageHandler(modelBytes));
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
    public async Task DownloadAsyncUsesInjectedHttpClientAndUserDirectory()
    {
        var modelBytes = new byte[] { 10, 20, 30, 40, 50 };
        var testRoot = CreateTestDirectory();
        try
        {
            var handler = new ModelHttpMessageHandler(modelBytes);
            using var client = new HttpClient(handler);
            var profile = CreateProfile(modelBytes);
            var userDirectory = Path.Combine(testRoot, "user");
            var installer = new SlmModelInstaller(
                profile,
                client,
                Path.Combine(testRoot, "packaged"),
                userDirectory);

            var installedPath = await installer.DownloadAsync(
                progress: null,
                CancellationToken.None);

            Assert.AreEqual(installer.UserModelPath, installedPath);
            CollectionAssert.AreEqual(modelBytes, await File.ReadAllBytesAsync(installedPath));
            Assert.AreEqual(profile.DownloadUri, handler.RequestedUri);
            Assert.IsFalse(Directory.EnumerateFiles(userDirectory, "*.partial").Any());
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
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

    private sealed class ModelHttpMessageHandler(byte[] modelBytes) : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(modelBytes)
            });
        }
    }
}
