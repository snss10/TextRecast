using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextRecast.Deployment.Storage;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Setup;

public sealed class ModelSetupStateStore
{
    private const string ApplicationDirectoryName = "TextRecast";
    private const string StateFileName = "setup-state.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private readonly Dictionary<string, SlmModelProfile> profiles;

    public ModelSetupStateStore(IEnumerable<SlmModelProfile> profiles)
        : this(GetDefaultStatePath(), profiles)
    {
    }

    internal ModelSetupStateStore(
        string statePath,
        IEnumerable<SlmModelProfile> profiles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        ArgumentNullException.ThrowIfNull(profiles);

        StatePath = Path.GetFullPath(statePath);
        this.profiles = profiles.ToDictionary(
            profile => profile.Id,
            StringComparer.Ordinal);
        if (this.profiles.Count == 0)
        {
            throw new ArgumentException(
                "At least one known model profile is required.",
                nameof(profiles));
        }
    }

    public string StatePath { get; }

    public async Task<ModelSetupState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(StatePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return ModelSetupState.Default;
        }
        catch (DirectoryNotFoundException)
        {
            return ModelSetupState.Default;
        }

        ModelSetupState? state;
        try
        {
            state = JsonSerializer.Deserialize<ModelSetupState>(
                json,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return ModelSetupState.Default;
        }
        catch (NotSupportedException)
        {
            return ModelSetupState.Default;
        }

        if (state is not null)
        {
            ThrowIfUnsupportedSchema(state);
        }

        return IsValid(state)
            ? state!
            : ModelSetupState.Default;
    }

    public async Task SaveAsync(
        ModelSetupState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!IsValid(state))
        {
            throw new ArgumentException(
                "The setup state is invalid or references an unknown model.",
                nameof(state));
        }

        var json = JsonSerializer.Serialize(state, SerializerOptions);
        await AtomicFileWriter.WriteAsync(StatePath, json, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string GetDefaultStatePath()
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
            StateFileName);
    }

    private bool IsValid(ModelSetupState? state)
    {
        return state is not null &&
            state.SchemaVersion == ModelSetupState.CurrentSchemaVersion &&
            IsValid(state.ActiveModel) &&
            IsValid(state.PendingSetup);
    }

    private bool IsValid(ActiveModelState? activeModel)
    {
        if (activeModel is null)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(activeModel.ModelId) &&
            !string.IsNullOrWhiteSpace(activeModel.ExpectedSha256) &&
            !string.IsNullOrWhiteSpace(activeModel.SourceRevision) &&
            profiles.TryGetValue(activeModel.ModelId, out var profile) &&
            activeModel.ExpectedSha256.Equals(
                profile.ExpectedSha256,
                StringComparison.OrdinalIgnoreCase) &&
            activeModel.ExpectedFileSize == profile.ExpectedFileSize &&
            activeModel.SourceRevision.Equals(
                profile.SourceRevision,
                StringComparison.Ordinal) &&
            IsUtc(activeModel.VerifiedAtUtc);
    }

    private bool IsValid(PendingModelSetupState? pendingSetup)
    {
        if (pendingSetup is null)
        {
            return true;
        }

        return pendingSetup.OperationId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(pendingSetup.ModelId) &&
            profiles.ContainsKey(pendingSetup.ModelId) &&
            !string.IsNullOrWhiteSpace(pendingSetup.ApplicationVersion) &&
            Enum.IsDefined(pendingSetup.Stage) &&
            IsUtc(pendingSetup.StartedAtUtc) &&
            IsUtc(pendingSetup.UpdatedAtUtc) &&
            pendingSetup.UpdatedAtUtc >= pendingSetup.StartedAtUtc;
    }

    private static bool IsUtc(DateTimeOffset value)
    {
        return value != default && value.Offset == TimeSpan.Zero;
    }

    private static void ThrowIfUnsupportedSchema(ModelSetupState state)
    {
        if (state.SchemaVersion != ModelSetupState.CurrentSchemaVersion ||
            state.ActiveModel is { SchemaVersion: not ActiveModelState.CurrentSchemaVersion } ||
            state.PendingSetup is { SchemaVersion: not PendingModelSetupState.CurrentSchemaVersion })
        {
            throw new InvalidDataException(
                "The setup state was created by an unsupported TextRecast version and was not changed.");
        }
    }
}
