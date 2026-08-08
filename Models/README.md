# Supported local models

TextRecast runs one cataloged GGUF model at a time. During setup, the user reviews the available choices and confirms the exact model before any download starts. Release archives do not contain model binaries.

All current profiles are for English rewriting. The three newer choices are experimental, and every replacement should be reviewed in the source application.

| Role | Model file | Size | SHA-256 |
| --- | --- | ---: | --- |
| Fast/default | `qwen2.5-1.5b-instruct-q4_k_m.gguf` | 1,117,320,736 bytes | `6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e` |
| Balanced | `Qwen3.5-2B-Q5_K_M.gguf` | 1,435,238,656 bytes | `1885b3a9195f8cc09da9a7a7a75afdc1e8d5cbf9fc4a499c3961dddea37098ac` |
| Best quality | `Qwen3.5-4B-Q5_K_M.gguf` | 3,143,656,608 bytes | `8814232b85594dcd46c50e5b8b29324a7efe9e746edbe8a3d1df3d3fce7aad39` |
| Alternative | `granite-4.1-3b-Q5_K_M.gguf` | 2,437,012,064 bytes | `f7724d259f29b0edf147144ac530ca26f91c97af8274249f933073c461678a3c` |

## Pinned sources

| Model | Repository and revision | License |
| --- | --- | --- |
| Qwen 2.5 1.5B Q4_K_M | [`Qwen/Qwen2.5-1.5B-Instruct-GGUF@dd26da4`](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/tree/dd26da440ef0330c47919d1ecae0966d24022222) | Apache-2.0 |
| Qwen 3.5 2B Q5_K_M | [`unsloth/Qwen3.5-2B-GGUF@f6d5376`](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF/tree/f6d5376be1edb4d416d56da11e5397a961aca8ae) | Apache-2.0 |
| Qwen 3.5 4B Q5_K_M | [`unsloth/Qwen3.5-4B-GGUF@e87f176`](https://huggingface.co/unsloth/Qwen3.5-4B-GGUF/tree/e87f176479d0855a907a41277aca2f8ee7a09523) | Apache-2.0 |
| Granite 4.1 3B Q5_K_M | [`ibm-granite/granite-4.1-3b-GGUF@ab47014`](https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF/tree/ab4701481089b58a082ef63cc1cee738887293ff) | Apache-2.0 |

The model binaries remain subject to their upstream terms and are not relicensed as part of TextRecast. Review the linked model cards and licenses before redistributing any binary.

## Storage and verified downloads

Downloaded models are stored for the current Windows user under:

```text
%LOCALAPPDATA%\TextRecast\Models
```

An interrupted download uses two temporary files beside its final destination:

```text
<model-file>.partial
<model-file>.partial.metadata.json
```

TextRecast uses HTTP Range and If-Range to resume compatible partial downloads. It restarts when the server ignores the range, returns inconsistent metadata, or exposes a changed validator. The final file is installed only after its exact size and SHA-256 pass verification.

## Offline or portable builds

Place only exact cataloged GGUF files in this directory before an intentional offline publish. Model inclusion is disabled by default so normal builds and installers cannot accidentally carry a local model. Pass `-p:IncludeBundledModels=true` when publishing an offline build; setup then recognizes an exact catalog match as installed. Do not rename a model file or substitute another quantization without adding and testing a separate catalog profile.

The normal GitHub Actions release intentionally fails if a GGUF file enters the self-contained ZIP. This keeps the public application download small and leaves the model choice with the user.

Catalog metadata is defined in [`SlmModelCatalog.cs`](../src/TextRecast.Deployment/SLM/SlmModelCatalog.cs).
