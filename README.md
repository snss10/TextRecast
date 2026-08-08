<div align="center">
  <img src="src/TextRecast.App/Assets/Brand/textrecast-icon.png" alt="TextRecast icon" width="144" height="144">

  <h1>TextRecast</h1>

  <p><strong>Rewrite selected text anywhere on Windows, privately and locally.</strong></p>

  <p>
    Improve writing, adjust its length, summarize it, or change its tone with a local small language model.<br>
    No account, API key, or cloud text processing is required.
  </p>

  <p>
    <img alt="Platform: Windows" src="https://img.shields.io/badge/platform-Windows-0078D4?logo=windows&logoColor=white">
    <img alt="Framework: .NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white">
    <img alt="Inference: Local" src="https://img.shields.io/badge/inference-local-1F883D">
    <img alt="License: Apache 2.0" src="https://img.shields.io/badge/license-Apache--2.0-D22128">
  </p>
</div>

---

## About

TextRecast is a lightweight Windows desktop utility that rewrites text inside the applications you already use. Select editable text, click the floating TextRecast button, choose an operation, and apply the result back to the original selection.

Inference runs locally through LLamaSharp's CPU backend. On first setup, TextRecast presents four cataloged models and downloads only the option the user explicitly confirms. Qwen 2.5 1.5B remains the fast default; the other choices are clearly labeled experimental.

## Features

- Improve grammar, spelling, punctuation, wording, and clarity
- Make text shorter or longer while preserving its meaning
- Summarize long selections
- Change tone to Professional, Casual, Friendly, Formal, or Direct
- Work in browsers, editors, messaging apps, and other Windows applications
- Run formatting locally without an account or API key
- Choose a compatible local model before any download begins
- Resume an interrupted model download instead of starting over
- Cancel an active generation and retry a failed replacement
- Revalidate the source window and selected text before replacing anything

## Install and run

### Requirements

- Windows 11 on x64 hardware, or a [Windows 10 edition still supported by .NET 10](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)
- The current [Microsoft Visual C++ v14 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170) for x64
- Approximately 2-4 GB of available disk space, depending on the selected model
- An internet connection when no valid packaged or previously installed model is available

The published Windows x64 application is self-contained. Users running that build do **not** need to install .NET 10.

### Run a published build

