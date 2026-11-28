# Integration plan

## Sources and their roles

| Source | Intended use | Integration rule |
| --- | --- | --- |
| [TSUGU](https://github.com/raynottt/tsugu-windows-toolkit) | Functional baseline: package discovery and install queue, reviewed Windows-operation catalogs, cleanup/repair tooling, history and the privileged helper boundary | Port the relevant Core, Desktop feature and helper projects into this repository, then rename and adapt them deliberately. Preserve upstream MIT notice. |
| [SettingsUI](https://github.com/BobbyESP/SettingsUI) | Visual reference for a Windows 11 Settings-style shell, navigation, page headers, grouped settings rows and colorful iconography | Recreate/adapt the useful XAML patterns for the current WinUI version. Do not reference the archived library directly: it targets .NET 5/6 and Windows App SDK 1.0, while the functional baseline targets .NET 10 and Windows App SDK 2.6.5. Preserve upstream MIT notice if source is copied. |

## Application concept

TSUGU is a Windows setup companion, not an aggressive "one-click optimizer".

1. **Prepare before reinstall** — inventory installed applications and export a reviewable setup profile.
2. **Restore after reinstall** — import that profile, show what is available and what is missing, then let the user choose which supported preferences and applications to restore.
3. **Configure Windows** — group native Windows preferences into understandable cards with the current value, proposed value, explanation and restore path where possible.
4. **Install applications** — search known package sources and run selected installs through a visible, reviewable queue.
5. **Maintain and diagnose** — retain carefully scoped cleanup and repair features from the functional baseline.

The setup profile is not a disk image, system backup or unattended script. It must not export passwords, browser cookies, authentication tokens, private keys, product keys or other secrets. Import must be declarative and reviewable; it must never execute arbitrary commands supplied by a profile.

## Architecture to preserve

- A normal-user WinUI 3 desktop process.
- A separate Core project for package management, catalogs, profile logic and system operations.
- A narrow elevated helper for only those operations that require administrator rights. It is started only after explicit UAC consent; the full desktop app must not run elevated.
- An explicit queue for package and repair operations. Show package ID/source/version and explain the expected action before execution.
- Small reviewed catalogs and allowlists for Windows changes. Save original values or a recovery record when the operation supports restoration.
- Tests around Core behavior and safety boundaries.

Do not flatten these boundaries as part of the UI redesign.

## TSUGU-specific behavior

- Power-saving features and balanced Windows power behavior stay enabled by default.
- Do not silently disable sleep, hibernation, battery saver, modern standby, CPU power management, Windows Update, Defender or firewall.
- Power-related options, if added, must be opt-in, clearly explain trade-offs, show the current setting and provide a restore action where possible.
- Prefer supported Windows settings/APIs over opaque registry "boost" recipes.
- Never promise a performance gain without a measurable, defensible reason.

## UI direction

The shell should feel close to Windows 11 Settings:

- a full, readable navigation pane with clear categories and search;
- page title, subtitle and optional illustration;
- settings grouped into roomy cards/rows with consistent spacing;
- colorful, recognizable icons, not a monochrome glyph rail for every page;
- light, dark and high-contrast support;
- responsive layout and keyboard/accessibility support.

The SettingsUI demo's colored Fluent icon PNGs and module illustrations are useful visual references. Binary assets need to be obtained and reviewed separately before they can be included in this repository.

## Porting sequence

1. **Inventory and attribution** — identify the TSUGU projects, source folders, embedded catalogs, assets, tests and build targets; preserve third-party notices.
2. **Build baseline** — port the Core, Desktop, CleanHost and SearchHost projects with their project references and build configuration. Keep a reproducible .NET 10 / WinUI 3 solution.
3. **Brand and rename** — rename product metadata, namespaces, app IDs, paths and visible strings to TSUGU without changing behavior accidentally.
4. **Shell redesign** — replace the compact icon-rail navigation with the SettingsUI-inspired Settings navigation and page layout while retaining each feature's existing view-model/service wiring.
5. **Setup profile** — add export/import as an explicit, versioned schema. Start with application inventory and a small set of safe supported preferences; add migrations and validation.
6. **Reinstall workflow** — add a before/after checklist, profile comparison and selected restore/install queue.
7. **Verification** — build on Windows with the pinned SDK, run tests and UI smoke checks, inspect package/installer output, and manually verify UAC boundaries and restore behavior.

## Current repository bootstrap

The initial commit only establishes the product direction. The TSUGU implementation and SettingsUI-inspired WinUI shell have not yet been ported or built. No claim of a working application should be made until the Windows build and tests pass.
