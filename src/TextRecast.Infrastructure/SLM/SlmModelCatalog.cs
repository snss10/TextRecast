namespace TextRecast.Infrastructure.SLM;

public static class SlmModelCatalog
{
    private const string DefaultFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
    private const string Qwen35BalancedRevision = "f6d5376be1edb4d416d56da11e5397a961aca8ae";

    public static SlmModelProfile Default { get; } = new()
    {
        Id = "Qwen2.5-1.5B-Instruct-Q4_K_M",
        DisplayName = "Qwen 2.5 1.5B Instruct",
        Role = SlmModelRole.Fast,
        Description = "Smallest download and fastest established TextRecast option.",
        LanguageSupport = "English",
        LimitationNotice =
            "Fast local model. It can miss context or change details, so review every result before replacing text.",
        IsExperimental = false,
        AdapterId = Qwen25ModelAdapter.AdapterId,
        PromptProfileId = "qwen2.5-production-v1",
        SamplingProfileId = "greedy-v1",
        FileName = DefaultFileName,
        DownloadUri = new Uri(
            "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf?download=true"),
        ExpectedSha256 = "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
        ExpectedFileSize = 1117320736,
        SourceRepository = "Qwen/Qwen2.5-1.5B-Instruct-GGUF",
        SourceRevision = "main",
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
            "Experimental. Summaries may add structure or unsupported actions; review names, facts, and deadlines before replacement.",
        IsExperimental = true,
        AdapterId = Qwen35ModelAdapter.AdapterId,
        PromptProfileId = Qwen35ModelAdapter.BalancedPromptProfileId,
        SamplingProfileId = Qwen35ModelAdapter.DefaultSamplingProfileId,
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

    public static IReadOnlyList<SlmModelProfile> All { get; } =
        Array.AsReadOnly([Default, Qwen35Balanced]);

    public static SlmModelProfile GetById(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
        return All.SingleOrDefault(profile => profile.Id.Equals(modelId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown model identifier '{modelId}'.", nameof(modelId));
    }
}
