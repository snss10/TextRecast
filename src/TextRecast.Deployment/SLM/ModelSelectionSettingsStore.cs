using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextRecast.Deployment.Storage;

namespace TextRecast.Infrastructure.SLM;

public enum ModelSelectionMode
{
    Automatic,
    Manual
}

public sealed record ModelSelectionSettings
{
    public const int CurrentSchemaVersion = 1;

    public static ModelSelectionSettings Default { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public ModelSelectionMode Mode { get; init; } = ModelSelectionMode.Automatic;
    public string? ActiveModelId { get; init; }
}

public sealed class ModelSelectionSettingsStore
{
    private const string ApplicationDirectoryName = "TextRecast";
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly HashSet<string> knownModelIds;

    public ModelSelectionSettingsStore(IEnumerable<string> knownModelIds)
        : this(GetDefaultSettingsPath(), knownModelIds)
    {
    }

    internal ModelSelectionSettingsStore(
        string settingsPath,
        IEnumerable<string> knownModelIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        ArgumentNullException.ThrowIfNull(knownModelIds);

        SettingsPath = Path.GetFullPath(settingsPath);
        this.knownModelIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var modelId in knownModelIds)
        {
            if (string.IsNullOrWhiteSpace(modelId))
            {
                throw new ArgumentException(
                    "Known model identifiers cannot be null, empty, or whitespace.",
                    nameof(knownModelIds));
            }

            this.knownModelIds.Add(modelId);
        }
    }

    public string SettingsPath { get; }

    public async Task<ModelSelectionSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(SettingsPath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return ModelSelectionSettings.Default;
        }
        catch (DirectoryNotFoundException)
        {
            return ModelSelectionSettings.Default;
        }

        ModelSelectionSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<ModelSelectionSettings>(
                json,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return ModelSelectionSettings.Default;
        }
        catch (NotSupportedException)
        {
            return ModelSelectionSettings.Default;
        }

        return IsValid(settings)
            ? settings!
            : ModelSelectionSettings.Default;
    }

    public async Task SaveAsync(
        ModelSelectionSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!IsValid(settings))
        {
            throw new ArgumentException(
                "The model selection settings are invalid or reference an unknown model.",
                nameof(settings));
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await AtomicFileWriter.WriteAsync(SettingsPath, json, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetDefaultSettingsPath()
    {
        var localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                "The local application-data directory is unavailable.");
        }

        return Path.Combine(
            localApplicationData,
            ApplicationDirectoryName,
            SettingsFileName);
    }

    private bool IsValid(ModelSelectionSettings? settings)
    {
        if (settings is null ||
            settings.SchemaVersion != ModelSelectionSettings.CurrentSchemaVersion ||
            !Enum.IsDefined(settings.Mode))
        {
            return false;
        }

        if (settings.ActiveModelId is not null &&
            !knownModelIds.Contains(settings.ActiveModelId))
        {
            return false;
        }

        return settings.Mode != ModelSelectionMode.Manual ||
            settings.ActiveModelId is not null;
    }
}
