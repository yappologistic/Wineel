[CmdletBinding()]
param([string]$Version)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props')
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$buildProperties.Project.PropertyGroup.VersionPrefix }
$publishRoot = Join-Path $repositoryRoot 'artifacts\publish\win-x64'
$zipPath = Join-Path $repositoryRoot "artifacts\Wineel-$Version-win-x64-portable.zip"
if (-not (Test-Path -LiteralPath (Join-Path $publishRoot 'Wineel.exe'))) { throw "Published Wineel.exe was not found at $publishRoot." }
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -Path (Join-Path $publishRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "Portable ZIP: $zipPath"
