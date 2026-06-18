# Apply-BusinessConfig.ps1
#
# Intune Platform Script: downloads a pinned WindowsBusinessConfig .winget
# release, verifies its SHA-256 against the matching .sha256 sidecar, then
# applies it via `winget configure`.
#
# Intune deployment settings:
#   - Run this script using the logged-on credentials: Yes
#   - Enforce script signature check: No (unless you sign it)
#   - Run script in 64-bit PowerShell host: Yes
#
# Assumptions:
#   - The signed-in user is a local administrator (typical SMB scenario).
#   - App Installer (winget) is present. On Windows 11 22H2+ it ships in-box.
#   - Network access to the release URL is allowed.
#
# Security:
#   - The default URLs point at a specific tagged release, not "latest".
#     Pinning to a tag plus SHA-256 verification means an attacker who
#     replaces a release asset (or MITMs the download) cannot cause arbitrary
#     PowerShell to run via the DSC document.
#   - To roll forward, bump $ReleaseTag in the param block and redeploy.

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ReleaseTag = 'v1.0.0',

    [Parameter()]
    [string]$BaseUrl    = 'https://github.com/aclinick/WindowsBusinessConfig/releases/download',

    [Parameter()]
    [string]$AssetName  = 'business-config.winget'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

function Assert-NotSystem {
    $cur = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    if ($cur.User.Value -eq 'S-1-5-18') {
        throw "This script must run as the logged-on user, not SYSTEM. Reconfigure the Intune Platform Script with 'Run this script using the logged-on credentials = Yes'."
    }
}

function Resolve-Winget {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidate = Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe" -ErrorAction SilentlyContinue
    if ($candidate) { return $candidate.FullName }
    throw "winget.exe not found. Install App Installer from the Microsoft Store and retry."
}

function Get-VerifiedConfig {
    param([string]$Tag, [string]$Base, [string]$Asset)

    $configUrl = "$Base/$Tag/$Asset"
    $hashUrl   = "$configUrl.sha256"
    $dest      = Join-Path $env:TEMP $Asset
    $hashFile  = "$dest.sha256"

    Write-Host "Downloading $hashUrl"
    Invoke-WebRequest -Uri $hashUrl -OutFile $hashFile -UseBasicParsing
    $expected = ((Get-Content $hashFile -Raw) -split '\s+')[0].Trim().ToUpperInvariant()
    if ($expected -notmatch '^[0-9A-F]{64}$') {
        throw "SHA-256 sidecar at $hashUrl did not contain a valid 64-hex-character digest."
    }

    Write-Host "Downloading $configUrl"
    Invoke-WebRequest -Uri $configUrl -OutFile $dest -UseBasicParsing
    $actual = (Get-FileHash -Path $dest -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $expected) {
        Remove-Item $dest -Force -ErrorAction SilentlyContinue
        throw "SHA-256 mismatch for $configUrl. Expected $expected, got $actual. Refusing to apply the configuration."
    }

    Write-Host "SHA-256 verified: $actual"
    return $dest
}

Assert-NotSystem
$winget = Resolve-Winget
Write-Host "Using winget: $winget"

$config = Get-VerifiedConfig -Tag $ReleaseTag -Base $BaseUrl -Asset $AssetName

Write-Host "Applying configuration..."
& $winget configure `
    --file $config `
    --accept-configuration-agreements `
    --disable-interactivity `
    --nowarn

$code = $LASTEXITCODE
Write-Host "winget configure exited with code $code"
exit $code
