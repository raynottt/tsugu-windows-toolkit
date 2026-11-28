# TSUGU product and integration plan

This document describes the product boundaries and the work that remains. The repository is the source of truth for the current implementation; the feature pages and tests may evolve as the preview develops.

## Product goal

TSUGU is a native Windows desktop toolkit for software setup, maintenance, troubleshooting and recovery. It is designed to make supported Windows operations understandable and reviewable, not to apply opaque “optimization” bundles.

The longer-term setup workflow is:

1. **Prepare before reinstall** — inventory applications and export a versioned setup profile.
2. **Restore after reinstall** — compare the profile with the new system and let the user choose which supported preferences and applications to restore.
3. **Maintain Windows** — provide reviewed cleanup, tuning, repair and diagnostic actions.
4. **Keep actions explainable** — show the target, expected effect, warnings and recovery path before an operation starts.

Setup profiles are not disk images or full backups. They must not contain passwords, browser cookies, authentication tokens, private keys, product keys or other secrets. Import must be declarative and reviewable; it must never execute arbitrary commands supplied by a profile.

## Current application structure

- **TSUGU.Core** owns package services, Windows operations, catalogs, update logic and recovery records.
- **TSUGU.Desktop** is the WinUI 3 interface. It runs as the signed-in user.
- **TSUGU.AdminHost** is the narrowly scoped helper for operations that require administrator rights. It is launched only after an explicit UAC approval.
- **TSUGU.SearchHost** runs the Scout search worker separately from the desktop process.
- **TSUGU.Tests** covers Core behavior, safety boundaries and selected desktop logic.
- **TSUGU.UiSmoke** provides a Windows UI smoke-test target.

Build references, project names and application assemblies use the TSUGU identity. Third-party copyright and license notices remain in the repository; see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md).

## Engineering principles

- Keep the desktop app un-elevated. Delegate only the operation that needs administrator rights.
- Keep privileged operations behind a private helper channel and rebuild actions from reviewed catalogs.
- Use explicit queues for package and repair work. Show the package ID, source, version and expected action.
- Prefer supported Windows APIs and settings over undocumented “booster” recipes.
- Save the original value or recovery record before reversible changes.
- Do not silently disable Windows Update, Defender, firewall, sleep, hibernation or power-saving features.
- Keep tests around path validation, package execution, restore behavior and privilege boundaries.

## Planned work

### Setup profiles

Add a versioned schema with migrations and strict validation. Start with application inventory and a small set of supported preferences. Exclude credentials, tokens, browser data, product keys and machine-specific secrets.

### Before/after reinstall experience

Add a guided export step, profile comparison after reinstall and a selected restore/install queue. Missing applications and unsupported settings must be shown rather than silently skipped.

### Verification and release quality

Keep locked dependency restore, Release builds and tests in CI. Before publishing a preview, run the Windows packaging workflow, verify the installer and portable archive, check the UAC boundary and test install/uninstall behavior on a clean Windows environment.

## Non-goals

TSUGU is not a disk imaging tool, an unattended deployment engine or an unrestricted PowerShell runner. It does not promise generic performance gains, and it does not weaken security protections to make a PC appear faster.
