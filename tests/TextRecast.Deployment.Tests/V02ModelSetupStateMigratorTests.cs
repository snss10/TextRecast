using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using TextRecast.Deployment.Setup;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Tests;

[TestClass]
public sealed class V02ModelSetupStateMigratorTests
{
    private static readonly byte[] ModelBytes = [1, 3, 5, 7, 9];
    private static readonly DateTimeOffset MigrationTime =
        new(2026, 8, 2, 15, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task LoadOrMigrateAsyncActivatesOnlyHashVerifiedV02Selection()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            using var fixture = CreateFixture(testRoot);
            Directory.CreateDirectory(fixture.UserModelDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(fixture.UserModelDirectory, fixture.Profile.FileName),
                ModelBytes);
            await File.WriteAllTextAsync(
                fixture.LegacyStore.SettingsPath,
                """
                {
                  "schemaVersion": 1,
                  "mode": "Manual",
                  "activeModelId": "Qwen2.5-1.5B-Instruct-Q4_K_M"
                }
                """);

            var migration = await fixture.Migrator.LoadOrMigrateWithResultAsync();
            var migrated = migration.State;

            Assert.AreEqual(fixture.Profile.Id, migrated.ActiveModel!.ModelId);
            Assert.AreEqual(
                Path.Combine(fixture.UserModelDirectory, fixture.Profile.FileName),
                migration.VerifiedLegacyModelPath);
            Assert.AreEqual(MigrationTime, migrated.ActiveModel.VerifiedAtUtc);
            Assert.IsNull(migrated.PendingSetup);
            Assert.IsTrue(migrated.IsComplete);
            Assert.IsTrue(File.Exists(fixture.StateStore.StatePath));
            Assert.AreEqual(0, fixture.Handler.RequestCount);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadOrMigrateAsyncRejectsSameSizeWrongHashWithoutDeletingIt()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            using var fixture = CreateFixture(testRoot);
            Directory.CreateDirectory(fixture.UserModelDirectory);
            var modelPath = Path.Combine(
                fixture.UserModelDirectory,
                fixture.Profile.FileName);
            await File.WriteAllBytesAsync(modelPath, [9, 7, 5, 3, 1]);
            await fixture.LegacyStore.SaveAsync(new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = fixture.Profile.Id
            });

            var migrated = await fixture.Migrator.LoadOrMigrateAsync();

            Assert.IsNull(migrated.ActiveModel);
            Assert.IsFalse(migrated.IsComplete);
            Assert.IsTrue(File.Exists(modelPath));
            Assert.AreEqual(0, fixture.Handler.RequestCount);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task ExistingStatePreventsStaleV02Migration()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            using var fixture = CreateFixture(testRoot);
            Directory.CreateDirectory(fixture.UserModelDirectory);
            await File.WriteAllBytesAsync(
                Path.Combine(fixture.UserModelDirectory, fixture.Profile.FileName),
                ModelBytes);
            await fixture.LegacyStore.SaveAsync(new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = fixture.Profile.Id
            });
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.StateStore.StatePath)!);
            await File.WriteAllTextAsync(
                fixture.StateStore.StatePath,
                "{ corrupt-new-state }");

            var loaded = await fixture.Migrator.LoadOrMigrateAsync();

            Assert.AreEqual(ModelSetupState.Default, loaded);
            Assert.AreEqual(0, fixture.Handler.RequestCount);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task MigrationPreservesPartialDownloadAndIsIdempotent()
    {
        var testRoot = CreateTestDirectory();
        try
        {
            using var fixture = CreateFixture(testRoot);
            var installer = fixture.Installations.GetInstaller(fixture.Profile.Id);
            Directory.CreateDirectory(fixture.UserModelDirectory);
            await File.WriteAllBytesAsync(installer.PartialModelPath, [1, 3]);

            var first = await fixture.Migrator.LoadOrMigrateAsync();
            await fixture.LegacyStore.SaveAsync(new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = fixture.Profile.Id
            });
            var second = await fixture.Migrator.LoadOrMigrateAsync();

            Assert.AreEqual(ModelSetupState.Default, first);
            Assert.AreEqual(first, second);
            Assert.IsTrue(File.Exists(installer.PartialModelPath));
            Assert.AreEqual(0, fixture.Handler.RequestCount);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static MigrationFixture CreateFixture(string testRoot)
    {
        var profile = CreateProfile();
        var legacyStore = new ModelSelectionSettingsStore(
            Path.Combine(testRoot, "settings.json"),
            [profile.Id]);
        var stateStore = new ModelSetupStateStore(
            Path.Combine(testRoot, "setup-state.json"),
            [profile]);
        var handler = new NoRequestHttpMessageHandler();
        var client = new HttpClient(handler);
        var packagedDirectory = Path.Combine(testRoot, "packaged");
        var userDirectory = Path.Combine(testRoot, "user");
        var installations = new SlmModelInstallationSet(
            [profile],
            client,
            packagedDirectory,
            userDirectory);
        var migrator = new V02ModelSetupStateMigrator(
            stateStore,
            legacyStore,
            installations,
            [profile],
            new FixedTimeProvider(MigrationTime));

        return new MigrationFixture(
            profile,
            legacyStore,
            stateStore,
            installations,
            migrator,
            handler,
            client,
            userDirectory);
    }

    private static SlmModelProfile CreateProfile() => new()
    {
        Id = "Qwen2.5-1.5B-Instruct-Q4_K_M",
        DisplayName = "v0.2 model",
        Role = SlmModelRole.Fast,
        Description = "Migration test model.",
        LanguageSupport = "English",
        LimitationNotice = "Review test output.",
        IsExperimental = false,
        AdapterId = SlmRuntimeProfileIds.Qwen25AdapterId,
        PromptProfileId = SlmRuntimeProfileIds.Qwen25PromptProfileId,
        SamplingProfileId = SlmRuntimeProfileIds.GreedySamplingProfileId,
        FileName = "v02-model.gguf",
        DownloadUri = new Uri("https://models.example.test/v02-model.gguf"),
        ExpectedSha256 = Convert.ToHexStringLower(SHA256.HashData(ModelBytes)),
        ExpectedFileSize = ModelBytes.LongLength,
        SourceRepository = "example/test",
        SourceRevision = "v02-revision",
        LicenseExpression = "Apache-2.0"
    };

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.V02Migration.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record MigrationFixture(
        SlmModelProfile Profile,
        ModelSelectionSettingsStore LegacyStore,
        ModelSetupStateStore StateStore,
        SlmModelInstallationSet Installations,
        V02ModelSetupStateMigrator Migrator,
        NoRequestHttpMessageHandler Handler,
        HttpClient Client,
        string UserModelDirectory) : IDisposable
    {
        public void Dispose()
        {
            Client.Dispose();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class NoRequestHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException("Migration must not send model requests.");
        }
    }
}
