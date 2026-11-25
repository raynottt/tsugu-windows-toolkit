# TSUGU

**A Windows setup companion.** Prepare a PC for a clean Windows reinstall, bring the familiar setup back afterwards, tune native settings responsibly, and install the apps you need.

> **Project status:** source code is in `main`; the first Windows installer and portable package are being built by GitHub Actions. The reinstall-profile export/import workflow is still under development.

## Download the Windows preview

The installer and portable ZIP will be listed on [GitHub Releases](https://github.com/raynottt/tsugu-windows-toolkit/releases) after the first Windows packaging workflow succeeds. Until then, the repository contains the source code and build instructions, not a downloadable installer.

The first preview targets Windows 10 (build 19041+) and Windows 11, x64. Back up important data before using maintenance or repair tools. The promised before/after reinstall profile is not included in this first preview yet.

## Product direction

TSUGU is intended to make a Windows reinstall feel less like starting from zero.

- **Before reinstall:** inspect installed applications and export a user-reviewed setup profile.
- **After reinstall:** compare the current PC with that profile, restore supported settings, and install missing applications.
- **Native Windows setup:** organize real Windows settings into clear, reversible groups rather than applying opaque "boost" scripts.
- **App installation:** use package managers and a reviewable queue, with package ID, source and version visible before an operation starts.
- **Safe tuning:** keep sensible defaults. Power-saving features, Windows Update, Defender, firewall and other security protections are not disabled as a blanket optimization.

The profile is a configuration aid, not a disk image or a full backup. It must never contain passwords, tokens, browser cookies, product keys or other secrets.

## Technology direction

- C# / .NET 10
- WinUI 3 and Windows App SDK
- Native Windows controls; no web renderer
- Separate core logic from desktop presentation
- Administrator-only work stays in a narrowly scoped helper process, launched only after an explicit UAC approval

The implementation baseline is being adapted from [TidyWindow](https://github.com/Cosmos-0118/TidyWindow), especially its package-operation queue, reviewed system-change catalogs, helper boundary and recovery-oriented behavior.

The interface direction is inspired by [SettingsUI](https://github.com/BobbyESP/SettingsUI): Windows Settings-style navigation, clear page headers, grouped settings cards and colorful iconography. The old SettingsUI project targets an older Windows App SDK, so its project is not used as a direct dependency; compatible UI patterns will be recreated for the current WinUI 3 stack.

## Safety principles

1. Show what will change before running it.
2. Keep the original value or a restore path where a change is reversible.
3. Do not silently change power plans or disable power-saving features.
4. Keep security controls and Windows servicing enabled by default.
5. Ask for administrator rights only for the specific operation that needs them.
6. Make profile import reviewable; never execute arbitrary commands embedded in a profile.

## Planned areas

- Setup profile: export / import, application inventory and supported preferences
- Install: search, review and install applications
- Windows settings: privacy, appearance, explorer, updates and power preferences
- Maintenance: carefully scoped cleanup and diagnostics
- History: show completed actions and available restore options

See [docs/INTEGRATION_PLAN.md](docs/INTEGRATION_PLAN.md) for the porting sequence and source boundaries.

## Third-party source notices

This project draws on two MIT-licensed projects as references and planned adaptation sources. Their notices are preserved in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the `LICENSES/` directory.
