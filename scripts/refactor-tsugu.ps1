$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
function Read-Text([string]$Path) { [System.IO.File]::ReadAllText($Path) }
function Write-Text([string]$Path, [string]$Content) { [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false)) }

# Keep checked-in project paths and workflow files stable. Only product-facing names, namespaces and runtime links are changed here.
$extensions = @('.cs','.xaml','.props','.ps1','.iss','.md','.json','.manifest','.toml','.xml','.editorconfig','.config')
$replacements = @(
 @('https://api.github.com/repos/Cosmos-0118/TidyWindow','https://api.github.com/repos/raynottt/tsugu-windows-toolkit'),
 @('https://github.com/Cosmos-0118/TidyWindow','https://github.com/raynottt/tsugu-windows-toolkit'),
 @('TidyWindow.Next.Core','TSUGU.Core'), @('TidyWindow.Next.Desktop','TSUGU.Desktop'), @('TidyWindow.Next.Tests','TSUGU.Tests'),
 @('TidyWindow.CleanHost','TSUGU.AdminHost'), @('TidyWindow.SearchHost','TSUGU.SearchHost'),
 @('TidyWindow.Desktop','TSUGU.Desktop'), @('TidyWindow.Core','TSUGU.Core'), @('TidyWindow.Tests','TSUGU.Tests'), @('TidyWindow.UiSmoke','TSUGU.UiSmoke'),
 @('TidyWindow.Next','TSUGU'), @('TidyWindow','TSUGU'), @('tidywindow','tsugu')
)
$sourceFiles = Get-ChildItem -LiteralPath $repo -Recurse -File | Where-Object {
 $extensions -contains $_.Extension.ToLowerInvariant() -and
 $_.FullName -notmatch '[\\/](LICENSES|docs[\\/]reference|scripts)[\\/]' -and
 $_.FullName -notmatch '[\\/]\.github[\\/]workflows[\\/]' -and
 $_.FullName -notmatch '[\\/]installer[\\/]build\.ps1$' -and
 ($_.Extension.ToLowerInvariant() -ne '.ps1' -or $_.FullName -match '[\\/]src[\\/]') -and
 $_.Name -notin @('README.md','THIRD_PARTY_NOTICES.md')
}
foreach ($file in $sourceFiles) {
 $before = Read-Text $file.FullName
 $after = $before
 foreach ($pair in $replacements) { $after = $after.Replace($pair[0], $pair[1]) }
 # These helper filenames are still part of the current publish workflow; namespaces and all app-owned names are TSUGU.
 $after = $after.Replace('TSUGU.AdminHost.exe','TidyWindow.CleanHost.exe').Replace('TSUGU.SearchHost.exe','TidyWindow.SearchHost.exe')
 # Physical source folders stay in place so the existing CI and release workflows keep resolving their project paths.
 $after = $after.Replace('src/TSUGU.Core/','src/TidyWindow.Core/').Replace('src/TSUGU.Desktop/','src/TidyWindow.Desktop/').Replace('src/TSUGU.AdminHost/','src/TidyWindow.CleanHost/').Replace('src/TSUGU.SearchHost/','src/TidyWindow.SearchHost/').Replace('tests/TSUGU.Tests/','tests/TidyWindow.Tests/').Replace('tests/TSUGU.UiSmoke/','tests/TidyWindow.UiSmoke/')
 $after = $after.Replace('src\TSUGU.Desktop\','src\TidyWindow.Desktop\')
 if ($after -cne $before) { Write-Text $file.FullName $after }
}

# Remove the Debloat implementation, its navigation and its elevated command handler.
foreach ($relative in @('src/TidyWindow.Core/Debloat','src/TidyWindow.Desktop/Features/Debloat','tests/TidyWindow.Tests/Debloat')) {
 $path = Join-Path $repo $relative
 if (Test-Path $path) { Remove-Item $path -Recurse -Force }
}
$research = Join-Path $repo 'docs/debloat-research.md'
if (Test-Path $research) { Remove-Item $research -Force }

$xamlPath = Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml'
$xaml = Read-Text $xamlPath
$xaml = [regex]::Replace($xaml, '(?ms)^\s*<Button x:Name="DebloatNav".*?^\s*</Button>\r?\n', '')
$xaml = [regex]::Replace($xaml, '(?m)^\s*<local:DebloatView x:Name="DebloatPage".*?/\>\r?\n', '')
Write-Text $xamlPath $xaml

$mainPath = Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml.cs'
$main = Read-Text $mainPath
$main = $main.Replace(', Debloat, Fixes', ', Fixes')
$main = [regex]::Replace($main, '(?m)^\s*private readonly DebloatViewModel debloat;\r?\n', '')
$main = [regex]::Replace($main, '(?m)^\s*private readonly DebloatSession debloatSession;\r?\n', '')
$main = [regex]::Replace($main, '(?ms)        // Removing apps needs administrator rights.*?        var labStore =', '        var labStore =')
$main = [regex]::Replace($main, '(?m)^\s*DebloatPage\.DataContext = debloat;\r?\n', '')
$main = [regex]::Replace($main, '(?m)^\s*// Debloat removes apps behind the package managers'' backs,.*?\r?\n', '')
$main = [regex]::Replace($main, '(?m)^\s*debloat\.AppsChanged \+= .*?\r?\n', '')
$main = $main.Replace('debloat.Dispose(); debloatSession.Dispose(); ', '')
$main = [regex]::Replace($main, '(?m)^\s*DebloatNav\.Visibility = .*?\r?\n', '')
$main = $main.Replace('Page.Debloat => DebloatPage, ', '').Replace('(DebloatNav, Page.Debloat), ', '')
$main = [regex]::Replace($main, '(?ms)    private void DebloatClick\(object sender, RoutedEventArgs e\)\r?\n    \{.*?^\s*\}\r?\n', '')
$main = [regex]::Replace($main, '(?m)^\s*if \(debloat\.WorkOverlay && !debloat\.ScanOverlay\).*?\r?\n', '')
if ($main -match '\bDebloat') { throw 'Debloat references remain in MainWindow.' }
Write-Text $mainPath $main

$hostPath = Join-Path $repo 'src/TidyWindow.CleanHost/Program.cs'
$adminHostText = Read-Text $hostPath
$adminHostText = [regex]::Replace($adminHostText, '(?m)^\s*:\s*args.Length > 0 && args\[0\] == TSUGU\.Core\.DebloatHost\.Command\r?\n\s*\?\s*await TSUGU\.Core\.DebloatHost\.RunAsync\(args\)\r?\n', '')
if ($adminHostText -match '\bDebloat') { throw 'Debloat still registered in the elevated helper.' }
Write-Text $hostPath $adminHostText

# The core assembly and test assembly use TSUGU identities even though their repository paths stay stable.
$corePath = Join-Path $repo 'src/TidyWindow.Core/TidyWindow.Core.csproj'
$core = Read-Text $corePath
$core = $core.Replace('<EmbeddedResource Include="Debloat/debloat-catalog.json" LogicalName="debloat-catalog.json" />', '')
$core = $core.Replace('Include="TidyWindow.Tests"', 'Include="TSUGU.Tests"')
if ($core -notmatch '<AssemblyName>') {
 $core = $core.Replace('<Project Sdk="Microsoft.NET.Sdk">', '<Project Sdk="Microsoft.NET.Sdk">' + [Environment]::NewLine + '  <PropertyGroup><AssemblyName>TSUGU.Core</AssemblyName></PropertyGroup>')
}
Write-Text $corePath $core

$testProjectPath = Join-Path $repo 'tests/TidyWindow.Tests/TidyWindow.Tests.csproj'
$testProject = Read-Text $testProjectPath
$testProject = $testProject.Replace('<PropertyGroup><IsTestProject>', '<PropertyGroup><AssemblyName>TSUGU.Tests</AssemblyName><IsTestProject>')
Write-Text $testProjectPath $testProject

# ProcessRunner no longer depends on the deleted Debloat assembly helper.
$runnerPath = Join-Path $repo 'src/TidyWindow.Core/Infrastructure/ProcessRunner.cs'
$runner = Read-Text $runnerPath
$module = 'string.Join(Path.PathSeparator, new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules") })'
$runner = $runner.Replace('DebloatProcess.SystemModulePath', $module)
if ($runner -match '\bDebloat') { throw 'ProcessRunner retains a Debloat dependency.' }
Write-Text $runnerPath $runner

# Keep the test planner in step with the removed area and renamed solution.
$planPath = Join-Path $repo '.github/ci/test-areas.json'
$plan = Read-Text $planPath | ConvertFrom-Json -AsHashtable
$plan.groups.files = @('Cleanup','Scout','Paths')
$plan.areas.Remove('Debloat')
$plan.areas.Infrastructure.dependsOn = @('Packages')
$plan.everything = $plan.everything | ForEach-Object { $_.Replace('TidyWindow.slnx', 'TSUGU.slnx') }
Write-Text $planPath (($plan | ConvertTo-Json -Depth 40) + [Environment]::NewLine)

# Point the updater at this repository's releases and the portable asset naming used by its release pipeline.
$updatePath = Join-Path $repo 'src/TidyWindow.Core/Updates/AppUpdateService.cs'
$update = Read-Text $updatePath
$update = $update.Replace('if (Flag(item, "draft") || Flag(item, "prerelease")) continue;', 'if (Flag(item, "draft")) continue;')
$update = $update.Replace('if (!TryParseTag(tag, out var version) || version.Major < 5) continue;', 'if (!TryParseTag(tag, out var version)) continue;')
$update = $update.Replace('$"TSUGU-{version}-win-x64.zip"', '$"TSUGU-Portable-win-x64-{version}.zip"')
$update = $update.Replace('name.StartsWith("TSUGU-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase);', 'name.StartsWith("TSUGU-Portable-win-x64-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);')
if ($update -match 'Cosmos-0118|TidyWindow') { throw 'Updater still references the upstream project.' }
Write-Text $updatePath $update

$testPath = Join-Path $repo 'tests/TidyWindow.Tests/Updates/AppUpdateTests.cs'
$tests = Read-Text $testPath
$tests = $tests.Replace('TSUGU-5.1.0-win-x64.zip', 'TSUGU-Portable-win-x64-5.1.0.zip')
Write-Text $testPath $tests

# Remove the old author's donation link and card; keep TSUGU's repository, issues, releases and legal notices.
$settingsVmPath = Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsViewModel.cs'
$settingsVm = Read-Text $settingsVmPath
$settingsVm = [regex]::Replace($settingsVm, '(?m)^\s*public const string BuyMeACoffeeUrl = .*?;\r?\n', '')
$settingsVm = $settingsVm.Replace('https://github.com/raynottt/tsugu-windows-toolkit/blob/main/LICENSE', 'https://github.com/raynottt/tsugu-windows-toolkit/blob/main/THIRD_PARTY_NOTICES.md')
$settingsVm = $settingsVm.Replace('No TSUGU 5 release was found on GitHub.', 'No TSUGU release was found on GitHub.')
Write-Text $settingsVmPath $settingsVm

$settingsCsPath = Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml.cs'
$settingsCs = Read-Text $settingsCsPath
$settingsCs = [regex]::Replace($settingsCs, '(?m)^\s*private void CoffeeClick\(object sender, RoutedEventArgs e\).*?\r?\n', '')
Write-Text $settingsCsPath $settingsCs

$settingsXamlPath = Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml'
$settingsXaml = Read-Text $settingsXamlPath
$coffee = '(?s)\s*<Border Style="\{StaticResource Card\}" Padding="20">\s*<Grid ColumnSpacing="14">.*?<TextBlock Text="Buy me a coffee".*?</Grid>\s*</Border>'
$settingsXaml = [regex]::Replace($settingsXaml, $coffee, '')
$settingsXaml = $settingsXaml.Replace('Text="License"', 'Text="Third-party notices"').Replace('Text="Released under the MIT License."', 'Text="Licensing and attribution for the upstream components used by TSUGU."').Replace('Content="Read license"', 'Content="Read notices"')
Write-Text $settingsXamlPath $settingsXaml

# Windows cannot start .cmd/.bat files as executable images: route test and real batch helpers through cmd.exe.
$scoutPath = Join-Path $repo 'src/TidyWindow.Core/Scout/ScoutWorker.cs'
$scout = Read-Text $scoutPath
$scout = $scout.Replace('Reinstall TidyWindow', 'Reinstall TSUGU').Replace('Rebuild TidyWindow', 'Rebuild TSUGU')
$old = @'
            var start = new ProcessStartInfo(hostPath)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? ""
            };
            start.ArgumentList.Add("--serve");
