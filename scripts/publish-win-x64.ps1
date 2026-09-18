<#
.SYNOPSIS
  Publish WindowsMediaSwitcher (unpackaged, self-contained win-x64) and zip to dist/.

.NOTES
  Must run on Windows with:
    - .NET 8 SDK
    - Windows App SDK workload / VS2022 "Windows application development"
  This script will NOT succeed on Linux.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
if (-not $Root) { $Root = Resolve-Path (Join-Path $PSScriptRoot "..") }

$Project = Join-Path $Root "src\WindowsMediaSwitcher\WindowsMediaSwitcher.csproj"
$OutDir  = Join-Path $Root "artifacts\publish\$Runtime"
$DistDir = Join-Path $Root "dist"
$ZipPath = Join-Path $DistDir "WindowsMediaSwitcher-win-x64.zip"

Write-Host "==> Project : $Project"
Write-Host "==> Output  : $OutDir"
Write-Host "==> Zip     : $ZipPath"

if (-not [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)) {
    Write-Error "This publish script must run on Windows (WinUI 3 / Windows App SDK)."
}

if (-not (Test-Path $Project)) {
    Write-Error "Project not found: $Project"
}

if (Test-Path $OutDir) {
    Remove-Item -Recurse -Force $OutDir
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null

Write-Host "==> dotnet restore"
dotnet restore $Project

Write-Host "==> dotnet publish"
dotnet publish $Project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:WindowsPackageType=None `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishReadyToRun=false `
    -o $OutDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}

if (Test-Path $ZipPath) {
    Remove-Item -Force $ZipPath
}

Write-Host "==> Compressing zip"
Compress-Archive -Path (Join-Path $OutDir "*") -DestinationPath $ZipPath -Force

$exe = Join-Path $OutDir "WindowsMediaSwitcher.exe"
Write-Host ""
Write-Host "Done."
Write-Host "  EXE : $exe"
Write-Host "  ZIP : $ZipPath"
Write-Host "Run the EXE (unpackaged). Tray icon appears in the notification area."
