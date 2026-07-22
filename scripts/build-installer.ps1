[CmdletBinding()]
param(
    [string]$Version,
    [string]$InnoSetupCompiler
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
[xml]$buildProperties = Get-Content (Join-Path $repositoryRoot 'Directory.Build.props')
if ([string]::IsNullOrWhiteSpace($Version)) { $Version = [string]$buildProperties.Project.PropertyGroup.VersionPrefix }
if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler)) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
    )
    $InnoSetupCompiler = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoSetupCompiler) -or -not (Test-Path -LiteralPath $InnoSetupCompiler)) {
    throw 'Inno Setup 6 compiler was not found. Install Inno Setup or pass -InnoSetupCompiler.'
}
$installerPath = Join-Path $repositoryRoot "artifacts\installer\Wineel-$Version-win-x64-setup.exe"
if (Test-Path -LiteralPath $installerPath) { Remove-Item -LiteralPath $installerPath -Force }
$process = Start-Process -FilePath $InnoSetupCompiler `
    -ArgumentList "/DMyAppVersion=$Version", (Join-Path $repositoryRoot 'installer\Wineel.iss') `
    -Wait -PassThru -WindowStyle Hidden
if ($process.ExitCode -ne 0) { throw "Inno Setup failed with exit code $($process.ExitCode)." }
$deadline = [DateTimeOffset]::UtcNow.AddMinutes(10)
while (Get-Process -Name ISCC -ErrorAction SilentlyContinue) {
    if ([DateTimeOffset]::UtcNow -ge $deadline) { throw 'Inno Setup did not finish within 10 minutes.' }
    Start-Sleep -Milliseconds 250
}
if (-not (Test-Path -LiteralPath $installerPath)) { throw "Inno Setup completed without producing $installerPath." }
Write-Host "Installer: $installerPath"
