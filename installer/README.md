# Windows installer architecture

TextRecast uses a two-layer per-user Windows setup:

1. `TextRecast.Setup` is a self-contained WPF host. It owns setup orchestration and has no dependency on LLamaSharp or the inference project.
2. An internal NSIS package owns application files, the Start Menu shortcut, and Windows uninstall registration. It is embedded in the setup host and is not published separately.

The application publish directory is the only package input. Normal publishes disable bundled models, and the build script rejects `*.gguf`, `*.partial`, and `*.partial.metadata.json` before invoking NSIS.

## Guided setup contract

The managed setup host owns this seven-step journey:

1. Welcome
2. License and Privacy
3. Destination
4. Model Choice
5. Ready to Install
6. Installing, downloading, and verifying
7. Complete

Legal and privacy content is packaged locally. Setup requires acceptance and one explicit compatible model choice before Install is enabled. No model request begins before Install, and retries or resumes remain bound to that selected catalog entry. Application installation completes before model setup begins; a model becomes active only after its exact size and SHA-256 pass verification.

The Install command changes the coordinator to the Installing page and yields one WPF render before starting the embedded package process. This keeps the UI responsive and makes the transition visible immediately. During installation, the shared compact footer shows only Cancel. Closing the window is guarded while package work is active.

The visual baseline and screen-specific decisions are maintained in the ignored local planning file `docs/design/v0.3.0/README.md`; executable behavior is enforced by setup and deployment tests in the repository.

## Build

First create the self-contained application publish, then build the setup executable:

```powershell
dotnet publish src/TextRecast.App/TextRecast.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/TextRecast-win-x64
./tools/Build-WindowsInstaller.ps1 -Version 0.2.0 -NoRestore
```

The result is written to `artifacts/release/TextRecast-v0.2.0-win-x64-setup.exe` with a matching SHA-256 file.

The tool bootstrap script downloads the official NSIS 3.12 archive and accepts it only when its SHA-256 is `56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F`. It expands that verified archive into a fresh hash-keyed directory and also checks the compiler SHA-256 (`25D1AA7081DB1A9DE9690B59983F9652B7409A189C3444328CC1841EFF693E8D`) before every package build. The archive and extracted compiler remain under ignored `artifacts/tools` paths.

## Ownership and recovery

- Installation is per-user and defaults to `%LOCALAPPDATA%\Programs\TextRecast`.
- The stable product identity is declared in `WindowsInstallerIdentity` and mirrored into the package build definition.
- Application files are staged under `Application.pending`, checked for required files, and then activated as one directory. The previous complete payload is retained during activation, restored if activation fails, and recovered on the next setup run after an interrupted swap.
- The shortcut and uninstall metadata are written only after payload activation. Uninstall removes the product-owned installation tree, shortcut, and registry entries.
- Models, partial downloads, settings, and setup state live under `%LOCALAPPDATA%\TextRecast` and survive application uninstall.
- The setup host extracts its internal package to a unique temporary directory and removes that directory after the package process exits.

The model-free setup can be exercised without UI by passing `--install --quiet` or `--uninstall --quiet`. `--install-directory <path>` is available for isolated smoke tests and is restricted to the current user's local application-data tree.

Source verification uses:

```powershell
dotnet format TextRecast.slnx --verify-no-changes --no-restore
dotnet build TextRecast.slnx -c Release --no-restore
dotnet test TextRecast.slnx -c Release --no-build
```

The packaging smoke test installs into an isolated per-user location, verifies payload replacement and uninstall ownership, and preserves model/settings data outside the package-owned application directory.

## Licensing

NSIS 3.12 and its zlib compression module are used under the zlib/libpng license. Their attribution and complete license terms are consolidated in `THIRD-PARTY-NOTICES.md`; a duplicate standalone NSIS notice is not installed. The package also contains TextRecast's Apache license, privacy notice, project notice, and the exact legal files from the bundled .NET and WPF runtime packs.
