[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SetupPath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $ExpectedVersion,

    [string] $PreviousSetupPath,

    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $PreviousVersion = "0.1.0",

    [string] $UpgradeFixtureFileName
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$resolvedSetupPath = (Resolve-Path $SetupPath).Path
$resolvedPreviousSetupPath = if ([string]::IsNullOrWhiteSpace($PreviousSetupPath)) {
    $null
}
else {
    (Resolve-Path $PreviousSetupPath).Path
}

$localApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::LocalApplicationData)
$roamingApplicationData = [Environment]::GetFolderPath(
    [Environment+SpecialFolder]::ApplicationData)
$smokeParent = [IO.Path]::GetFullPath(
    (Join-Path $localApplicationData "TextRecast.InstallerSmoke"))
$smokeRoot = Join-Path $smokeParent ([Guid]::NewGuid().ToString("N"))
$installDirectory = Join-Path $smokeRoot "TextRecast"
$applicationDirectory = Join-Path $installDirectory "Application"
$dataDirectory = Join-Path $localApplicationData "TextRecast"
$sentinelPath = Join-Path $dataDirectory ".installer-smoke-$([Guid]::NewGuid().ToString('N'))"
$applicationRegistryPath = "HKCU:\Software\TextRecast"
$uninstallRegistryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\TextRecast"
$shortcutDirectory = Join-Path $roamingApplicationData "Microsoft\Windows\Start Menu\Programs\TextRecast"
$shortcutPath = Join-Path $shortcutDirectory "TextRecast.lnk"
$dataDirectoryExisted = Test-Path -LiteralPath $dataDirectory -PathType Container

if ((Test-Path -LiteralPath $applicationRegistryPath) -or
    (Test-Path -LiteralPath $uninstallRegistryPath) -or
    (Test-Path -LiteralPath $shortcutDirectory)) {
    throw "A TextRecast installation already exists for this user. Installer smoke testing was not started."
}

function Invoke-Setup {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [ValidateSet("--install", "--uninstall")]
        [string] $Operation
    )

    $argumentList = @(
        $Operation,
        "--quiet",
        "--install-directory",
        "`"$installDirectory`""
    )
    $process = Start-Process -FilePath $Executable -ArgumentList $argumentList -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        $installedEntries = if (Test-Path -LiteralPath $installDirectory -PathType Container) {
            @(Get-ChildItem -LiteralPath $installDirectory -Force | Select-Object -ExpandProperty Name) -join ", "
        }
        else {
            "<installation directory absent>"
        }
        throw "TextRecast setup operation '$Operation' failed with exit code $($process.ExitCode). Install-root entries: $installedEntries"
    }
}

function Invoke-SetupExpectingFailure {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [ValidateSet("--install", "--uninstall")]
        [string] $Operation
    )

    $argumentList = @(
        $Operation,
        "--quiet",
        "--install-directory",
        "`"$installDirectory`""
    )
    $process = Start-Process -FilePath $Executable -ArgumentList $argumentList -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -eq 0) {
        throw "TextRecast setup operation '$Operation' unexpectedly succeeded during the rollback test."
    }
}

function Assert-InstalledVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Version
    )

    $requiredApplicationFiles = @(
        "TextRecast.exe",
        "LICENSE",
        "NOTICE",
        "PRIVACY.md",
        "THIRD-PARTY-NOTICES.md",
        "DOTNET-LICENSE.txt",
        "DOTNET-THIRD-PARTY-NOTICES.txt",
        "WPF-LICENSE.txt",
        ".textrecast-payload-complete"
    )
    foreach ($file in $requiredApplicationFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $applicationDirectory $file) -PathType Leaf)) {
            throw "The installed payload is missing required file: $file"
        }
    }

    if (Test-Path -LiteralPath (Join-Path $applicationDirectory "NSIS-LICENSE.txt") -PathType Leaf) {
        throw "The redundant standalone NSIS notice must not be installed; its terms belong in THIRD-PARTY-NOTICES.md."
    }

    $payloadVersion = Get-Content -LiteralPath (Join-Path $applicationDirectory ".textrecast-payload-complete") -Raw
    if ($payloadVersion -ne $Version) {
        throw "The active application payload marker does not match version $Version."
    }

    $ownershipMarker = Get-Content -LiteralPath (Join-Path $installDirectory ".textrecast-install") -Raw
    if ($ownershipMarker -ne "{FF637B7C-6CB2-470E-A9A6-7366AA51A3D6}") {
        throw "The installed application ownership marker is invalid."
    }

    foreach ($transientDirectoryName in @("Application.pending", "Application.previous")) {
        if (Test-Path -LiteralPath (Join-Path $installDirectory $transientDirectoryName)) {
            throw "The transient payload directory remains after installation: $transientDirectoryName"
        }
    }

    if (-not (Test-Path -LiteralPath (Join-Path $installDirectory "Uninstall.exe") -PathType Leaf)) {
        throw "The installed payload is missing required file: Uninstall.exe"
    }

    if (-not (Test-Path -LiteralPath $shortcutPath -PathType Leaf)) {
        throw "The per-user Start Menu shortcut was not created."
    }

    $applicationIdentity = Get-ItemProperty -LiteralPath $applicationRegistryPath
    if ($applicationIdentity.ProductId -ne "{FF637B7C-6CB2-470E-A9A6-7366AA51A3D6}" -or
        $applicationIdentity.InstallLocation -ne $installDirectory) {
        throw "The installed application identity is incorrect."
    }

    $uninstallIdentity = Get-ItemProperty -LiteralPath $uninstallRegistryPath
    if ($uninstallIdentity.DisplayVersion -ne $Version -or
        $uninstallIdentity.InstallLocation -ne $installDirectory) {
        throw "The Windows uninstall identity is incorrect for version $Version."
    }

    $excludedPayload = Get-ChildItem -Path $installDirectory -File -Recurse |
        Where-Object {
            $_.Name -like "*.gguf" -or
            $_.Name -like "*.partial" -or
            $_.Name -like "*.partial.metadata.json"
        } |
        Select-Object -First 1
    if ($null -ne $excludedPayload) {
        throw "The installed application unexpectedly contains model data."
    }

    $applicationPath = Join-Path $applicationDirectory "TextRecast.exe"
    $process = Start-Process -FilePath $applicationPath -ArgumentList "--verify-installation" -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "The installed TextRecast application failed its no-UI launch verification."
    }
}

