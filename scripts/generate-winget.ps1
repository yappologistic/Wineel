[CmdletBinding()]
param(
    [string]$Version,
    [string]$InstallerPath,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props')
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$buildProperties.Project.PropertyGroup.VersionPrefix }
if ([string]::IsNullOrWhiteSpace($InstallerPath)) { $InstallerPath = Join-Path $repositoryRoot "artifacts\installer\Wineel-$Version-win-x64-setup.exe" }
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $repositoryRoot "artifacts\winget\$Version" }
if (-not (Test-Path -LiteralPath $InstallerPath)) { throw "Installer not found: $InstallerPath" }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$hash = (Get-FileHash -LiteralPath $InstallerPath -Algorithm SHA256).Hash
$manifest = @"
# yaml-language-server: `$schema=https://aka.ms/winget-manifest.singleton.1.6.0.schema.json
PackageIdentifier: yappologistic.Wineel
PackageVersion: $Version
PackageLocale: en-US
Publisher: yappologistic
PackageName: Wineel
License: Proprietary
ShortDescription: Fast native radial application switcher for Windows.
PackageUrl: https://github.com/yappologistic/Wineel
InstallerType: inno
Installers:
  - Architecture: x64
    InstallerUrl: https://github.com/yappologistic/Wineel/releases/download/v$Version/Wineel-$Version-win-x64-setup.exe
    InstallerSha256: $hash
ManifestType: singleton
ManifestVersion: 1.6.0
"@
$outputPath = Join-Path $OutputDirectory 'yappologistic.Wineel.yaml'
$manifest | Set-Content -LiteralPath $outputPath -Encoding utf8NoBOM
Write-Host "WinGet manifest: $outputPath"
