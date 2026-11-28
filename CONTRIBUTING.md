# Contributing to TSUGU

Thanks for helping improve TSUGU. Bug reports, thoughtful feature requests, documentation corrections and focused pull requests are welcome.

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md). Security problems must be reported privately as described in [SECURITY.md](SECURITY.md), not in a public issue.

## Ways to help

- **Report a bug.** [Open a bug report](https://github.com/raynottt/tsugu-windows-toolkit/issues/new/choose). Include the TSUGU version, Windows build, installation type, steps to reproduce and the result you expected.
- **Suggest a feature or catalog change.** Explain the user problem, expected behavior and any relevant Windows documentation. TSUGU keeps system changes scoped to reviewed catalogs.
- **Improve documentation.** Architecture notes, research, validation results and safety explanations are part of the project.
- **Submit a pull request.** For a larger change, open an issue first so the approach can be discussed before implementation.

See issues labelled [good first issue](https://github.com/raynottt/tsugu-windows-toolkit/labels/good%20first%20issue) or [help wanted](https://github.com/raynottt/tsugu-windows-toolkit/labels/help%20wanted).

## Share logs safely

Logs are stored in %LOCALAPPDATA%\TSUGU\Logs. The account name is replaced by %USERPROFILE%, but package names, other local paths and crash stack traces can still appear. Read the log before attaching it and remove anything you do not want to make public.

## Development environment

You need:

- **Windows 10 build 19041 or later, x64**, or Windows 11.
- The **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**. The pinned SDK and roll-forward policy are in [global.json](global.json).
- The Windows App SDK build prerequisites. Package managers such as WinGet, Chocolatey and Scoop are optional for compilation.

Clone and build:

\`\`\`powershell
git clone https://github.com/raynottt/tsugu-windows-toolkit.git
cd tsugu-windows-toolkit

dotnet restore TSUGU.slnx --locked-mode
dotnet build TSUGU.slnx --configuration Release --no-restore
dotnet test tests/TSUGU.Tests/TSUGU.Tests.csproj --configuration Release --no-build
\`\`\`

The desktop app is a WinUI 3 application and must be built on Windows. To start it during development:

\`\`\`powershell
dotnet run --project src/TSUGU.Desktop/TSUGU.Desktop.csproj
\`\`\`

## Project layout

| Folder | Purpose |
| --- | --- |
| src/TSUGU.Core | Package services, Windows operations, reviewed catalogs, update logic and recovery records. |
| src/TSUGU.Desktop | WinUI 3 desktop application, feature pages and shared controls. |
| src/TSUGU.AdminHost | Narrowly scoped helper for operations that require administrator rights. |
| src/TSUGU.SearchHost | Separate Scout search worker. |
| tests/TSUGU.Tests | Unit tests and tests for privilege and recovery boundaries. |
| tests/TSUGU.UiSmoke | WinUI smoke-test application. |
| docs/ | Architecture, research and validation notes. |
| installer/ | Inno Setup definition and packaging scripts. |

See [docs/architecture.md](docs/architecture.md) for the application boundaries and behavior.

## Run tests

Run the test project after restore and build:

\`\`\`powershell
dotnet test tests/TSUGU.Tests/TSUGU.Tests.csproj --configuration Release --no-build
\`\`\`

The standard suite avoids changing Windows settings or installing packages. Tests that inspect the real PC or exercise privileged flows are opt-in:

| Variable | What it enables |
| --- | --- |
| TSUGU_LIVE_TESTS=1 | Read-only checks of local packages, services, registry entries, installed programs and caches. |
| TSUGU_LIVE_DOWNLOAD=1 with the variable above | Downloads and unpacks a vendor installer in a temporary folder. |
| TSUGU_RECOVERY_TESTS=1 with TSUGU_TEST_ARTIFACTS=<folder> | Copies a Scoop installation into the supplied folder and tests recovery scripts against the copy. |
| TSUGU_ELEVATED_TESTS=1 | Runs the privileged repair path against disposable test data. Use an administrator terminal only when the test explicitly requires it. |

Add or update tests in tests/TSUGU.Tests/<Area>/ for the behavior being changed.

## Engineering guidelines

1. Keep changes focused and explain the user-visible behavior in the pull request.
2. Preserve the normal-user desktop process and the separate, explicitly elevated AdminHost boundary.
3. Never pass arbitrary command text across the helper boundary; privileged actions must be rebuilt from reviewed catalogs and allowlists.
4. Save original state before reversible changes and test the restore path.
5. Keep Windows Update, Defender, firewall and power-saving defaults intact unless a user explicitly selects a supported change.
6. Update the relevant research or architecture note when a change affects a Windows operation or its safety assumptions.
7. Follow .editorconfig. Warnings and NuGet audit findings are build failures by design; update locked package files when dependencies change.

## Pull requests and CI

Create a branch from main, describe the reason for the change and include relevant test results. For UI work, check light and dark themes, keyboard navigation, high contrast and accessible names; screenshots are helpful.

GitHub Actions restores dependencies in locked mode, builds the solution and runs the affected test groups (a push to main runs the full CI suite). The release workflow is manually started from the Actions tab and packages a preview only after the build and tests pass.

## License and attribution

Keep the project's existing third-party copyright and license notices intact. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and the LICENSES/ directory before redistributing or adapting code.
