# Build VpnTraffic MSIX for local sideload
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "0.1.0.0",
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"

# Ensure NuGet can resolve machine-wide settings in stripped environments
if (-not $env:ProgramFiles) { $env:ProgramFiles = "C:\Program Files" }
if (-not $env:ProgramW6432) { $env:ProgramW6432 = "C:\Program Files" }

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "VpnTraffic\VpnTraffic.csproj"
$out = Join-Path $root "artifacts\$Configuration\win-$Platform"
$payload = Join-Path $root "artifacts\msix-payload"
$msix = Join-Path $root "artifacts\VpnTraffic_$Version_$Platform.msix"
$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"

Write-Host "Publishing $proj" -ForegroundColor Cyan
dotnet publish $proj -c $Configuration -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

if ($SkipPack) {
    Write-Host "Skip pack. Output: $out"
    return
}

Write-Host "Preparing payload" -ForegroundColor Cyan
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Path $payload | Out-Null
Copy-Item -Path (Join-Path $out "*") -Destination $payload -Recurse -Force

# Ensure AppxManifest.xml is present with correct name
$manifestSrc = Join-Path $root "VpnTraffic\Package.appxmanifest"
Copy-Item $manifestSrc (Join-Path $payload "AppxManifest.xml") -Force

if (-not (Test-Path $makeappx)) { throw "makeappx not found: $makeappx" }
& $makeappx pack /d $payload /p $msix /o
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }

Write-Host "MSIX: $msix" -ForegroundColor Green
