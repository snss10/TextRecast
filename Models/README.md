# Default local SLM

TextRecast currently uses the following default model profile:

| Property | Value |
| --- | --- |
| Model | `Qwen2.5-1.5B-Instruct-GGUF` |
| File | `qwen2.5-1.5b-instruct-q4_k_m.gguf` |
| Quantization | `Q4_K_M` |
| Size | `1,117,320,736` bytes |
| SHA-256 | `6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e` |
| Source | [Qwen/Qwen2.5-1.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF) |
| License | Apache License 2.0 |

The model binary is intentionally ignored by Git. When no packaged model is present, TextRecast downloads and verifies it automatically, then stores it at:

```text
%LOCALAPPDATA%\TextRecast\Models\qwen2.5-1.5b-instruct-q4_k_m.gguf
```

Interrupted downloads use stable files beside the final model:

```text
qwen2.5-1.5b-instruct-q4_k_m.gguf.partial
qwen2.5-1.5b-instruct-q4_k_m.gguf.partial.metadata.json
```

TextRecast sends HTTP Range and If-Range requests to continue a valid partial download. It restarts when the server ignores the range, reports a mismatched range, or exposes a changed validator. Temporary HTTP failures are retried with bounded backoff. The final file is moved into place only after its exact size and SHA-256 checksum are verified; untrusted partial data is removed.

For an offline or portable build, place the GGUF file in this repository directory before publishing. The App project links matching `Models/*.gguf` files into its output.

The default metadata is defined in [`SlmModelCatalog.cs`](../src/TextRecast.Infrastructure/SLM/SlmModelCatalog.cs). Add future model profiles there and review each upstream model card and license before distribution.
