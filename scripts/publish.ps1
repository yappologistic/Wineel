[CmdletBinding()]
param(
    [string]$Version,
    [switch]$SkipArchive
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props')
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$buildProperties.Project.PropertyGroup.VersionPrefix }
if ([string]::IsNullOrWhiteSpace($Version)) { throw 'VersionPrefix is missing from Directory.Build.props.' }
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishRoot = Join-Path $artifactsRoot 'publish\win-x64'
$zipPath = Join-Path $artifactsRoot "Wineel-$Version-win-x64-portable.zip"

if (Test-Path -LiteralPath $publishRoot) {
    $resolvedPublishRoot = [System.IO.Path]::GetFullPath($publishRoot)
    $resolvedArtifactsRoot = [System.IO.Path]::GetFullPath($artifactsRoot)
    if (-not $resolvedPublishRoot.StartsWith($resolvedArtifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove an output directory outside artifacts: $resolvedPublishRoot"
    }
    Remove-Item -LiteralPath $resolvedPublishRoot -Recurse -Force
}
if (-not $SkipArchive -and (Test-Path -LiteralPath $zipPath)) { Remove-Item -LiteralPath $zipPath -Force }

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
Push-Location $repositoryRoot
try {
    dotnet publish src/Wineel.App/Wineel.App.csproj -c Release -r win-x64 --self-contained true `
        -p:Platform=x64 -p:Version=$Version -p:PublishReadyToRun=true -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    if (-not $SkipArchive) { Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal }
}
finally { Pop-Location }

Write-Host "Published: $publishRoot"
if (-not $SkipArchive) { Write-Host "Portable ZIP: $zipPath" }
