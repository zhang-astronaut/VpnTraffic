# Sign + sideload MSIX (certificate import may require elevation)
param(
    [string]$Version = "0.1.0.0",
    [string]$Platform = "x64",
    [string]$CertName = "VpnTraffic Dev"
)

$ErrorActionPreference = "Stop"
if (-not $env:ProgramFiles) { $env:ProgramFiles = "C:\Program Files" }

$root = Split-Path -Parent $PSScriptRoot
$msix = Join-Path $root "artifacts\VpnTraffic_$Version_$Platform.msix"
if (-not (Test-Path $msix)) {
    $candidate = Get-ChildItem (Join-Path $root "artifacts") -Filter "VpnTraffic_*.msix" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($candidate) { $msix = $candidate.FullName }
}

$cer = Join-Path $root "artifacts\VpnTraffic.cer"
$pfx = Join-Path $root "artifacts\VpnTraffic.pfx"
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe"

if (-not (Test-Path $msix)) { throw "MSIX not found. Run build-msix.ps1 first. Looked at $msix" }

if (-not (Test-Path $pfx)) {
    Write-Host "Creating self-signed cert..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=VpnTraffic" `
        -KeyUsage DigitalSignature -FriendlyName $CertName `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
    $pwd = ConvertTo-SecureString -String "VpnTraffic" -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pwd | Out-Null
    Export-Certificate -Cert $cert -FilePath $cer | Out-Null
}

Write-Host "Signing MSIX..." -ForegroundColor Cyan
& $signtool sign /f $pfx /p "VpnTraffic" /fd SHA256 $msix
if ($LASTEXITCODE -ne 0) { throw "sign failed" }

# Prefer elevated trust+install; fall back to current user if already trusted.
$elevateScript = Join-Path $env:TEMP "vpntraffic-install.ps1"
@"
certutil -addstore -f Root '$cer' | Out-Null
certutil -addstore -f TrustedPeople '$cer' | Out-Null
Get-AppxPackage -Name VpnTraffic -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
Add-AppxPackage -Path '$msix'
Get-AppxPackage -Name VpnTraffic | Format-List Name, Version, InstallLocation
"@ | Set-Content $elevateScript -Encoding UTF8

Write-Host "Requesting elevation to trust cert + install..." -ForegroundColor Cyan
$p = Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $elevateScript -PassThru -Wait
if ($p.ExitCode -ne 0) {
    Write-Warning "Elevated install exit=$($p.ExitCode). Trying Add-AppxPackage without new cert trust..."
    Add-AppxPackage -Path $msix
}

Get-AppxPackage -Name VpnTraffic | Format-List Name, Version, InstallLocation
Write-Host "Done. Open Command Palette → Reload Command Palette Extension." -ForegroundColor Green
