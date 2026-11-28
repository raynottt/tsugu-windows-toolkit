<p align="center">
  <img src="docs/badges/tsugu.svg" alt="TSUGU — Windows toolkit" />
</p>

<p align="center">
  <img src="docs/badges/windows.svg" alt="Windows 10 and 11" />
  <img src="docs/badges/dotnet.svg" alt=".NET 10" />
  <img src="docs/badges/winui.svg" alt="WinUI 3" />
  <img src="docs/badges/source.svg" alt="Build from source" />
</p>

# TSUGU

**A practical Windows desktop toolkit for setup, maintenance and recovery.**

TSUGU brings everyday Windows tools into one native WinUI 3 app: package management, startup review, cleanup, file search, PATH checks, system tuning, repair and diagnostics. It favours explicit actions, clear explanations and recoverable changes over opaque one-click “optimizers”.

## Project status

TSUGU is under active development. The source and CI workflows live in this repository. Preview installers and portable builds will be published on [GitHub Releases](https://github.com/raynottt/tsugu-windows-toolkit/releases) when a release is ready. The reinstall-profile export/import workflow is planned, but is not available yet.

## What TSUGU does

| Area | What it is for |
| --- | --- |
| **Install** | Search package sources and install applications through a visible operation queue. |
| **Installed** | Review installed applications and manage supported package operations. |
| **Cleanup** | Inspect temporary files and other supported cleanup targets before removing them. |
| **Startup** | Review startup entries and use supported enable/disable and restore actions. |
| **Quiet** | Adjust selected background and Windows experience settings through reviewed options. |
| **Tune** | Change selected native settings with original values and restore support where available. |
| **Fixes / Unstick** | Run narrowly scoped troubleshooting and repair actions from reviewed catalogs. |
| **Scout** | Search indexed files and selected folders without reading file contents or deleting anything. |
| **Paths** | Inspect and safely manage the user and machine PATH. |
| **Repair** | Diagnose and repair supported Windows Installer registrations and application files. |
| **Crashes / Logs** | Review Windows crash events and TSUGU's own activity records. |
| **Lab / Terminal** | Use selected diagnostics and setup helpers without turning the app into an unrestricted script runner. |

Feature coverage depends on Windows version, installed tools and the selected operation. Read the explanation shown in the app before applying system changes.

## Safety model

- The desktop app runs as the signed-in user; it does not ask to run fully elevated.
- Administrator-only work is delegated to a separate helper and starts only after an explicit Windows UAC approval.
- System changes are constrained by reviewed catalogs and allowlists rather than arbitrary command text.
- Where a change can be reversed, TSUGU saves the original state or a recovery record before applying it.
- Security protections and Windows servicing are not disabled as a blanket performance tweak.

A helper or recovery record cannot make every Windows operation reversible. Back up important data before maintenance or repair.

## Technology

- C# and .NET 10
- WinUI 3 and Windows App SDK
- Native Windows desktop APIs; no embedded web renderer
- Separate Core, Desktop, AdminHost and SearchHost projects
- x64 Windows 10 (build 19041 or later) and Windows 11

## Build from source

Install the .NET 10 SDK and use a Windows machine with the Windows App SDK build prerequisites.

```powershell
git clone https://github.com/raynottt/tsugu-windows-toolkit.git
cd tsugu-windows-toolkit

dotnet restore TSUGU.slnx --locked-mode
dotnet build TSUGU.slnx --configuration Release --no-restore
dotnet test tests/TSUGU.Tests/TSUGU.Tests.csproj --configuration Release --no-build
```

To run the desktop app from source:

```powershell
dotnet run --project src/TSUGU.Desktop/TSUGU.Desktop.csproj
```

To publish a self-contained app and build the Inno Setup installer, first publish the desktop project and then run `installer/build.ps1`. The release workflow contains the complete packaging sequence.

## Repository map

- `src/TSUGU.Core` — Windows operations, package services, catalogs and update logic.
- `src/TSUGU.Desktop` — WinUI 3 application and feature pages.
- `src/TSUGU.AdminHost` — narrowly scoped elevated helper.
- `src/TSUGU.SearchHost` — Scout worker process.
- `tests/TSUGU.Tests` — unit and safety-boundary tests.
- `tests/TSUGU.UiSmoke` — WinUI smoke-test application.
- `docs/` — architecture, research and validation notes.
- `installer/` — Inno Setup definition and packaging scripts.

## Documentation

- [Architecture and behavior](docs/architecture.md)
- [Integration and product plan](docs/INTEGRATION_PLAN.md)
- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Accessibility](ACCESSIBILITY.md)
- [Third-party notices](THIRD_PARTY_NOTICES.md)

## License and third-party notices

TSUGU contains original project work and adapted third-party components. Their copyright and license notices are retained in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `LICENSES/` directory. Please keep those notices with redistributed builds.