function Assert-Uninstalled {
    if (Test-Path -LiteralPath $installDirectory) {
        throw "The owned TextRecast installation directory remains after uninstall."
    }

    if ((Test-Path -LiteralPath $applicationRegistryPath) -or
        (Test-Path -LiteralPath $uninstallRegistryPath) -or
        (Test-Path -LiteralPath $shortcutPath)) {
        throw "Per-user installer metadata remains after uninstall."
    }

    if (-not (Test-Path -LiteralPath $sentinelPath -PathType Leaf)) {
        throw "Uninstall removed TextRecast user data."
    }
}

try {
    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
    Set-Content -LiteralPath $sentinelPath -Value "preserve" -NoNewline

    if ($null -ne $resolvedPreviousSetupPath) {
        Invoke-Setup -Executable $resolvedPreviousSetupPath -Operation "--install"
        Assert-InstalledVersion -Version $PreviousVersion
        if (-not [string]::IsNullOrWhiteSpace($UpgradeFixtureFileName) -and
            -not (Test-Path -LiteralPath (Join-Path $applicationDirectory $UpgradeFixtureFileName) -PathType Leaf)) {
            throw "The controlled upgrade fixture is missing after installing the earlier package."
        }

        $lockedPayloadPath = Join-Path $applicationDirectory "TextRecast.exe"
        $lockedPayload = [IO.File]::Open(
            $lockedPayloadPath,
            [IO.FileMode]::Open,
            [IO.FileAccess]::Read,
            [IO.FileShare]::Read)
        try {
            Invoke-SetupExpectingFailure -Executable $resolvedSetupPath -Operation "--install"
        }
        finally {
            $lockedPayload.Dispose()
        }

        Assert-InstalledVersion -Version $PreviousVersion
        if (-not [string]::IsNullOrWhiteSpace($UpgradeFixtureFileName) -and
            -not (Test-Path -LiteralPath (Join-Path $applicationDirectory $UpgradeFixtureFileName) -PathType Leaf)) {
            throw "The failed upgrade did not preserve the previous application payload."
        }
    }

    Invoke-Setup -Executable $resolvedSetupPath -Operation "--install"
    Assert-InstalledVersion -Version $ExpectedVersion
    if (-not [string]::IsNullOrWhiteSpace($UpgradeFixtureFileName) -and
        (Test-Path -LiteralPath (Join-Path $applicationDirectory $UpgradeFixtureFileName))) {
        throw "The upgrade left an obsolete file from the earlier application payload."
    }

    Invoke-Setup -Executable $resolvedSetupPath -Operation "--uninstall"
    Assert-Uninstalled

    Invoke-Setup -Executable $resolvedSetupPath -Operation "--install"
    Assert-InstalledVersion -Version $ExpectedVersion
    Invoke-Setup -Executable $resolvedSetupPath -Operation "--uninstall"
    Assert-Uninstalled
}
finally {
    if (Test-Path -LiteralPath (Join-Path $installDirectory "Uninstall.exe") -PathType Leaf) {
        try {
            Invoke-Setup -Executable $resolvedSetupPath -Operation "--uninstall"
        }
        catch {
            Write-Warning "Automatic smoke-test uninstall failed: $($_.Exception.Message)"
        }
    }

    if (Test-Path -LiteralPath $sentinelPath -PathType Leaf) {
        Remove-Item -LiteralPath $sentinelPath -Force
    }

    if (Test-Path -LiteralPath $applicationRegistryPath) {
        Remove-Item -LiteralPath $applicationRegistryPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $uninstallRegistryPath) {
        Remove-Item -LiteralPath $uninstallRegistryPath -Recurse -Force
    }
    if (Test-Path -LiteralPath $shortcutDirectory -PathType Container) {
        Remove-Item -LiteralPath $shortcutDirectory -Recurse -Force
    }

    $resolvedSmokeRoot = [IO.Path]::GetFullPath($smokeRoot)
    $safeSmokePrefix = "$($smokeParent.TrimEnd('\'))\"
    if ($resolvedSmokeRoot.StartsWith($safeSmokePrefix, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedSmokeRoot)) {
        Remove-Item -LiteralPath $resolvedSmokeRoot -Recurse -Force
    }

    if (-not $dataDirectoryExisted -and
        (Test-Path -LiteralPath $dataDirectory -PathType Container) -and
        @(Get-ChildItem -LiteralPath $dataDirectory -Force).Count -eq 0) {
        Remove-Item -LiteralPath $dataDirectory -Force
    }
}