'@
$new = @'
            var batchFile = Path.GetExtension(hostPath).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(hostPath).Equals(".bat", StringComparison.OrdinalIgnoreCase);
            var executable = batchFile ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") : hostPath;
            var start = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? ""
            };
            if (batchFile) start.Arguments = "/d /s /c " + (char)34 + (char)34 + hostPath + (char)34 + " --serve" + (char)34;
            else start.ArgumentList.Add("--serve");
'@
if ($scout.Contains($old)) {
 $scout = $scout.Replace($old, $new)
} elseif (-not $scout.Contains('var batchFile = Path.GetExtension(hostPath)')) {
 throw 'Scout process start block not found.'
}
Write-Text $scoutPath $scout

$pathCommandsPath = Join-Path $repo 'src/TidyWindow.Core/Paths/PathCommands.cs'
$pc = Read-Text $pathCommandsPath
$old = @'
        var start = new ProcessStartInfo(file)

        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        start.ArgumentList.Add(command is "java" or "javac" ? "-version" : "--version");
'@
$new = @'
        var versionArgument = command is "java" or "javac" ? "-version" : "--version";
        var batchFile = Path.GetExtension(file).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(file).Equals(".bat", StringComparison.OrdinalIgnoreCase);
        var executable = batchFile ? (Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") : file;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(file) ?? Environment.CurrentDirectory
        };
        if (batchFile) start.Arguments = "/d /s /c " + (char)34 + (char)34 + file + (char)34 + " " + versionArgument + (char)34;
        else start.ArgumentList.Add(versionArgument);
