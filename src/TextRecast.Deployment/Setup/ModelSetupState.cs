using System.Text.Json.Serialization;
using TextRecast.Infrastructure.SLM;

namespace TextRecast.Deployment.Setup;

public enum PendingModelSetupStage
{
    ApplicationInstalled,
    DownloadingModel,
    VerifyingModel
}

public sealed record ActiveModelState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required string ModelId { get; init; }
    public required string ExpectedSha256 { get; init; }
    public required long ExpectedFileSize { get; init; }
    public required string SourceRevision { get; init; }
    public required DateTimeOffset VerifiedAtUtc { get; init; }
}

public sealed record PendingModelSetupState
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public required Guid OperationId { get; init; }
    public required string ModelId { get; init; }
    public required string ApplicationVersion { get; init; }
    public required PendingModelSetupStage Stage { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public required DateTimeOffset UpdatedAtUtc { get; init; }
}

public sealed record ModelSetupState
{
    public const int CurrentSchemaVersion = 1;

    public static ModelSetupState Default { get; } = new();

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public ActiveModelState? ActiveModel { get; init; }
    public PendingModelSetupState? PendingSetup { get; init; }

    [JsonIgnore]
    public bool IsComplete => ActiveModel is not null && PendingSetup is null;

    public ModelSetupState BeginPendingSetup(
        string modelId,
        string applicationVersion,
        DateTimeOffset startedAtUtc,
        Guid? operationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);
        EnsureUtc(startedAtUtc, nameof(startedAtUtc));

        var id = operationId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "The setup operation identifier cannot be empty.",
                nameof(operationId));
        }

        return this with
        {
            PendingSetup = new PendingModelSetupState
            {
                OperationId = id,
                ModelId = modelId,
                ApplicationVersion = applicationVersion,
                Stage = PendingModelSetupStage.ApplicationInstalled,
                StartedAtUtc = startedAtUtc,
                UpdatedAtUtc = startedAtUtc
            }
        };
    }

    public ModelSetupState AdvancePendingSetup(
        PendingModelSetupStage stage,
        DateTimeOffset updatedAtUtc)
    {
        if (PendingSetup is null)
        {
            throw new InvalidOperationException("There is no pending model setup to update.");
        }

        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
        if (updatedAtUtc < PendingSetup.UpdatedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(updatedAtUtc),
                "The setup update time cannot move backwards.");
        }

        return this with
        {
            PendingSetup = PendingSetup with
            {
                Stage = stage,
                UpdatedAtUtc = updatedAtUtc
            }
        };
    }

    public ModelSetupState ActivateVerifiedModel(
        SlmModelProfile profile,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureUtc(verifiedAtUtc, nameof(verifiedAtUtc));
        if (PendingSetup is not null &&
            !PendingSetup.ModelId.Equals(profile.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The verified model does not match the pending model setup.");
        }

        return this with
        {
            ActiveModel = new ActiveModelState
            {
                ModelId = profile.Id,
                ExpectedSha256 = profile.ExpectedSha256,
                ExpectedFileSize = profile.ExpectedFileSize,
                SourceRevision = profile.SourceRevision,
                VerifiedAtUtc = verifiedAtUtc
            },
            PendingSetup = null
        };
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException(
                "Setup-state timestamps must be non-default UTC values.",
                parameterName);
        }
    }
}
