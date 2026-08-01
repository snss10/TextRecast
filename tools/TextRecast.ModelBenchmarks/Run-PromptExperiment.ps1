[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('prompt-development', 'prompt-validation')]
    [string] $Scope,

    [Parameter(Mandatory)]
    [string] $ModelPath,

    [Parameter(Mandatory)]
    [string] $BaselineResultPath,

    [Parameter(Mandatory)]
    [string] $PromptProfile,

    [Parameter(Mandatory)]
    [string] $OutputPath,

    [ValidateRange(1, 10)]
    [int] $Iterations = 1
)

$ErrorActionPreference = 'Stop'
$projectDirectory = $PSScriptRoot
$repositoryRoot = (Resolve-Path (Join-Path $projectDirectory '..\..')).Path
$projectPath = Join-Path $projectDirectory 'TextRecast.ModelBenchmarks.csproj'
$resolvedModelPath = (Resolve-Path -LiteralPath $ModelPath).Path
$resolvedBaselinePath = (Resolve-Path -LiteralPath $BaselineResultPath).Path
$baseline = Get-Content -Raw -LiteralPath $resolvedBaselinePath | ConvertFrom-Json
$resolvedOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutputPath

if ($resolvedOutputPath.Equals($resolvedBaselinePath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The experiment output path cannot overwrite the baseline result.'
}

if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$benchmarkArguments = @(
    '--model', $resolvedModelPath,
    '--model-id', $baseline.ModelId,
    '--adapter', $baseline.AdapterId,
    '--prompt-profile', $PromptProfile,
    '--output', $resolvedOutputPath,
    '--source-repo', $baseline.Source.Repository,
    '--source-revision', $baseline.Source.Revision,
    '--source-license', $baseline.Source.License,
    '--quantization', $baseline.Source.Quantization,
    '--expected-sha', $baseline.Source.Sha256,
    '--expected-size', $baseline.Source.FileSizeBytes,
    '--context', $baseline.Environment.ContextSize,
    '--max-output', $baseline.Environment.MaxOutputTokens,
    '--threads', $baseline.Environment.Threads,
    '--iterations', $Iterations,
    '--corpus-scope', $Scope
)

Push-Location $repositoryRoot
try {
    & dotnet run --project $projectPath --configuration Release --no-restore -- @benchmarkArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Prompt experiment failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
