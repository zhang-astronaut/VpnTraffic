# Build unpackaged publish + Inno installer for WinGet
param(
    [string]$Configuration = "Release",
    [string]$Version = "0.2.0.0"
)

$ErrorActionPreference = "Stop"
if (-not $env:ProgramFiles) { $env:ProgramFiles = "C:\Program Files" }
if (-not $env:ProgramW6432) { $env:ProgramW6432 = "C:\Program Files" }

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "VpnTraffic\VpnTraffic.csproj"
$out = Join-Path $root "artifacts\publish\win-x64"
$installerDir = Join-Path $root "artifacts\installer"

$Iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $Iscc) { throw "Inno Setup ISCC.exe not found. Install JRSoftware.InnoSetup." }

Write-Host "Publish unpackaged self-contained..." -ForegroundColor Cyan
dotnet publish $proj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:WindowsPackageType=None `
    -p:AppxPackage=false `
    -p:GenerateAppxPackageOnBuild=false `
    -p:AppxPackageSigningEnabled=false `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Compiling Inno installer..." -ForegroundColor Cyan
Push-Location $root
try {
    & $Iscc "/DAppVersion=$Version" "setup.iss"
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }
}
finally {
    Pop-Location
}

$setup = Get-ChildItem $installerDir -Filter "VpnTraffic-Setup-*.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $setup) { throw "Installer not found in $installerDir" }
$hash = (Get-FileHash $setup.FullName -Algorithm SHA256).Hash
Write-Host "Installer: $($setup.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($setup.Length/1MB,2)) MB"
Write-Host "SHA256: $hash"
