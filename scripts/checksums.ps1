[CmdletBinding()]
param(
    [Parameter(Mandatory)][string[]]$Files,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $repositoryRoot 'artifacts\SHA256SUMS.txt' }
$lines = foreach ($file in $Files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Checksum input not found: $file" }
    $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($file))"
}
$lines | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
Write-Host "Checksums: $OutputPath"
