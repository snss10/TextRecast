namespace TextRecast.Infrastructure.SLM;

public static class SlmModelCatalog
{
    private const string DefaultFileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";

    public static SlmModelProfile Default { get; } = new()
    {
        Id = "Qwen2.5-1.5B-Instruct-Q4_K_M",
        FileName = DefaultFileName,
        DownloadUri = new Uri(
            "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf?download=true"),
        ExpectedSha256 = "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
        ExpectedFileSize = 1117320736,
        ContextSize = 4096,
        MaxOutputTokens = 768
    };
}
