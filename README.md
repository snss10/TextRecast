<div align="center">
  <img src="src/TextRecast.App/Assets/Brand/textrecast-icon.png" alt="TextRecast icon" width="144" height="144">

  <h1>TextRecast</h1>

  <p><strong>Rewrite selected text anywhere on Windows, privately and locally.</strong></p>

  <p>
    Improve writing, adjust length, summarize, or change tone with a local SLM.<br>
    No account, API key, or cloud text processing required.
  </p>

  <p>
    <img alt="Platform: Windows" src="https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white">
    <img alt="Framework: .NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white">
    <img alt="Inference: Local" src="https://img.shields.io/badge/inference-local-1F883D">
    <img alt="Model: Local SLM" src="https://img.shields.io/badge/model-local%20SLM-6C5CE7">
  </p>
</div>

---

## What is TextRecast?

TextRecast is a small Windows desktop utility for rewriting text directly inside the applications you already use. Select editable text, click the floating TextRecast button, choose an operation, and apply the result back to the original selection.

All inference runs on your computer through LLamaSharp's CPU backend. The default SLM is Qwen2.5-1.5B-Instruct, while the model subsystem is isolated so additional local models can be introduced later. TextRecast does not send selected text to an external API or store a formatting history.

## Features

- **Improve** grammar, spelling, punctuation, wording, and clarity
- **Shorten** text while retaining essential details
- **Lengthen** compact text without inventing new facts
- **Summarize** selections into a concise sentence
- **Change tone** to Professional, Casual, Friendly, Formal, or Direct
- Work across browsers, editors, messaging apps, and other Windows applications
- Preserve clipboard contents whenever Windows allows them to be restored
- Cancel long operations and retry replacement without regenerating text
- Validate the source window and selection before changing anything

## Quick start

### 1. Requirements

- Windows 10 or Windows 11 on x64 hardware
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Approximately 1.5 GB of available disk space

### 2. Build and run

Open PowerShell in the repository root and run:

```powershell
dotnet restore TextRecast.slnx
dotnet run --project src/TextRecast.App/TextRecast.App.csproj
```

On first launch, TextRecast automatically downloads the 1.1 GB local model and displays progress. The download can be cancelled or retried. A completed model is stored at:

```text
%LOCALAPPDATA%\TextRecast\Models\qwen2.5-1.5b-instruct-q4_k_m.gguf
```

The download is written to a temporary partial file. TextRecast only installs it after verifying both values below, so an interrupted or corrupted download is never loaded:

| Property | Expected value |
| --- | --- |
| File size | `1,117,320,736` bytes |
| SHA-256 | `6a1a2eb6d15622bf3c96857206351ba97e1af16c30d7a74ee38970e434e9407e` |

For offline setup or portable distribution, you can instead download the model from the official [Qwen2.5-1.5B-Instruct-GGUF model page](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF) and place it in the repository's `Models/` directory before building. A packaged model is preferred when present. More information is available in [Models/README.md](Models/README.md).

The first formatting request loads the verified model into memory. Later requests reuse the loaded model.

## Using TextRecast

1. Select editable text in another Windows application.
2. Click the floating TextRecast icon.
3. Choose **Improve**, **Shorter**, **Longer**, **Summarize**, or **Tone**.
4. Choose a style if you selected **Tone**.
5. Click **Apply**.

TextRecast returns focus to the source application, confirms that the original selection is still present, and replaces it with the generated result.

You can drag the floating icon to reposition it. Right-click the icon and choose **Quit TextRecast** to close the application.

If replacement cannot be completed safely, the original text remains unchanged. The generated result stays visible so you can restore the selection and click **Retry replace**.

## How it works

```text
Select text in another application
             |
             v
Capture selection
(UI Automation or clipboard fallback)
             |
             v
Format with the local SLM
             |
             v
Revalidate window, process, age, and text
             |
             v
Paste the result and restore the clipboard
```

### Architecture

Dependencies point inward toward the platform-independent Core project:

```text
TextRecast.App
    |-- TextRecast.Core
    `-- TextRecast.Infrastructure
            `-- TextRecast.Core
```

- **Core** defines workflows, contracts, formatting requests, results, and shared models. It has no WPF, LLamaSharp, clipboard, or Win32 dependency.
- **Infrastructure** implements local SLM inference and Windows-specific capture and replacement services.
- **App** contains WPF presentation and wires Core abstractions to Infrastructure implementations.
- **Tests** exercise workflow safety, prompt escaping, and text chunking independently from the desktop UI.

### Project structure

