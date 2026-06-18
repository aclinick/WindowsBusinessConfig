# Remediate-BusinessConfig.ps1
#
# Intune Remediation: remediation script. Invoked when Detect-BusinessConfig
# exits non-zero. Re-applies the configuration. Idempotent: only resources
# that drifted are re-applied by `winget configure`.
#
# Security: download is integrity-checked against a SHA-256 sidecar in the
# same release. See Apply-BusinessConfig.ps1 for the rationale.

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
        Write-Output 'Refusing to remediate as SYSTEM. Reconfigure remediation to run as logged-on user.'
        exit 1
    }
}

function Resolve-Winget {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $candidate = Get-ChildItem -Path "$env:LOCALAPPDATA\Microsoft\WindowsApps\winget.exe" -ErrorAction SilentlyContinue
    if ($candidate) { return $candidate.FullName }
    return $null
}

function Get-VerifiedConfig {
    param([string]$Tag, [string]$Base, [string]$Asset)
    $configUrl = "$Base/$Tag/$Asset"
    $hashUrl   = "$configUrl.sha256"
    $dest      = Join-Path $env:TEMP $Asset
    $hashFile  = "$dest.sha256"

    Invoke-WebRequest -Uri $hashUrl   -OutFile $hashFile -UseBasicParsing
    $expected = ((Get-Content $hashFile -Raw) -split '\s+')[0].Trim().ToUpperInvariant()
    if ($expected -notmatch '^[0-9A-F]{64}$') { throw "Invalid SHA-256 sidecar at $hashUrl" }

    Invoke-WebRequest -Uri $configUrl -OutFile $dest    -UseBasicParsing
    $actual = (Get-FileHash -Path $dest -Algorithm SHA256).Hash.ToUpperInvariant()
    if ($actual -ne $expected) {
        Remove-Item $dest -Force -ErrorAction SilentlyContinue
        throw "SHA-256 mismatch for $configUrl"
    }
    return $dest
}

Assert-NotSystem

$winget = Resolve-Winget
if (-not $winget) {
    Write-Output 'winget.exe not found; cannot remediate.'
    exit 1
}

$config = Get-VerifiedConfig -Tag $ReleaseTag -Base $BaseUrl -Asset $AssetName

& $winget configure `
    --file $config `
    --accept-configuration-agreements `
    --disable-interactivity `
    --nowarn

exit $LASTEXITCODE
