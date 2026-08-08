[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$version = "3.12"
$expectedSha256 = "56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F"
$expectedCompilerSha256 = "25D1AA7081DB1A9DE9690B59983F9652B7409A189C3444328CC1841EFF693E8D"
$downloadUri = "https://master.dl.sourceforge.net/project/nsis/NSIS%203/3.12/nsis-3.12.zip?viasf=1"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolRoot = Join-Path $repositoryRoot "artifacts\tools"
$archivePath = Join-Path $toolRoot "nsis-$version.zip"
$archiveIdentity = $expectedSha256.Substring(0, 12).ToLowerInvariant()
$extractionRoot = Join-Path $toolRoot "nsis-$version-$archiveIdentity"
$compilerPath = Join-Path $extractionRoot "nsis-$version\Bin\makensis.exe"

New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    Invoke-WebRequest -Uri $downloadUri -OutFile $archivePath
}

$actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if (-not $actualSha256.Equals($expectedSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw "NSIS archive integrity check failed. Expected $expectedSha256 but found $actualSha256. Delete '$archivePath' and retry."
}

$fullToolRoot = [IO.Path]::GetFullPath($toolRoot).TrimEnd('\')
$fullExtractionRoot = [IO.Path]::GetFullPath($extractionRoot)
$safeToolPrefix = "$fullToolRoot\"
if (-not $fullExtractionRoot.StartsWith(
    $safeToolPrefix,
    [StringComparison]::OrdinalIgnoreCase)) {
    throw "The NSIS extraction target is outside the repository tool directory."
}

$stagingRoot = "$fullExtractionRoot.pending-$([Guid]::NewGuid().ToString('N'))"
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $stagingRoot
    $stagedCompilerPath = Join-Path $stagingRoot "nsis-$version\Bin\makensis.exe"
    if (-not (Test-Path -LiteralPath $stagedCompilerPath -PathType Leaf)) {
        throw "The verified NSIS archive did not contain the expected compiler."
    }

    $actualCompilerSha256 = (Get-FileHash -LiteralPath $stagedCompilerPath -Algorithm SHA256).Hash
    if (-not $actualCompilerSha256.Equals(
        $expectedCompilerSha256,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "The extracted NSIS compiler failed its integrity check."
    }

    if (Test-Path -LiteralPath $fullExtractionRoot) {
        Remove-Item -LiteralPath $fullExtractionRoot -Recurse -Force
    }
    Move-Item -LiteralPath $stagingRoot -Destination $fullExtractionRoot
}
finally {
    if (Test-Path -LiteralPath $stagingRoot) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Write-Output $compilerPath