'@
if ($pc.Contains($old)) {
 $pc = $pc.Replace($old, $new)
} elseif (-not $pc.Contains('var batchFile = Path.GetExtension(file)')) {
 throw 'Path Doctor process probe block not found.'
}
Write-Text $pathCommandsPath $pc

# Keep installer verification aligned with the WinUI publish output: MainWindow.xbf is not a guaranteed loose file.
$installerBuildPath = Join-Path $repo 'installer/build.ps1'
$installerBuild = Read-Text $installerBuildPath
$installerBuild = $installerBuild.Replace("'TSUGU.exe', 'Shell\MainWindow.xbf', 'CleanHost\TidyWindow.CleanHost.exe'", "'TSUGU.exe', 'CleanHost\TidyWindow.CleanHost.exe'")
Write-Text $installerBuildPath $installerBuild

# Remove only the obsolete upstream product icon; keep source-license attribution intact.
$oldIcon = Join-Path $repo 'src/TidyWindow.Desktop/Assets/Brand/tidywindow.ico'
if (Test-Path $oldIcon) { Remove-Item $oldIcon -Force }

$remaining = Get-ChildItem (Join-Path $repo 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj,*.json | Select-String '\bDebloat'
if ($remaining) {
 Write-Host 'Remaining Debloat text references (review before release):'
 $remaining | ForEach-Object { Write-Host ($_.Path + ':' + $_.LineNumber + ': ' + $_.Line.Trim()) }
}
Write-Host 'TSUGU refactor applied.'
