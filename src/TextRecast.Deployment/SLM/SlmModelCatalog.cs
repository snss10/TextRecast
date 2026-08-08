namespace TextRecast.Infrastructure.SLM;

public static class SlmModelCatalog
{
    private const string DefaultFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
    private const string DefaultRevision = "dd26da440ef0330c47919d1ecae0966d24022222";
    private const string Qwen35BalancedRevision = "f6d5376be1edb4d416d56da11e5397a961aca8ae";
    private const string Qwen35QualityRevision = "e87f176479d0855a907a41277aca2f8ee7a09523";
    private const string Granite41AlternativeRevision = "ab4701481089b58a082ef63cc1cee738887293ff";

    public static SlmModelProfile Default { get; } = new()
    {
        Id = "Qwen2.5-1.5B-Instruct-Q4_K_M",
        DisplayName = "Qwen 2.5 1.5B Instruct",
        Role = SlmModelRole.Fast,
        Description = "Smallest download and fastest established TextRecast option.",
        LanguageSupport = "English",
        LimitationNotice =
            "Fast local model. It can miss context or change details, so review every replacement in the source application.",
        IsExperimental = false,
        AdapterId = SlmRuntimeProfileIds.Qwen25AdapterId,
        PromptProfileId = SlmRuntimeProfileIds.Qwen25PromptProfileId,
        SamplingProfileId = SlmRuntimeProfileIds.GreedySamplingProfileId,
        FileName = DefaultFileName,
        DownloadUri = new Uri(
            $"https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/{DefaultRevision}/{DefaultFileName}?download=true"),
        ExpectedSha256 = "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
        ExpectedFileSize = 1117320736,
        SourceRepository = "Qwen/Qwen2.5-1.5B-Instruct-GGUF",
        SourceRevision = DefaultRevision,
        LicenseExpression = "Apache-2.0",
        ContextSize = 4096,
        MaxOutputTokens = 768
    };

    public static SlmModelProfile Qwen35Balanced { get; } = new()
    {
        Id = "Qwen3.5-2B-Q5_K_M",
        DisplayName = "Qwen 3.5 2B",
        Role = SlmModelRole.Balanced,
        Description = "Faster experimental option with stronger general rewriting than the smallest model.",
        LanguageSupport = "English",
        LimitationNotice =
            "Experimental. Summaries may add structure or unsupported actions; review names, facts, and deadlines after every replacement.",
        IsExperimental = true,
        AdapterId = SlmRuntimeProfileIds.Qwen35AdapterId,
        PromptProfileId = SlmRuntimeProfileIds.Qwen35BalancedPromptProfileId,
        SamplingProfileId = SlmRuntimeProfileIds.Qwen35SamplingProfileId,
        FileName = "Qwen3.5-2B-Q5_K_M.gguf",
        DownloadUri = new Uri(
            $"https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/resolve/{Qwen35BalancedRevision}/Qwen3.5-2B-Q5_K_M.gguf?download=true"),
        ExpectedSha256 = "1885b3a9195f8cc09da9a7a7a75afdc1e8d5cbf9fc4a499c3961dddea37098ac",
        ExpectedFileSize = 1435238656,
        SourceRepository = "unsloth/Qwen3.5-2B-GGUF",
        SourceRevision = Qwen35BalancedRevision,
        LicenseExpression = "Apache-2.0",
        Requirements = new SlmModelRequirements
        {
            Tier = SlmModelTier.Balanced,
            QualityScore = 9.77,
            PeakWorkingSetBytes = 1708875776,
            MeasuredTokensPerSecond = 14.20
        },
        ContextSize = 4096,
        MaxOutputTokens = 768
    };

    public static SlmModelProfile Qwen35Quality { get; } = new()
    {
        Id = "Qwen3.5-4B-Q5_K_M",
        DisplayName = "Qwen 3.5 4B",
        Role = SlmModelRole.Quality,
        Description = "Largest TextRecast option for higher-capability computers.",
        LanguageSupport = "English",
        LimitationNotice =
            "Experimental. It may occasionally assign unsupported roles or titles or alter deadline wording; carefully review every result.",
        IsExperimental = true,
        AdapterId = SlmRuntimeProfileIds.Qwen35AdapterId,
        PromptProfileId = SlmRuntimeProfileIds.Qwen35QualityPromptProfileId,
        SamplingProfileId = SlmRuntimeProfileIds.Qwen35SamplingProfileId,
        FileName = "Qwen3.5-4B-Q5_K_M.gguf",
        DownloadUri = new Uri(
            $"https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/resolve/{Qwen35QualityRevision}/Qwen3.5-4B-Q5_K_M.gguf?download=true"),
        ExpectedSha256 = "8814232b85594dcd46c50e5b8b29324a7efe9e746edbe8a3d1df3d3fce7aad39",
        ExpectedFileSize = 3143656608,
        SourceRepository = "unsloth/Qwen3.5-4B-GGUF",
        SourceRevision = Qwen35QualityRevision,
        LicenseExpression = "Apache-2.0",
        Requirements = new SlmModelRequirements
        {
            Tier = SlmModelTier.Quality,
            QualityScore = 9.75,
            PeakWorkingSetBytes = 3515650048,
            MeasuredTokensPerSecond = 6.27
        },
        ContextSize = 4096,
        MaxOutputTokens = 768
    };

    public static SlmModelProfile Granite41Alternative { get; } = new()
    {
        Id = "Granite-4.1-3B-Q5_K_M",
        DisplayName = "Granite 4.1 3B",
        Role = SlmModelRole.Alternative,
        Description = "Experimental IBM model-family alternative for users who want another local option.",
        LanguageSupport = "English",
        LimitationNotice =
            "Experimental. This model showed more semantic drift, especially in summaries and conditions; verify meaning, status, actors, and deadlines carefully.",
        IsExperimental = true,
        AdapterId = SlmRuntimeProfileIds.Granite41AdapterId,
        PromptProfileId = SlmRuntimeProfileIds.Granite41BalancedPromptProfileId,
        SamplingProfileId = SlmRuntimeProfileIds.GreedySamplingProfileId,
        FileName = "granite-4.1-3b-Q5_K_M.gguf",
        DownloadUri = new Uri(
            $"https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF/resolve/{Granite41AlternativeRevision}/granite-4.1-3b-Q5_K_M.gguf?download=true"),
        ExpectedSha256 = "f7724d259f29b0edf147144ac530ca26f91c97af8274249f933073c461678a3c",
        ExpectedFileSize = 2437012064,
        SourceRepository = "ibm-granite/granite-4.1-3b-GGUF",
        SourceRevision = Granite41AlternativeRevision,
        LicenseExpression = "Apache-2.0",
        Requirements = new SlmModelRequirements
        {
            Tier = SlmModelTier.Balanced,
            QualityScore = 9.49,
            PeakWorkingSetBytes = 2896392192,
            MeasuredTokensPerSecond = 9.13
        },
        ContextSize = 4096,
        MaxOutputTokens = 768
    };

    public static IReadOnlyList<SlmModelProfile> All { get; } =
        Array.AsReadOnly([Default, Qwen35Balanced, Qwen35Quality, Granite41Alternative]);

    public static SlmModelProfile GetById(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return All.SingleOrDefault(profile => profile.Id.Equals(modelId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown model identifier '{modelId}'.", nameof(modelId));
    }
}
