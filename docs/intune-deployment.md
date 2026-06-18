# Intune deployment guide

WindowsBusinessConfig is a single `winget configure` document. Intune deploys it
by running `winget configure` against the document on each target device. There
are two supported patterns; pick whichever matches your operational model.

The shipped scripts assume the assigned user is a local administrator on the
target device, which is the common small-business scenario. UAC will prompt
once per run when winget elevates for the system-scoped resources.

## Prerequisites

- Windows 11 22H2 or later, or Windows 10 with App Installer 1.6+ installed.
- App Installer (`winget`) reachable from the user context.
- Microsoft Visual C++ Redistributable installed (App Installer ships against
  it). On a fresh image, run once:
  - x64: `winget install Microsoft.VCRedist.2015+.x64`
  - ARM64: `winget install Microsoft.VCRedist.2015+.arm64`
- A GitHub release hosting both `business-config.winget` and the matching
  `business-config.winget.sha256` sidecar. The scripts pin to a specific
  release tag (default `v1.0.0`) and verify SHA-256 against the sidecar
  before invoking `winget configure`. To roll forward, bump the
  `-ReleaseTag` parameter and republish.

## Pattern 1: Platform Script (one-shot install)

Use this when you want to apply the configuration once per device, typically at
enrollment.

1. Microsoft Intune admin center > Devices > Scripts and remediations >
   Platform scripts > Add > Windows 10 and later.
2. Upload `intune/Apply-BusinessConfig.ps1`.
3. Settings:
   - Run this script using the logged-on credentials: **Yes**
   - Enforce script signature check: **No** (unless you signed the script)
   - Run script in 64-bit PowerShell host: **Yes**
4. Assignments: target a user group (the user must be a local admin on the
   device).

Intune retries failed runs up to three times. Subsequent reruns are a no-op
because every resource in the configuration is idempotent.

## Pattern 2: Remediation (continuous enforcement)

Use this when you want ongoing drift detection and automatic correction.

1. Microsoft Intune admin center > Devices > Scripts and remediations >
   Remediations > Create.
2. Detection script file: `intune/Detect-BusinessConfig.ps1`.
3. Remediation script file: `intune/Remediate-BusinessConfig.ps1`.
4. Settings:
   - Run this script using the logged-on credentials: **Yes**
   - Enforce script signature check: **No** (unless you signed the scripts)
   - Run script in 64-bit PowerShell host: **Yes**
5. Schedule: daily is usually sufficient. Hourly works, but each run downloads
   the config and invokes `winget configure test`, which is non-trivial.
6. Assignments: same user group as Pattern 1.

The detection script returns exit 0 (compliant) only when both:
- The install marker registry key is present, and
- `winget configure test` reports no drift.

## Customizing the release tag and base URL

All three scripts accept `-ReleaseTag`, `-BaseUrl`, and `-AssetName`
parameters. The defaults point at this repository's `v1.0.0` release. To
target a different release or a private mirror, edit the `param()` block at
the top of each script before uploading to Intune. Platform Scripts and
Remediations do not pass parameters at runtime.

## Why the hash check matters

A `releases/latest/download/...` URL is mutable: anyone who can publish a
release in the owning repo can replace the asset, and Intune devices would
silently pick up the new payload. The shipped scripts pin to a specific
release tag and verify SHA-256 against a `business-config.winget.sha256`
sidecar in the same release. If you fork this project, generate the sidecar
during release:

```powershell
$h = (Get-FileHash -Path business-config.winget -Algorithm SHA256).Hash
"$h  business-config.winget" | Set-Content business-config.winget.sha256 -Encoding ASCII
```

Upload both files to the release before the Intune scripts are activated.

## Logging and troubleshooting

- Platform Script and Remediation output is captured by the Intune Management
  Extension and visible per-device in the admin center under Devices >
  *device* > Managed Apps / Device diagnostics.
- Local logs:
  `C:\ProgramData\Microsoft\IntuneManagementExtension\Logs\IntuneManagementExtension.log`
  and `AgentExecutor.log` in the same folder.
- `winget configure` writes its own log under
  `%LOCALAPPDATA%\Packages\Microsoft.DesktopAppInstaller_*\LocalState\DiagOutputDir\`.

## What if `winget` is not present?

Both scripts fail fast if `winget.exe` is not on PATH and not at the well-known
`%LOCALAPPDATA%\Microsoft\WindowsApps\winget.exe` location. To bootstrap App
Installer on devices missing it, deploy the
[Microsoft.DesktopAppInstaller](https://learn.microsoft.com/windows/package-manager/winget/)
LOB app via Intune first, or add a one-line `Add-AppxPackage` shim to the top
of `Apply-BusinessConfig.ps1`.
