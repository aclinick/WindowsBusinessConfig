# Detect-BusinessConfig.ps1
#
# Intune Remediation: detection script.
# Returns exit 0 (compliant) when the system matches the desired
# configuration, exit 1 (non-compliant) otherwise. Writes a single line of
# state to STDOUT for Intune reporting.
#
# Implementation strategy:
#   1. Refuse to run as SYSTEM. HKCU resources cannot be evaluated against
#      the assigned user from the SYSTEM profile.
#   2. Check the install marker key written by the .winget config. If the
#      key is missing, treat as non-compliant without invoking winget.
#   3. If the marker is present, download the pinned release config plus
#      its SHA-256 sidecar, verify the hash, then run `winget configure test`.
#      Test exits 0 only when every resource is in the desired state.

[CmdletBinding()]
param(
    [Parameter()]
    [string]$ReleaseTag  = 'v1.0.0',

    [Parameter()]
    [string]$BaseUrl     = 'https://github.com/aclinick/WindowsBusinessConfig/releases/download',

    [Parameter()]
    [string]$AssetName   = 'business-config.winget',

    [Parameter()]
    [string]$MarkerKey   = 'HKLM:\SOFTWARE\WindowsBusinessConfig',

    [Parameter()]
    [string]$MarkerValue = 'Version'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'

function Assert-NotSystem {
    $cur = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    if ($cur.User.Value -eq 'S-1-5-18') {
        Write-Output 'RunAsSystem'
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

try {
    $version = (Get-ItemProperty -Path $MarkerKey -Name $MarkerValue -ErrorAction Stop).$MarkerValue
} catch {
    Write-Output 'NotInstalled'
    exit 1
}

$winget = Resolve-Winget
if (-not $winget) {
    Write-Output 'WingetMissing'
    exit 1
}

try {
    $config = Get-VerifiedConfig -Tag $ReleaseTag -Base $BaseUrl -Asset $AssetName
} catch {
    Write-Output "DownloadOrHashFailed:$($_.Exception.Message)"
    exit 1
}

& $winget configure test `
    --file $config `
    --accept-configuration-agreements `
    --disable-interactivity `
    --nowarn | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Output "Compliant:$version"
    exit 0
} else {
    Write-Output "Drift:$version"
    exit 1
}
