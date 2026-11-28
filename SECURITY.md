# Security policy

TSUGU changes Windows settings, manages software and delegates a limited set of operations to an administrator helper. Security reports are taken seriously and should be shared responsibly.

## Supported versions

| Version | Support |
| --- | --- |
| Current 0.x preview on `main` | Supported; fixes are made in the next preview or release. |
| Older preview builds | Please update to the latest available build before reporting. |

Use the [latest GitHub release](https://github.com/raynottt/tsugu-windows-toolkit/releases/latest) when one is available. When no release exists, report the commit or build you tested.

## Reporting a vulnerability

**Do not open a public issue, pull request or discussion for a security problem.**

Use [GitHub Security Advisories](https://github.com/raynottt/tsugu-windows-toolkit/security/advisories/new) to send a private report. Include:

- The TSUGU version or commit and how the app was installed.
- Your Windows version and build.
- What access an attacker needs and what they could achieve.
- Reproduction steps and a proof of concept, where possible.

Review logs before attaching them: they can contain local paths, package names and stack traces.

## What happens next

TSUGU is maintained by a small team. We aim to acknowledge reports within **7 days**, confirm whether the issue can be reproduced and publish a fix or a clear explanation. Reporter credit is optional.

Please give us a reasonable chance to release a fix before sharing details publicly.

## Scope

Reports are especially useful for components that cross a privilege boundary or hold state that must be trusted:

- **Administrator helper** (`TSUGU.AdminHost.exe`) and its named pipe, including attempts to connect from another account or make it run an operation outside its catalogs.
- **Catalog and allowlist checks** in Cleanup, Quiet, Tune, Fixes and Lab.
- **Saved originals and recovery records**, including attempts to tamper with data that the helper restores with administrator rights.
- **Package manager and PATH handling**, including attempts to launch programs from locations that an ordinary account can modify.
- **Release integrity**, including the installer, portable archive, checksums and build provenance.

### Known limitations (not vulnerabilities)

- **Per-user and portable installs.** The app and helper may be stored in a folder your account can write to. A program running as you could replace the helper before a later UAC prompt. The default all-users install under Program Files offers stronger protection.
- **Running as administrator.** The desktop app refuses to run with a full administrator token. This is intentional.
- **Logs.** Logs can contain local paths, package names and stack traces. Review them before sharing.
- **Third-party tools.** Vulnerabilities in WinGet, Chocolatey, Scoop, Everything or packages they install should be reported to those projects. Let us know too if TSUGU makes the issue worse.

Out of scope are attacks that already require administrator rights on the PC, physical access to an unlocked PC, social engineering and denial of service against your own machine.