1. Download the latest `TextRecast-vX.Y.Z-win-x64.zip` from the [Releases page](https://github.com/snss10/TextRecast/releases).
2. Optionally verify it with the accompanying `.zip.sha256` file.
3. Extract the entire archive to a folder.
4. Run `TextRecast.exe`.

Keep every file from the archive together; the application depends on the included .NET runtime and native inference libraries. If a packaged release is not available yet, follow [Build from source](#build-from-source).

## First launch and model download

TextRecast shows the available models before making a network request. Each choice includes its role, download size, hardware compatibility, language support, and known limitations. The exact model and size are shown again for confirmation; only that model is downloaded.

| Role | Model | Download | Status |
| --- | --- | ---: | --- |
| Fast/default | Qwen 2.5 1.5B Q4_K_M | 1.04 GiB | Established default; review all output |
| Balanced | Qwen 3.5 2B Q5_K_M | 1.34 GiB | Experimental; summaries need extra review |
| Best quality | Qwen 3.5 4B Q5_K_M | 2.93 GiB | Experimental; verify roles, titles, and deadlines |
| Alternative | Granite 4.1 3B Q5_K_M | 2.27 GiB | Experimental; higher semantic-drift risk |

These profiles are currently supported for English rewriting only. None is perfect, so review the replaced text in the source application after every operation. Hardware recommendations are guidance; Qwen 2.5 remains selectable when hardware inspection is unavailable.

Cancelling or losing the connection keeps a validated partial download. Select **Retry** on the same computer to continue from the saved point. TextRecast restarts safely if the server no longer accepts the saved range or the remote file has changed. A model becomes active only after its exact size and SHA-256 checksum pass verification. A valid installed selection is reused on later launches.

The first formatting request loads the model into memory. Later requests reuse the loaded model and usually start faster.

<details>
<summary><strong>Where is the downloaded model stored?</strong></summary>

TextRecast stores the model for the current Windows user under:

```text
%LOCALAPPDATA%\TextRecast\Models
```

`%LOCALAPPDATA%` is a standard Windows environment variable. It expands automatically to the signed-in user's local application-data folder; it is not a path tied to the developer's computer.

For exact filenames, checksums, pinned upstream revisions, and offline setup instructions, see [Models/README.md](Models/README.md).
</details>

<details>
<summary><strong>Why does this README contain file paths?</strong></summary>

Only portable paths are used:

- Paths such as `src/TextRecast.App/...` are relative to the repository and work wherever the project is cloned. The relative icon path at the top lets GitHub display the TextRecast icon.
- `%LOCALAPPDATA%\TextRecast\Models` is the Windows per-user storage location for the downloaded model.
- Project paths in developer commands identify which project .NET should build or run.

There are no author-specific absolute paths in this README.
</details>

## Using TextRecast

1. Select editable text in another Windows application.
2. Click the floating TextRecast icon.
3. Choose **Improve**, **Shorter**, **Longer**, **Summarize**, or **Tone**.
4. Choose a style when using **Tone**.
5. Click **Apply** to generate the formatted text and replace the selection.

TextRecast returns focus to the source application, confirms that the original selection is still valid, and then replaces it. If replacement cannot be completed safely, the original text remains unchanged and the generated result remains available for **Retry replace**.

Drag the floating icon to reposition it. Right-click it to view the active model and its limitations, or choose **Quit TextRecast** to close the application.

## Privacy and safeguards

The complete data-handling statement is available in the [TextRecast Privacy Notice](PRIVACY.md).

- TextRecast itself does not write selected or generated text to logs or files.
- Formatting runs on the local CPU and does not use a text-processing API.
- TextRecast does not maintain a formatting history.
- Selections are limited to 32,000 characters and expire after 15 minutes.
- The source window, process, capture age, and selected text are checked before replacement.
- Empty, oversized, or unsafe generated output is rejected.
- Clipboard restoration is best-effort because other applications can temporarily lock the Windows clipboard.

Capture fallback and replacement temporarily use the Windows clipboard. Windows clipboard history, clipboard sync, or third-party clipboard managers may retain that content, so disable those features when working with sensitive text.

Network access begins only after the user confirms a model that is not already packaged or installed. After that download, normal formatting does not require a cloud service.

## How it works

```text
Select text in another application
              |
              v
Capture the selection
(UI Automation or clipboard fallback)
              |
              v
Format with the local SLM
              |
              v
Revalidate the original selection
              |
              v
Replace the text and restore the clipboard when possible
```

## Troubleshooting

<details>
<summary><strong>The cloned project says that .NET 10 is missing</strong></summary>

A source checkout requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) for development. A published self-contained Windows x64 build does not require a separate .NET installation.
</details>

<details>
<summary><strong>The application reports a missing Visual C++ DLL</strong></summary>

Install the latest x64 package from Microsoft's [supported Visual C++ Redistributable downloads](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170), then restart TextRecast.
</details>

<details>
<summary><strong>The model download or integrity check failed</strong></summary>

Check the internet connection and available disk space, then retry. TextRecast keeps a safe partial download after cancellation or a temporary network failure and resumes it automatically. Invalid partial data is discarded instead of being installed. Offline setup details and expected checksums are in [Models/README.md](Models/README.md).
</details>

<details>
<summary><strong>No selected text was captured</strong></summary>

Keep the source application open and the text selected before clicking TextRecast. The application must support UI Automation text selection or copying with <kbd>Ctrl</kbd>+<kbd>C</kbd>.
</details>

<details>
<summary><strong>The selection could not be replaced</strong></summary>

The window, process, selection, or capture age may have changed. Select the original text again and retry. Windows can also block simulated input when the source application is running as administrator and TextRecast is not.
</details>

<details>
<summary><strong>Formatting is slow</strong></summary>

Inference is CPU-only. The first request also verifies and loads the model, and large selections can require multiple inference passes. An active operation can be cancelled from the result window.
</details>

## Build from source

Building requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). From the repository root:

