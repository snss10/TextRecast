[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$version = "3.12"
$expectedSha256 = "56581F90DB321581C5381193D796FFFCF2D24B2F8FED2160A6C6A3BAA67F2C4F"
$expectedCompilerSha256 = "25D1AA7081DB1A9DE9690B59983F9652B7409A189C3444328CC1841EFF693E8D"
$downloadUri = "https://sourceforge.net/projects/nsis/files/NSIS%203/3.12/nsis-3.12.zip/download"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$toolRoot = Join-Path $repositoryRoot "artifacts\tools"
$archivePath = Join-Path $toolRoot "nsis-$version.zip"
$archiveIdentity = $expectedSha256.Substring(0, 12).ToLowerInvariant()
$extractionRoot = Join-Path $toolRoot "nsis-$version-$archiveIdentity"
$compilerPath = Join-Path $extractionRoot "nsis-$version\Bin\makensis.exe"

if (Test-Path -LiteralPath $compilerPath -PathType Leaf) {
    $cachedCompilerSha256 = (Get-FileHash -LiteralPath $compilerPath -Algorithm SHA256).Hash
    if (-not $cachedCompilerSha256.Equals(
        $expectedCompilerSha256,
        [StringComparison]::OrdinalIgnoreCase)) {
        throw "The cached NSIS compiler failed its integrity check. Delete '$extractionRoot' and retry."
    }

    Write-Output $compilerPath
    exit 0
}

New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    $currentUri = [Uri] $downloadUri
    $downloaded = $false

    for ($redirect = 0; $redirect -lt 5; $redirect++) {
        $candidatePath = "$archivePath.pending-$([Guid]::NewGuid().ToString('N'))"
        try {
            Invoke-WebRequest -Uri $currentUri -OutFile $candidatePath

            if ($PSVersionTable.PSVersion.Major -ge 7) {
                $signature = Get-Content -LiteralPath $candidatePath -AsByteStream -TotalCount 4
            }
            else {
                $signature = Get-Content -LiteralPath $candidatePath -Encoding Byte -TotalCount 4
            }

            if ($signature.Length -eq 4 -and
                $signature[0] -eq 0x50 -and
                $signature[1] -eq 0x4B) {
                Move-Item -LiteralPath $candidatePath -Destination $archivePath
                $downloaded = $true
                break
            }

            $html = Get-Content -LiteralPath $candidatePath -Raw
            $refresh = [regex]::Match(
                $html,
                '(?i)<meta\s+http-equiv="refresh"\s+content="[^"]*url=([^"]+)"')
            if (-not $refresh.Success) {
                throw "The NSIS download endpoint returned neither a ZIP archive nor a supported SourceForge redirect."
            }

            $nextUri = [Uri] [Net.WebUtility]::HtmlDecode($refresh.Groups[1].Value)
            $isAllowedSourceForgeHost =
                $nextUri.Host.Equals("sourceforge.net", [StringComparison]::OrdinalIgnoreCase) -or
                $nextUri.Host.Equals("downloads.sourceforge.net", [StringComparison]::OrdinalIgnoreCase) -or
                $nextUri.Host.EndsWith(".dl.sourceforge.net", [StringComparison]::OrdinalIgnoreCase)
            if ($nextUri.Scheme -ne [Uri]::UriSchemeHttps -or
                -not $isAllowedSourceForgeHost) {
                throw "The NSIS download endpoint returned an untrusted redirect."
            }

            $currentUri = $nextUri
        }
        finally {
            if (Test-Path -LiteralPath $candidatePath) {
                Remove-Item -LiteralPath $candidatePath -Force
            }
        }
    }

    if (-not $downloaded) {
        throw "The NSIS archive could not be downloaded after following the allowed SourceForge redirects."
    }
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
