using System.IO;
using System.Security.Cryptography;
using TextRecast.Deployment.Setup;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Tests;

[TestClass]
public sealed class ModelSetupStateStoreTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset VerifiedAt =
        new(2026, 8, 2, 12, 5, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task SaveAndLoadAsyncRoundTripsVersionedActiveAndPendingState()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var profile = CreateProfile();
            var statePath = Path.Combine(testDirectory, "setup-state.json");
            var store = new ModelSetupStateStore(statePath, [profile]);
            var active = ModelSetupState.Default.ActivateVerifiedModel(profile, VerifiedAt);
            var pending = active.BeginPendingSetup(
                profile.Id,
                "0.3.0",
                StartedAt,
                Guid.Parse("86440b2f-e591-4af4-9671-b4cca28df60e"));

            await store.SaveAsync(pending);
            var loaded = await store.LoadAsync();

            Assert.AreEqual(pending, loaded);
            Assert.IsFalse(loaded.IsComplete);
            Assert.AreEqual(
                ActiveModelState.CurrentSchemaVersion,
                loaded.ActiveModel!.SchemaVersion);
            Assert.AreEqual(
                PendingModelSetupState.CurrentSchemaVersion,
                loaded.PendingSetup!.SchemaVersion);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void StateTransitionsPreserveActiveUntilVerifiedActivation()
    {
        var previous = CreateProfile("previous-model", [1, 2, 3]);
        var replacement = CreateProfile("replacement-model", [4, 5, 6]);
        var active = ModelSetupState.Default.ActivateVerifiedModel(previous, VerifiedAt);

        var pending = active.BeginPendingSetup(
            replacement.Id,
            "0.3.0",
            StartedAt,
            Guid.Parse("180ce3fd-c005-47af-8c73-188a4a715b19"));
        var downloading = pending.AdvancePendingSetup(
            PendingModelSetupStage.DownloadingModel,
            StartedAt.AddMinutes(1));
        var activated = downloading.ActivateVerifiedModel(
            replacement,
            VerifiedAt.AddMinutes(1));

        Assert.AreEqual(previous.Id, pending.ActiveModel!.ModelId);
        Assert.AreEqual(
            PendingModelSetupStage.DownloadingModel,
            downloading.PendingSetup!.Stage);
        Assert.AreEqual(replacement.Id, activated.ActiveModel!.ModelId);
        Assert.IsNull(activated.PendingSetup);
        Assert.IsTrue(activated.IsComplete);
    }

    [TestMethod]
    public async Task LoadAsyncRecoversFromCorruptStateButRejectsUnsupportedSchema()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var statePath = Path.Combine(testDirectory, "setup-state.json");
            var store = new ModelSetupStateStore(statePath, [CreateProfile()]);

            await File.WriteAllTextAsync(statePath, "{ definitely-not-json }");
            var corrupt = await store.LoadAsync();

            await File.WriteAllTextAsync(
                statePath,
                """
                {
                  "schemaVersion": 99,
                  "activeModel": null,
                  "pendingSetup": null
                }
                """);
            Assert.AreEqual(ModelSetupState.Default, corrupt);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => store.LoadAsync());
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsyncRecoversFromNullRequiredEvidence()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var statePath = Path.Combine(testDirectory, "setup-state.json");
            var store = new ModelSetupStateStore(statePath, [CreateProfile()]);
            await File.WriteAllTextAsync(
                statePath,
                """
                {
                  "schemaVersion": 1,
                  "activeModel": {
                    "schemaVersion": 1,
                    "modelId": null,
                    "expectedSha256": null,
                    "expectedFileSize": 4,
                    "sourceRevision": null,
                    "verifiedAtUtc": "2026-08-02T12:05:00+00:00"
                  },
                  "pendingSetup": null
                }
                """);

            var state = await store.LoadAsync();

            Assert.AreEqual(ModelSetupState.Default, state);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsyncRejectsUnsupportedNestedSchema()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var statePath = Path.Combine(testDirectory, "setup-state.json");
            var store = new ModelSetupStateStore(statePath, [CreateProfile()]);
            await File.WriteAllTextAsync(
                statePath,
                """
                {
                  "schemaVersion": 1,
                  "activeModel": {
                    "schemaVersion": 2,
                    "modelId": "test-model",
                    "expectedSha256": "9f64a747e1b97f131fabb6b447296c9b6f0201e79fb3c5356e6c77e89b6a806a",
                    "expectedFileSize": 4,
                    "sourceRevision": "test-revision",
                    "verifiedAtUtc": "2026-08-02T12:05:00+00:00"
                  },
                  "pendingSetup": null
                }
                """);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => store.LoadAsync());
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsyncPreservesExistingStateWhenAtomicReplacementFails()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var profile = CreateProfile();
            var statePath = Path.Combine(testDirectory, "setup-state.json");
            var store = new ModelSetupStateStore(statePath, [profile]);
            var original = ModelSetupState.Default.ActivateVerifiedModel(profile, VerifiedAt);
            await store.SaveAsync(original);

            await using (var lockedState = new FileStream(
                statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var pending = original.BeginPendingSetup(
                    profile.Id,
                    "0.3.0",
                    StartedAt);
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => store.SaveAsync(pending));
            }

            Assert.AreEqual(original, await store.LoadAsync());
            Assert.IsEmpty(Directory.GetFiles(testDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsyncRejectsCatalogEvidenceMismatch()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var profile = CreateProfile();
            var store = new ModelSetupStateStore(
                Path.Combine(testDirectory, "setup-state.json"),
                [profile]);
            var invalid = ModelSetupState.Default.ActivateVerifiedModel(
                profile with { ExpectedSha256 = new string('0', 64) },
                VerifiedAt);

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => store.SaveAsync(invalid));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static SlmModelProfile CreateProfile(
        string id = "test-model",
        byte[]? modelBytes = null)
    {
        modelBytes ??= [1, 2, 3, 4];
        return new SlmModelProfile
        {
            Id = id,
            DisplayName = id,
            Role = SlmModelRole.Fast,
            Description = "Setup-state test model.",
            LanguageSupport = "English",
            LimitationNotice = "Review test output.",
            IsExperimental = false,
            AdapterId = SlmRuntimeProfileIds.Qwen25AdapterId,
            PromptProfileId = SlmRuntimeProfileIds.Qwen25PromptProfileId,
            SamplingProfileId = SlmRuntimeProfileIds.GreedySamplingProfileId,
            FileName = $"{id}.gguf",
            DownloadUri = new Uri($"https://models.example.test/{id}.gguf"),
            ExpectedSha256 = Convert.ToHexStringLower(SHA256.HashData(modelBytes)),
            ExpectedFileSize = modelBytes.LongLength,
            SourceRepository = "example/test",
            SourceRevision = "test-revision",
            LicenseExpression = "Apache-2.0"
        };
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"TextRecast.SetupState.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
