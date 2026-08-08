# Windows installer architecture

TextRecast uses a two-layer per-user Windows setup:

1. `TextRecast.Setup` is a self-contained WPF host. It owns setup orchestration and has no dependency on LLamaSharp or the inference project.
2. An internal NSIS package owns application files, the Start Menu shortcut, and Windows uninstall registration. It is embedded in the setup host and is not published separately.

The application publish directory is the only package input. Normal publishes disable bundled models, and the build script rejects `*.gguf`, `*.partial`, and `*.partial.metadata.json` before invoking NSIS.

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

## Licensing

NSIS 3.12 and its zlib compression module are used under the zlib/libpng license. The applicable notice is kept in `NSIS-LICENSE.txt` and installed beside the application. The package also contains TextRecast's Apache license, privacy notice, project notice, third-party notices, and the exact legal files from the bundled .NET and WPF runtime packs.
