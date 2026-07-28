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

Inference runs locally through LLamaSharp's CPU backend. The default model is Qwen2.5-1.5B-Instruct, and the SLM layer is designed so additional local models can be added later.

## Features

- Improve grammar, spelling, punctuation, wording, and clarity
- Make text shorter or longer while preserving its meaning
- Summarize long selections
- Change tone to Professional, Casual, Friendly, Formal, or Direct
- Work in browsers, editors, messaging apps, and other Windows applications
- Run formatting locally without an account or API key
- Cancel an active generation and retry a failed replacement
- Revalidate the source window and selected text before replacing anything

## Install and run

### Requirements

- Windows 10 or Windows 11 on x64 hardware
- The current [Microsoft Visual C++ v14 Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170) for x64
- Approximately 1.5 GB of available disk space
- An internet connection when no valid packaged or previously installed model is available

The published Windows x64 application is self-contained. Users running that build do **not** need to install .NET 10.

### Run a published build

1. Download the complete Windows x64 archive from the [Releases page](https://github.com/snss10/TextRecast/releases).
2. Extract the entire archive to a folder.
3. Run `TextRecast.exe`.

Keep every file from the archive together; the application depends on the included .NET runtime and native inference libraries. If a packaged release is not available yet, follow [Build from source](#build-from-source).

## First launch and model download

When no valid packaged or previously installed model is available, TextRecast downloads the 1.1 GB Qwen model on first launch. It shows download and verification progress and lets you cancel or retry. The model is written to a temporary file and is installed only after its file size and SHA-256 checksum are verified.

The first formatting request loads the model into memory. Later requests reuse the loaded model and usually start faster.

<details>
<summary><strong>Where is the downloaded model stored?</strong></summary>

TextRecast stores the model for the current Windows user under:

```text
%LOCALAPPDATA%\TextRecast\Models
```

`%LOCALAPPDATA%` is a standard Windows environment variable. It expands automatically to the signed-in user's local application-data folder; it is not a path tied to the developer's computer.

For the exact filename, checksum, upstream source, and offline setup instructions, see [Models/README.md](Models/README.md).
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

Drag the floating icon to reposition it. Right-click the icon and choose **Quit TextRecast** to close the application.

## Privacy and safeguards

- TextRecast itself does not write selected or generated text to logs or files.
- Formatting runs on the local CPU and does not use a text-processing API.
- TextRecast does not maintain a formatting history.
- Selections are limited to 32,000 characters and expire after 15 minutes.
- The source window, process, capture age, and selected text are checked before replacement.
- Empty, oversized, or unsafe generated output is rejected.
- Clipboard restoration is best-effort because other applications can temporarily lock the Windows clipboard.

Capture fallback and replacement temporarily use the Windows clipboard. Windows clipboard history, clipboard sync, or third-party clipboard managers may retain that content, so disable those features when working with sensitive text.

Network access is used to obtain the model only when a valid model is not already packaged or installed. After that download, normal formatting does not require a cloud service.

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

Check the internet connection and available disk space, then restart TextRecast and retry. A failed or cancelled partial download is not installed. Offline setup details and the expected checksum are in [Models/README.md](Models/README.md).
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

Distribute the complete output folder, not only the executable. The exact cataloged GGUF file documented in [Models/README.md](Models/README.md) is packaged when it is placed in the repository's `Models` directory before publishing; otherwise TextRecast downloads the model for the current user on first launch.

## Project layout

- [TextRecast.App](src/TextRecast.App/) contains the WPF presentation layer and application composition.
- [TextRecast.Core](src/TextRecast.Core/) contains platform-independent workflows, contracts, results, and models.
- [TextRecast.Infrastructure](src/TextRecast.Infrastructure/) contains local SLM inference and Windows integrations.
- [tests](tests/) contains Core and Infrastructure automated tests.
- [Models](Models/) documents the default model and offline packaging.

```text
TextRecast.App ---------> TextRecast.Core
        |
        +---------------> TextRecast.Infrastructure
                                  |
                                  +---------------> TextRecast.Core
```

Model profiles are defined in [SlmModelCatalog.cs](src/TextRecast.Infrastructure/SLM/SlmModelCatalog.cs). A new model can reuse installation, verification, storage, inference lifecycle, chunking, and workflow components; models with a different chat format should provide a matching prompt builder.

## Technology

- WPF on .NET 10
- [LLamaSharp](https://github.com/SciSharp/LLamaSharp) with CPU inference
- [Qwen2.5-1.5B-Instruct-GGUF](https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF)
- Windows UI Automation, clipboard, and input APIs

## Security and license

Report security issues using the process in [SECURITY.md](SECURITY.md).

TextRecast source code and documentation are licensed under the [Apache License 2.0](LICENSE). Copyright 2026 snss10.

The TextRecast name, icon, and other brand assets identify this project. The Apache License 2.0 does not grant trademark rights beyond the uses permitted by Section 6 of the license.

See [NOTICE](NOTICE) for project attribution and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) for the separate licenses and notices that apply to dependencies, icon artwork, and the downloaded model.
