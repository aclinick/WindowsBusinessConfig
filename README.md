# WindowsBusinessConfig

Turn a fresh Windows 11 PC into a calm, distraction-free small-business
workstation in one command. Idempotent. Re-runnable.

Inspired by [microsoft/WindowsDeveloperConfig](https://github.com/microsoft/WindowsDeveloperConfig).
This project keeps the "calm environment" half of that repo and replaces the
developer-tools half with small-business defaults: Microsoft 365 Apps for
Business, Teams, OneDrive, plus consumer-bloat removal and sensible OS policy.

No WSL. No reboot. One file.

## What you get

**Calm environment**
- Dark theme
- File Explorer: show extensions, full path titlebar, open to This PC, kill
  Quick Access frequent files and cloud recommendations
- Notifications: Do Not Disturb on by default
- Taskbar: hide Widgets
- Start and Search: kill web suggestions, search highlights, Start
  recommendations
- Widgets service disabled
- Edge: blank new tab, first-run experience suppressed

**Bloat removal**
- HKLM consumer-features policies (block silent install of Candy Crush, the
  Spotify stub, and friends)
- Per-user ContentDeliveryManager keys (kill Start, Settings, lock-screen
  suggestions and rotating ads)
- Game Bar and Game DVR off
- Aggressive Appx removal: Xbox suite, Solitaire, Zune Music and Video, Bing
  News and Weather, Get Help, Get Started, Mixed Reality Portal, Your Phone,
  People, Feedback Hub, Mail and Calendar (assumes Outlook from M365)

**Sensible defaults**
- Power: never sleep on AC, screen off after 20 minutes on AC and 10 on
  battery
- BitLocker status check (warns if off, never auto-enables)

**Business apps via winget**
- Microsoft 365 Apps for Business (`Microsoft.Office`)
- Microsoft Teams (`Microsoft.Teams`)
- OneDrive sync client (`Microsoft.OneDrive`)
- Adobe Acrobat Reader (`Adobe.Acrobat.Reader.64-bit`)
- Windows Terminal (`Microsoft.WindowsTerminal`)

## Apply it on a single PC

Open an elevated PowerShell prompt and run:

```powershell
git clone https://github.com/aclinick/WindowsBusinessConfig.git
cd WindowsBusinessConfig
winget configure -f .\business-config\business-config.winget --accept-configuration-agreements --disable-interactivity
```

If Git is not installed, download the repo as a ZIP and extract it first.

If `winget configure` is not recognized, install or update App Installer from
the Microsoft Store, then ensure the Visual C++ Redistributable is present:

```powershell
# x64
winget install Microsoft.VCRedist.2015+.x64
# ARM64
winget install Microsoft.VCRedist.2015+.arm64
```

## Deploy across a fleet via Intune

The full guide is in [docs/intune-deployment.md](docs/intune-deployment.md).
Short version:

1. Tag a release in this repo and attach `business-config.winget` to it.
2. Upload `intune/Apply-BusinessConfig.ps1` as an Intune Platform Script,
   set to run as the logged-on user.
3. For ongoing drift correction, also upload
   `intune/Detect-BusinessConfig.ps1` and `intune/Remediate-BusinessConfig.ps1`
   as a Remediation pair.

Both patterns assume the assigned user is a local administrator on the target
device, which is the common SMB scenario.

## Why a winget config and not an MSIX or .intunewin

The deliverable is a YAML configuration document that `winget configure`
already knows how to apply. Wrapping it in an MSIX, an `.intunewin` Win32
package, or any other installer adds layers without adding capability. Intune
runs the document directly via a small PowerShell script. Updates ship by
tagging a new release.

## Compose your own

`business-config/business-config.winget` is a plain DSC v3 document. Read it,
fork it, comment out the resources you do not want. Every resource is
idempotent, so re-running the configuration after edits is safe.

## Acknowledgements

Built on top of the work in
[microsoft/WindowsDeveloperConfig](https://github.com/microsoft/WindowsDeveloperConfig).
The "calm environment" registry tweaks, the dark-theme script pattern, and the
Edge policy keys are taken almost verbatim from that repo. Licensed under MIT
to match the upstream.
