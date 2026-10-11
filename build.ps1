# Runs the tests, then builds QuasiRobotics in Release and deploys it to LocalUserPresets.
# Usage: .\build.ps1 [-SkipTests] [-NoDeploy]
param(
    [switch]$SkipTests,
    [switch]$NoDeploy
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not $SkipTests) {
    Write-Host '== Tests ==' -ForegroundColor Cyan
    dotnet run --project tests/QuasiRobotics.Tests -c Release
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Tests failed. Nothing deployed.' -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

Write-Host '== Build ==' -ForegroundColor Cyan
$deploy = if ($NoDeploy) { 'false' } else { 'true' }
dotnet build src/QuasiRobotics -c Release "-p:Deploy=$deploy"
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Build failed.' -ForegroundColor Red
    exit $LASTEXITCODE
}

if (-not $NoDeploy) {
    $target = Join-Path $env:USERPROFILE 'AppData\LocalLow\Magnum Scriptum Ltd\Quasimorph\LocalUserPresets\QuasiRobotics'
    Write-Host "Deployed to $target. Restart the game to load it." -ForegroundColor Green
} else {
    Write-Host 'Built to workshop\. Not deployed.' -ForegroundColor Green
}
