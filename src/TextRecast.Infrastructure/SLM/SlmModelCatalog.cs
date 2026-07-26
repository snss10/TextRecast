using System.IO;

namespace TextRecast.Infrastructure.SLM;

public static class SlmModelCatalog
{
    public static SlmModelOptions CreateDefault()
    {
        const string fileName = "qwen2.5-1.5b-instruct-q4_k_m.gguf";
        return new SlmModelOptions
        {
            ModelId = "Qwen2.5-1.5B-Instruct-Q4_K_M",
            ModelFileName = fileName,
            DownloadUri = new Uri(
                "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf?download=true"),
            ModelPath = Path.Combine(AppContext.BaseDirectory, "Models", fileName),
            ExpectedModelSha256 = "6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e",
            ExpectedModelFileSize = 1117320736,
            ContextSize = 4096,
            MaxOutputTokens = 768
        };
    }
}