```powershell
dotnet restore TextRecast.slnx
dotnet build TextRecast.slnx -c Release --no-restore
dotnet test TextRecast.slnx -c Release --no-build
dotnet run --project src/TextRecast.App/TextRecast.App.csproj
```

### Publish a self-contained Windows build

```powershell
dotnet publish src/TextRecast.App/TextRecast.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/TextRecast-win-x64
```

Distribute the complete output folder, not only the executable. Normal publishes intentionally contain no model; the user chooses and confirms one during setup. An offline build can explicitly include an exact cataloged GGUF file with `-p:IncludeBundledModels=true`; see [Models/README.md](Models/README.md).

For tagged releases, GitHub Actions creates a model-free `TextRecast-vX.Y.Z-win-x64.zip` containing the complete runtime, TextRecast legal files, and the legal files supplied with the bundled .NET runtime packs, plus a matching SHA-256 file. A release tag must use `vMAJOR.MINOR.PATCH` and match the version in `Directory.Build.props`.

## Project layout

- [TextRecast.App](src/TextRecast.App/) contains the WPF presentation layer and application composition.
- [TextRecast.Core](src/TextRecast.Core/) contains platform-independent workflows, contracts, results, and models.
- [TextRecast.Deployment](src/TextRecast.Deployment/) contains the model catalog, verified downloads, hardware recommendations, setup state, and installer identity without inference dependencies.
- [TextRecast.Infrastructure](src/TextRecast.Infrastructure/) contains local SLM inference and Windows capture/replacement integrations.
- [TextRecast.Setup](src/TextRecast.Setup/) contains the self-contained managed Windows setup host.
- [installer](installer/) contains the internal per-user application package definition.
- [tests](tests/) contains Core, Deployment, Infrastructure, and Setup automated tests.
- [Models](Models/) documents supported models, integrity metadata, and offline packaging.

```text
TextRecast.Setup -------> TextRecast.Deployment
TextRecast.App ---------> TextRecast.Core + TextRecast.Deployment + TextRecast.Infrastructure
TextRecast.Infrastructure -> TextRecast.Core + TextRecast.Deployment
```

Model profiles are defined in [SlmModelCatalog.cs](src/TextRecast.Deployment/SLM/SlmModelCatalog.cs). A new model can reuse installation, verification, storage, inference lifecycle, chunking, and workflow components; models with a different chat format should provide a matching prompt builder. The model-free setup build and its transaction boundaries are documented in [installer/README.md](installer/README.md).

## Technology

- WPF on .NET 10
- NSIS 3.12 for the internal per-user application package
- [LLamaSharp](https://github.com/SciSharp/LLamaSharp) with CPU inference
- [Qwen 2.5 GGUF](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF), [Qwen 3.5 GGUF](https://huggingface.co/unsloth/Qwen3.5-2B-GGUF), and [Granite 4.1 GGUF](https://huggingface.co/ibm-granite/granite-4.1-3b-GGUF)
- Windows UI Automation, clipboard, and input APIs

## Security and license

Report security issues using the process in [SECURITY.md](SECURITY.md). Data handling is described in [PRIVACY.md](PRIVACY.md).

TextRecast source code and documentation are licensed under the [Apache License 2.0](LICENSE). Copyright 2026 snss10.

The TextRecast name, icon, and other brand assets identify this project. The Apache License 2.0 does not grant trademark rights beyond the uses permitted by Section 6 of the license.

See [NOTICE](NOTICE) for project attribution and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the separate licenses and notices that apply to dependencies, icon artwork, and the downloaded model.
