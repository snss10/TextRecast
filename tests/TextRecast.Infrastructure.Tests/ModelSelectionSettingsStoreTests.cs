using System.IO;
using System.Text.Json;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Infrastructure.Tests;

[TestClass]
public sealed class ModelSelectionSettingsStoreTests
{
    private static readonly string[] KnownModelIds = ["fast-model", "quality-model"];
    private static readonly string[] PersistedPropertyNames =
        ["schemaVersion", "mode", "activeModelId"];

    [TestMethod]
    public async Task LoadAsyncReturnsAutomaticDefaultsOnFirstRun()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var store = new ModelSelectionSettingsStore(settingsPath, KnownModelIds);

            var settings = await store.LoadAsync();

            Assert.AreEqual(ModelSelectionMode.Automatic, settings.Mode);
            Assert.IsNull(settings.ActiveModelId);
            Assert.IsFalse(File.Exists(settingsPath));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsyncRoundTripsAValidSelection()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var saved = new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = "quality-model"
            };

            await new ModelSelectionSettingsStore(settingsPath, KnownModelIds)
                .SaveAsync(saved);
            var loaded = await new ModelSelectionSettingsStore(settingsPath, KnownModelIds)
                .LoadAsync();

            Assert.AreEqual(saved, loaded);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsyncRecoversFromAnUnknownModelIdentifier()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            await File.WriteAllTextAsync(
                settingsPath,
                """
                {
                  "schemaVersion": 1,
                  "mode": "Manual",
                  "activeModelId": "removed-model"
                }
                """);

            var loaded = await new ModelSelectionSettingsStore(settingsPath, KnownModelIds)
                .LoadAsync();

            Assert.AreEqual(ModelSelectionSettings.Default, loaded);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsyncRecoversFromCorruptOrOutdatedSettings()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var store = new ModelSelectionSettingsStore(settingsPath, KnownModelIds);

            await File.WriteAllTextAsync(settingsPath, "{ definitely-not-json }");
            var corrupt = await store.LoadAsync();

            await File.WriteAllTextAsync(
                settingsPath,
                """
                {
                  "schemaVersion": 0,
                  "mode": "Manual",
                  "activeModelId": "fast-model"
                }
                """);
            var outdated = await store.LoadAsync();

            Assert.AreEqual(ModelSelectionSettings.Default, corrupt);
            Assert.AreEqual(ModelSelectionSettings.Default, outdated);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsyncPreservesExistingSettingsWhenAtomicReplacementFails()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var store = new ModelSelectionSettingsStore(settingsPath, KnownModelIds);
            var original = new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Manual,
                ActiveModelId = "fast-model"
            };
            await store.SaveAsync(original);

            await using (var lockedSettings = new FileStream(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
            {
                var replacement = original with { ActiveModelId = "quality-model" };
                await Assert.ThrowsAsync<UnauthorizedAccessException>(
                    () => store.SaveAsync(replacement));
            }

            var loaded = await store.LoadAsync();
            Assert.AreEqual(original, loaded);
            Assert.IsEmpty(Directory.GetFiles(testDirectory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SavedDocumentContainsOnlyModelSelectionFields()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var settingsPath = Path.Combine(testDirectory, "settings.json");
            var store = new ModelSelectionSettingsStore(settingsPath, KnownModelIds);
            await store.SaveAsync(new ModelSelectionSettings
            {
                Mode = ModelSelectionMode.Automatic,
                ActiveModelId = "fast-model"
            });

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(settingsPath));
            var propertyNames = document.RootElement
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray();

            CollectionAssert.AreEquivalent(
                PersistedPropertyNames,
                propertyNames);
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAsyncRejectsInvalidManualSelection()
    {
        var testDirectory = CreateTestDirectory();
        try
        {
            var store = new ModelSelectionSettingsStore(
                Path.Combine(testDirectory, "settings.json"),
                KnownModelIds);

            await Assert.ThrowsExactlyAsync<ArgumentException>(
                () => store.SaveAsync(new ModelSelectionSettings
                {
                    Mode = ModelSelectionMode.Manual
                }));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TextRecast.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
