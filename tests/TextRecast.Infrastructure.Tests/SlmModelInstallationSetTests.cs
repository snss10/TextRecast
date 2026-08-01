using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class SlmModelInstallationSetTests
{
    private static readonly byte[] FastModelBytes = [1, 2, 3, 4];
    private static readonly byte[] QualityModelBytes = [5, 6, 7, 8, 9];

    [TestMethod]
    public void ReviewingChoicesAndDiscoveringModelsSendsNoHttpRequests()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            var profiles = CreateProfiles();
            using var handler = new RoutingHttpMessageHandler(profiles);
            using var client = new HttpClient(handler);
            var installations = CreateInstallationSet(profiles, client, testRoot);

            _ = SlmModelSetupPlanner.CreateChoices(
                profiles,
                hardware: null,
                installations.FindInstalledModels().Keys,
                profiles[0].Id);

            Assert.IsEmpty(handler.RequestUris);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadingSelectionRequestsAndInstallsOnlySelectedModel()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            var profiles = CreateProfiles();
            using var handler = new RoutingHttpMessageHandler(profiles);
            using var client = new HttpClient(handler);
            var installations = CreateInstallationSet(profiles, client, testRoot);
            var selectedProfile = profiles[1];

            var installedPath = await installations
                .GetInstaller(selectedProfile.Id)
                .DownloadAsync(progress: null, CancellationToken.None);

            Assert.HasCount(1, handler.RequestUris);
            Assert.AreEqual(selectedProfile.DownloadUri, handler.RequestUris[0]);
            CollectionAssert.AreEqual(
                QualityModelBytes,
                await File.ReadAllBytesAsync(installedPath));
            Assert.IsFalse(File.Exists(Path.Combine(
                testRoot,
                "user",
                profiles[0].FileName)));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void InstalledSelectionIsDiscoveredWithoutNetworkAccess()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            var profiles = CreateProfiles();
            var userDirectory = Path.Combine(testRoot, "user");
            Directory.CreateDirectory(userDirectory);
            File.WriteAllBytes(Path.Combine(userDirectory, profiles[0].FileName), FastModelBytes);
            using var handler = new RoutingHttpMessageHandler(profiles);
            using var client = new HttpClient(handler);
            var installations = CreateInstallationSet(profiles, client, testRoot);

            var installed = installations.FindInstalledModels();

            Assert.HasCount(1, installed);
            Assert.IsTrue(installed.ContainsKey(profiles[0].Id));
            Assert.IsEmpty(handler.RequestUris);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public void UnknownSelectionCannotResolveAnInstaller()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            var profiles = CreateProfiles();
            using var handler = new RoutingHttpMessageHandler(profiles);
            using var client = new HttpClient(handler);
            var installations = CreateInstallationSet(profiles, client, testRoot);

            Assert.ThrowsExactly<ArgumentException>(() =>
                installations.GetInstaller("unknown-model"));
            Assert.IsEmpty(handler.RequestUris);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static SlmModelInstallationSet CreateInstallationSet(
        SlmModelProfile[] profiles,
        HttpClient client,
        string testRoot) => new(
            profiles,
            client,
            Path.Combine(testRoot, "packaged"),
            Path.Combine(testRoot, "user"));

    private static SlmModelProfile[] CreateProfiles() =>
    [
        CreateProfile("fast", FastModelBytes),
        CreateProfile("quality", QualityModelBytes)
    ];

    private static SlmModelProfile CreateProfile(string id, byte[] modelBytes) => new()
    {
        Id = id,
        DisplayName = $"{id} model",
        Role = SlmModelRole.Fast,
        Description = "Setup integration test model.",
        LanguageSupport = "English",
        LimitationNotice = "Review test output.",
        IsExperimental = true,
        AdapterId = Qwen25ModelAdapter.AdapterId,
        PromptProfileId = "test-prompt-v1",
        SamplingProfileId = "greedy-v1",
        FileName = $"{id}.gguf",
        DownloadUri = new Uri($"https://models.example.test/{id}.gguf"),
        ExpectedSha256 = Convert.ToHexStringLower(SHA256.HashData(modelBytes)),
        ExpectedFileSize = modelBytes.LongLength,
        SourceRepository = "example/test",
        SourceRevision = "test-revision",
        LicenseExpression = "Apache-2.0"
    };

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.InstallationSet.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class RoutingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<Uri, byte[]> contentByUri;

        public RoutingHttpMessageHandler(IEnumerable<SlmModelProfile> profiles)
        {
            contentByUri = profiles.ToDictionary(
                profile => profile.DownloadUri,
                profile => profile.Id.Equals("fast", StringComparison.Ordinal)
                    ? FastModelBytes
                    : QualityModelBytes);
        }

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requestUri = request.RequestUri ??
                throw new InvalidOperationException("The test request has no URI.");
            RequestUris.Add(requestUri);
            if (!contentByUri.TryGetValue(requestUri, out var modelBytes))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(modelBytes)
            });
        }
    }
}
