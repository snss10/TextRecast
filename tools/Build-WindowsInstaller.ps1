[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version,

    [string] $PublishDirectory = "artifacts\publish\TextRecast-win-x64",

    [string] $OutputDirectory = "artifacts\release",

    [switch] $NoRestore
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $PublishDirectory))
$outputPath = [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
$internalDirectory = Join-Path $repositoryRoot "artifacts\installer\internal"
$setupPublishDirectory = Join-Path $repositoryRoot "artifacts\installer\setup-host"
$internalPackagePath = Join-Path $internalDirectory "TextRecast.Package.exe"
$packageName = "TextRecast-v$Version-win-x64-setup"
$setupPath = Join-Path $outputPath "$packageName.exe"

if (-not (Test-Path -LiteralPath $publishPath -PathType Container)) {
    throw "The application publish directory does not exist: $publishPath"
}

$requiredFiles = @(
    "TextRecast.exe",
    "LICENSE",
    "NOTICE",
    "PRIVACY.md",
    "THIRD-PARTY-NOTICES.md",
    "DOTNET-LICENSE.txt",
    "DOTNET-THIRD-PARTY-NOTICES.txt",
    "WPF-LICENSE.txt"
)
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $file) -PathType Leaf)) {
        throw "The application publish is missing required file: $file"
    }
}

$excludedPayloads = @(
    Get-ChildItem -Path $publishPath -File -Recurse |
        Where-Object {
            $_.Name -like "*.gguf" -or
            $_.Name -like "*.partial" -or
            $_.Name -like "*.partial.metadata.json"
        }
)
if ($excludedPayloads.Count -ne 0) {
    throw "The installer payload contains a model or partial-download file."
}

New-Item -ItemType Directory -Path $internalDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $setupPublishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$nsisCompiler = & (Join-Path $PSScriptRoot "Get-Nsis.ps1")
$versionParts = $Version.Split('.')
$fileVersion = "$($versionParts[0]).$($versionParts[1]).$($versionParts[2]).0"
$packageScript = Join-Path $repositoryRoot "installer\TextRecast.Package\TextRecast.nsi"
$productId = "{FF637B7C-6CB2-470E-A9A6-7366AA51A3D6}"

& $nsisCompiler "/V3" "/DAPP_VERSION=$Version" "/DFILE_VERSION=$fileVersion" "/DPUBLISH_DIRECTORY=$publishPath" "/DOUTPUT_FILE=$internalPackagePath" "/DPRODUCT_ID=$productId" $packageScript
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $internalPackagePath -PathType Leaf)) {
    throw "NSIS failed to build the internal TextRecast package."
}

$publishArguments = @(
    "publish",
    "src\TextRecast.Setup\TextRecast.Setup.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:Version=$Version",
    "-p:EmbeddedPackagePath=$internalPackagePath",
    "-o", $setupPublishDirectory
)
if ($NoRestore) {
    $publishArguments += "--no-restore"
}

Push-Location $repositoryRoot
try {
    & dotnet $publishArguments
    if ($LASTEXITCODE -ne 0) {
        throw "The self-contained setup host publish failed."
    }
}
finally {
    Pop-Location
}

$publishedSetupPath = Join-Path $setupPublishDirectory "TextRecast.Setup.exe"
if (-not (Test-Path -LiteralPath $publishedSetupPath -PathType Leaf)) {
    throw "The setup host publish did not produce TextRecast.Setup.exe."
}

Copy-Item -LiteralPath $publishedSetupPath -Destination $setupPath -Force
$checksum = (Get-FileHash -LiteralPath $setupPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$setupPath.sha256"
Set-Content -LiteralPath $checksumPath -Value "$checksum  $packageName.exe" -NoNewline

Write-Output $setupPath
Write-Output $checksumPath