| Path | Purpose |
| --- | --- |
| `src/TextRecast.App/` | WPF entry point, presentation windows, and brand assets |
| `src/TextRecast.Core/` | Platform-independent application workflow, contracts, results, and models |
| `src/TextRecast.Infrastructure/SLM/` | Model configuration, prompt building, chunking, installation, and local inference |
| `src/TextRecast.Infrastructure/Windows/` | Clipboard, UI Automation, replacement, and Win32 implementations |
| `tests/TextRecast.Core.Tests/` | Application workflow and safety tests |
| `tests/TextRecast.Infrastructure.Tests/` | SLM infrastructure behavior tests |
| `Models/` | Local GGUF model location and model documentation |
| `TextRecast.slnx` | Solution entry point for builds and tests |

### Adding another SLM

Model-independent behavior lives in `src/TextRecast.Infrastructure/SLM`. To add another model:

1. Add its filename, download URL, checksum, size, context, and output limits as a new profile in `SlmModelCatalog`.
2. Implement `ISlmPromptBuilder` when the model uses a prompt format other than ChatML.
3. Select the profile and prompt builder in the App composition root.

The installer, integrity verification, local storage, inference lifecycle, chunking, and application workflow can then be reused without model-specific renaming.

## Privacy and safety

TextRecast is designed to fail safely when it cannot prove that the original selection is still valid.

- Selected and generated text is not logged or saved.
- Formatting runs locally without an API key.
- Input selections are limited to 32,000 characters.
- Generated replacements are limited to 128,000 characters.
- Captured selections expire after 15 minutes.
- The source window handle and process identity must still match.
- The current selection must match the originally captured text.
- Empty output, NUL characters, and oversized output are rejected.
- ChatML control markers in source content are neutralized.
- Clipboard restoration is best-effort because other applications can temporarily lock it.

Long selections are split at sensible text boundaries. Improve, Shorter, Longer, and Tone use chunks above 450 characters. Summaries above 6,000 characters use hierarchical reduction. Prompt tokens are checked against the model's context capacity before inference.

## Build a release

Create a framework-dependent Windows x64 build with:

```powershell
dotnet publish src/TextRecast.App/TextRecast.App.csproj -c Release -r win-x64 --self-contained false
```

Output is written to:

```text
src/TextRecast.App/bin/Release/net10.0-windows/win-x64/publish/
```

The GGUF model is copied into the published `Models` directory when it exists in the source `Models` directory. Otherwise, the published application downloads it into the current user's local application-data directory on first launch.

## Troubleshooting

<details>
<summary><strong>The local model file is missing</strong></summary>

Restart TextRecast to reopen the first-run downloader. For an offline installation, place the GGUF file in the application's `Models/` directory using the exact filename shown above.
</details>

<details>
<summary><strong>The model integrity check failed</strong></summary>

The file is incomplete or is not the expected Q4_K_M model. Download it again and compare its file size and SHA-256 checksum with the expected values above.
</details>

<details>
<summary><strong>No selected text was captured</strong></summary>

Keep the source application open and the text selected before clicking TextRecast. The source application must support UI Automation text selection or copying with <kbd>Ctrl</kbd>+<kbd>C</kbd>.
</details>

<details>
<summary><strong>The selection could not be replaced</strong></summary>

The window, process, selection, or capture age may have changed. Select the original text again and repeat the operation. An elevated application may also reject simulated input from a non-elevated TextRecast process.
</details>

<details>
<summary><strong>Formatting is slow</strong></summary>

Inference is CPU-only. The initial request also verifies and loads the model, while large selections can require several inference passes. You can cancel an active operation from the result window.
</details>

## Technology

- WPF on .NET 10
- Local SLM inference through [LLamaSharp](https://github.com/SciSharp/LLamaSharp)
- Default model: [Qwen2.5-1.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF)
- Windows UI Automation
- Win32 clipboard and input APIs

## Development status

The project builds with nullable reference types enabled, recommended .NET analyzers, and warnings treated as errors. Run the automated suite with `dotnet test TextRecast.slnx`. Windows capture and replacement behavior should also be manually verified across supported applications before a public release.

See [SECURITY.md](SECURITY.md) for responsible vulnerability reporting.

## License

TextRecast source code and documentation are licensed under the [Apache License 2.0](LICENSE).

```text
Copyright 2026 snss10
```

The license permits use, modification, and distribution, including commercial use, subject to its conditions. It does not grant permission to use the TextRecast name or branding as trademarks. See [NOTICE](NOTICE) for project attribution and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for dependency and model licensing information.

### Model license

Qwen2.5-1.5B-Instruct is distributed under the Apache License 2.0. Review the upstream [model card and license](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF) before redistributing the model with TextRecast.
