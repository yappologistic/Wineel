[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CertificatePath,
    [Parameter(Mandatory)][string]$CertificatePassword,
    [Parameter(Mandatory)][string[]]$Files,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Signing certificate not found: $CertificatePath" }
$signTool = (Get-Command signtool.exe -ErrorAction SilentlyContinue)?.Source
if (-not $signTool) {
    $kitRoot = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
    $signTool = Get-ChildItem -LiteralPath $kitRoot -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -ExpandProperty FullName -First 1
}
if (-not $signTool) { throw 'signtool.exe was not found in PATH or the Windows 10 SDK.' }
foreach ($file in $Files) {
    if (-not (Test-Path -LiteralPath $file)) { throw "File to sign was not found: $file" }
    & $signTool sign /fd SHA256 /f $CertificatePath /p $CertificatePassword /tr $TimestampUrl /td SHA256 $file
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $file with exit code $LASTEXITCODE." }
    & $signTool verify /pa /v $file
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $file with exit code $LASTEXITCODE." }
}
