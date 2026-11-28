$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
function Read-Text([string]$Path) { [System.IO.File]::ReadAllText($Path) }
function Write-Text([string]$Path, [string]$Content) { [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false)) }
$extensions = @('.cs','.xaml','.props','.ps1','.iss','.md','.json','.manifest','.toml','.xml','.editorconfig','.config')
$replacements = @(
 @('https://api.github.com/repos/Cosmos-0118/TidyWindow','https://api.github.com/repos/raynottt/tsugu-windows-toolkit'),
 @('https://github.com/Cosmos-0118/TidyWindow','https://github.com/raynottt/tsugu-windows-toolkit'),
 @('TidyWindow.Next.Core','TSUGU.Core'), @('TidyWindow.Next.Desktop','TSUGU.Desktop'), @('TidyWindow.Next.Tests','TSUGU.Tests'),
 @('TidyWindow.CleanHost','TSUGU.AdminHost'), @('TidyWindow.SearchHost','TSUGU.SearchHost'),
 @('TidyWindow.Desktop','TSUGU.Desktop'), @('TidyWindow.Core','TSUGU.Core'), @('TidyWindow.Tests','TSUGU.Tests'), @('TidyWindow.UiSmoke','TSUGU.UiSmoke'),
 @('TidyWindow.Next','TSUGU'), @('TidyWindow','TSUGU'), @('tidywindow','tsugu'), @('CleanHost','AdminHost'), @('clean-host','admin-host')
)
$sourceFiles = Get-ChildItem -LiteralPath $repo -Recurse -File | Where-Object {
 $extensions -contains $_.Extension.ToLowerInvariant() -and $_.FullName -notmatch '[\\/](LICENSES|docs[\\/]reference|scripts)[\\/]' -and $_.FullName -notmatch '[\\/]\.github[\\/]workflows[\\/]' -and $_.FullName -notmatch '[\\/]installer[\\/]build\.ps1
foreach ($file in $sourceFiles) {
 $before=Read-Text $file.FullName; $after=$before
 foreach($pair in $replacements){$after=$after.Replace($pair[0],$pair[1])}
 $after=$after.Replace('TSUGU.AdminHost.exe','TidyWindow.CleanHost.exe').Replace('TSUGU.SearchHost.exe','TidyWindow.SearchHost.exe')
 $after=$after.Replace('src/TSUGU.Core/','src/TidyWindow.Core/').Replace('src/TSUGU.Desktop/','src/TidyWindow.Desktop/').Replace('src/TSUGU.AdminHost/','src/TidyWindow.CleanHost/').Replace('src/TSUGU.SearchHost/','src/TidyWindow.SearchHost/').Replace('tests/TSUGU.Tests/','tests/TidyWindow.Tests/').Replace('tests/TSUGU.UiSmoke/','tests/TidyWindow.UiSmoke/')
 if($after -cne $before){Write-Text $file.FullName $after}
}
# Remove Debloat implementation and all UI wiring.
foreach($relative in @('src/TidyWindow.Core/Debloat','src/TidyWindow.Desktop/Features/Debloat','tests/TidyWindow.Tests/Debloat')){$path=Join-Path $repo $relative;if(Test-Path $path){Remove-Item $path -Recurse -Force}}
$research=Join-Path $repo 'docs/debloat-research.md';if(Test-Path $research){Remove-Item $research -Force}
$xamlPath=Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml';$xaml=Read-Text $xamlPath
$xaml=[regex]::Replace($xaml,'(?ms)^\s*<Button x:Name="DebloatNav".*?^\s*</Button>\r?\n','')
$xaml=[regex]::Replace($xaml,'(?m)^\s*<local:DebloatView x:Name="DebloatPage".*?/\>\r?\n','');Write-Text $xamlPath $xaml
$mainPath=Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml.cs';$main=Read-Text $mainPath
$main=$main.Replace(', Debloat, Fixes',', Fixes')
$main=[regex]::Replace($main,'(?m)^\s*private readonly DebloatViewModel debloat;\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*private readonly DebloatSession debloatSession;\r?\n','')
$main=[regex]::Replace($main,'(?ms)        // Removing apps needs administrator rights.*?        var labStore =','        var labStore =')
$main=[regex]::Replace($main,'(?m)^\s*DebloatPage\.DataContext = debloat;\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*// Debloat removes apps behind the package managers'' backs,.*?\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*debloat\.AppsChanged \+= .*?\r?\n','')
$main=$main.Replace('debloat.Dispose(); debloatSession.Dispose(); ','')
$main=[regex]::Replace($main,'(?m)^\s*DebloatNav\.Visibility = .*?\r?\n','')
$main=$main.Replace('Page.Debloat => DebloatPage, ','').Replace('(DebloatNav, Page.Debloat), ','')
$main=[regex]::Replace($main,'(?ms)    private void DebloatClick\(object sender, RoutedEventArgs e\)\r?\n    \{.*?^\s*\}\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*if \(debloat\.WorkOverlay && !debloat\.ScanOverlay\).*?\r?\n','')
if($main -match '\bDebloat'){throw 'Debloat references remain in MainWindow.'};Write-Text $mainPath $main
$hostPath=Join-Path $repo 'src/TidyWindow.CleanHost/Program.cs';$adminHostText=Read-Text $hostPath
$adminHostText=[regex]::Replace($adminHostText,'(?m)^\s*:\s*args\.Length > 0 && args\[0\] == TSUGU\.Core\.DebloatHost\.Command\r?\n\s*\?\s*await TSUGU\.Core\.DebloatHost\.RunAsync\(args\)\r?\n','')
if($adminHostText -match '\bDebloat'){throw 'Debloat still registered in AdminHost.'};Write-Text $hostPath $adminHostText
$corePath=Join-Path $repo 'src/TidyWindow.Core/TSUGU.Core.csproj';$core=Read-Text $corePath
$core=$core.Replace('<EmbeddedResource Include="Debloat/debloat-catalog.json" LogicalName="debloat-catalog.json" />','')
$core=$core.Replace('Include="TidyWindow.Tests"','Include="TSUGU.Tests"')
if($core -notmatch '<AssemblyName>'){$core=$core.Replace('<Project Sdk="Microsoft.NET.Sdk">','<Project Sdk="Microsoft.NET.Sdk">'+[Environment]::NewLine+'  <PropertyGroup><AssemblyName>TSUGU.Core</AssemblyName></PropertyGroup>')};Write-Text $corePath $core
$testProjectPath=Join-Path $repo 'tests/TidyWindow.Tests/TidyWindow.Tests.csproj';$testProject=Read-Text $testProjectPath
$testProject=$testProject.Replace('<PropertyGroup><IsTestProject>','<PropertyGroup><AssemblyName>TSUGU.Tests</AssemblyName><IsTestProject>');Write-Text $testProjectPath $testProject
$runnerPath=Join-Path $repo 'src/TidyWindow.Core/Infrastructure/ProcessRunner.cs';$runner=Read-Text $runnerPath
$module='string.Join(Path.PathSeparator, new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules") })'
$runner=$runner.Replace('DebloatProcess.SystemModulePath',$module);if($runner -match '\bDebloat'){throw 'ProcessRunner retains Debloat dependency.'};Write-Text $runnerPath $runner
$planPath=Join-Path $repo '.github/ci/test-areas.json';$plan=Read-Text $planPath|ConvertFrom-Json -AsHashtable
$plan.groups.files=@('Cleanup','Scout','Paths');$plan.areas.Remove('Debloat');$plan.areas.Infrastructure.dependsOn=@('Packages')
$plan.everything=$plan.everything | ForEach-Object { $_.Replace('TidyWindow.slnx','TSUGU.slnx') }
Write-Text $planPath (($plan|ConvertTo-Json -Depth 40)+[Environment]::NewLine)

# Point updater to TSUGU releases and their actual asset names.
$updatePath=Join-Path $repo 'src/TidyWindow.Core/Updates/AppUpdateService.cs';$update=Read-Text $updatePath
$update=$update.Replace('if (Flag(item, "draft") || Flag(item, "prerelease")) continue;','if (Flag(item, "draft")) continue;')
$update=$update.Replace('if (!TryParseTag(tag, out var version) || version.Major < 5) continue;','if (!TryParseTag(tag, out var version)) continue;')
$update=$update.Replace('$"TSUGU-{version}-win-x64.zip"','$"TSUGU-Portable-win-x64-{version}.zip"')
$update=$update.Replace('name.StartsWith("TSUGU-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase);','name.StartsWith("TSUGU-Portable-win-x64-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);')
if($update -match 'Cosmos-0118|TidyWindow'){throw 'Updater still references upstream.'};Write-Text $updatePath $update
$testPath=Join-Path $repo 'tests/TidyWindow.Tests/Updates/AppUpdateTests.cs';$tests=Read-Text $testPath
$tests=$tests.Replace('TSUGU-5.1.0-win-x64.zip','TSUGU-Portable-win-x64-5.1.0.zip');Write-Text $testPath $tests

# Remove the upstream donation panel and its handler; keep TSUGU repository, issues and release links.
$settingsVmPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsViewModel.cs';$settingsVm=Read-Text $settingsVmPath
$settingsVm=[regex]::Replace($settingsVm,'(?m)^\s*public const string BuyMeACoffeeUrl = .*?;\r?\n','')
$settingsVm=$settingsVm.Replace('https://github.com/raynottt/tsugu-windows-toolkit/blob/main/LICENSE','https://github.com/raynottt/tsugu-windows-toolkit/blob/main/THIRD_PARTY_NOTICES.md')
$settingsVm=$settingsVm.Replace('No TSUGU 5 release was found on GitHub.','No TSUGU release was found on GitHub.');Write-Text $settingsVmPath $settingsVm
$settingsCsPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml.cs';$settingsCs=Read-Text $settingsCsPath
$settingsCs=[regex]::Replace($settingsCs,'(?m)^\s*private void CoffeeClick\(object sender, RoutedEventArgs e\).*?\r?\n','');Write-Text $settingsCsPath $settingsCs
$settingsXamlPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml';$settingsXaml=Read-Text $settingsXamlPath
$coffee='(?s)\s*<Border Style="\{StaticResource Card\}" Padding="20">\s*<Grid ColumnSpacing="14">.*?<TextBlock Text="Buy me a coffee".*?</Grid>\s*</Border>'
$settingsXaml=[regex]::Replace($settingsXaml,$coffee,'')
$settingsXaml=$settingsXaml.Replace('Text="License"','Text="Third-party notices"').Replace('Text="Released under the MIT License."','Text="Licensing and attribution for the upstream components used by TSUGU."').Replace('Content="Read license"','Content="Read notices"')
Write-Text $settingsXamlPath $settingsXaml

# Batch scripts must be launched through cmd.exe on Windows, not directly as executable images.
$scoutPath=Join-Path $repo 'src/TidyWindow.Core/Scout/ScoutWorker.cs';$scout=Read-Text $scoutPath
$scout=$scout.Replace('Reinstall TidyWindow','Reinstall TSUGU').Replace('Rebuild TidyWindow','Rebuild TSUGU')
$old=@'
            var start = new ProcessStartInfo(hostPath)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? ""
            };
            start.ArgumentList.Add("--serve");
'@
$new=@'
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
if(-not $scout.Contains($old)){throw 'Scout process start block not found.'};$scout=$scout.Replace($old,$new);Write-Text $scoutPath $scout
$pathCommandsPath=Join-Path $repo 'src/TidyWindow.Core/Paths/PathCommands.cs';$pc=Read-Text $pathCommandsPath
$old=@'
        var start = new ProcessStartInfo(file)

        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        start.ArgumentList.Add(command is "java" or "javac" ? "-version" : "--version");
'@
$new=@'
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
if(-not $pc.Contains($old)){throw 'Path Doctor process probe block not found.'};$pc=$pc.Replace($old,$new);Write-Text $pathCommandsPath $pc

# Remove the obsolete icon but retain legal attribution files and the original source notice.
$oldIcon=Join-Path $repo 'src/TidyWindow.Desktop/Assets/Brand/tidywindow.ico';if(Test-Path $oldIcon){Remove-Item $oldIcon -Force}
$remaining=Get-ChildItem (Join-Path $repo 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj,*.json|Select-String '\bDebloat'
if($remaining){ Write-Host 'Remaining Debloat text references (review before release):'; $remaining | ForEach-Object { Write-Host ($_.Path + ':' + $_.LineNumber + ': ' + $_.Line.Trim()) } }
Write-Host 'TSUGU refactor applied.'
 -and $_.Name -notin @('README.md','THIRD_PARTY_NOTICES.md')
}
foreach ($file in $sourceFiles) { $before=Read-Text $file.FullName; $after=$before; foreach($pair in $replacements){$after=$after.Replace($pair[0],$pair[1])}; if($after -cne $before){Write-Text $file.FullName $after} }
$moves=@(
 @('src/TidyWindow.Core','src/TidyWindow.Core'),@('src/TidyWindow.Desktop','src/TidyWindow.Desktop'),
 @('src/TidyWindow.CleanHost','src/TidyWindow.CleanHost'),@('src/TidyWindow.SearchHost','src/TidyWindow.SearchHost'),
 @('tests/TidyWindow.Tests','tests/TidyWindow.Tests'),@('tests/TidyWindow.UiSmoke','tests/TSUGU.UiSmoke')
)
foreach($move in $moves){$from=Join-Path $repo $move[0];$to=Join-Path $repo $move[1];if(Test-Path $from){if(Test-Path $to){throw "Target exists: $($move[1])"};Move-Item $from $to}}
$renames=@(
 @('src/TidyWindow.Core/TidyWindow.Core.csproj','TSUGU.Core.csproj'),@('src/TidyWindow.Desktop/TidyWindow.Desktop.csproj','TSUGU.Desktop.csproj'),
 @('src/TidyWindow.CleanHost/TidyWindow.CleanHost.csproj','TSUGU.AdminHost.csproj'),@('src/TidyWindow.SearchHost/TidyWindow.SearchHost.csproj','TSUGU.SearchHost.csproj'),
 @('tests/TidyWindow.Tests/TidyWindow.Tests.csproj','TSUGU.Tests.csproj'),@('tests/TSUGU.UiSmoke/TidyWindow.UiSmoke.csproj','TSUGU.UiSmoke.csproj')
)
foreach($item in $renames){$path=Join-Path $repo $item[0];if(Test-Path $path){Rename-Item $path $item[1]}}
$iss=Join-Path $repo 'installer/TidyWindow.iss';if(Test-Path $iss){Rename-Item $iss 'TSUGU.iss'}

# Remove Debloat implementation and all UI wiring.
foreach($relative in @('src/TidyWindow.Core/Debloat','src/TidyWindow.Desktop/Features/Debloat','tests/TidyWindow.Tests/Debloat')){$path=Join-Path $repo $relative;if(Test-Path $path){Remove-Item $path -Recurse -Force}}
$research=Join-Path $repo 'docs/debloat-research.md';if(Test-Path $research){Remove-Item $research -Force}
$xamlPath=Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml';$xaml=Read-Text $xamlPath
$xaml=[regex]::Replace($xaml,'(?ms)^\s*<Button x:Name="DebloatNav".*?^\s*</Button>\r?\n','')
$xaml=[regex]::Replace($xaml,'(?m)^\s*<local:DebloatView x:Name="DebloatPage".*?/\>\r?\n','');Write-Text $xamlPath $xaml
$mainPath=Join-Path $repo 'src/TidyWindow.Desktop/Shell/MainWindow.xaml.cs';$main=Read-Text $mainPath
$main=$main.Replace(', Debloat, Fixes',', Fixes')
$main=[regex]::Replace($main,'(?m)^\s*private readonly DebloatViewModel debloat;\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*private readonly DebloatSession debloatSession;\r?\n','')
$main=[regex]::Replace($main,'(?ms)        // Removing apps needs administrator rights.*?        var labStore =','        var labStore =')
$main=[regex]::Replace($main,'(?m)^\s*DebloatPage\.DataContext = debloat;\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*// Debloat removes apps behind the package managers'' backs,.*?\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*debloat\.AppsChanged \+= .*?\r?\n','')
$main=$main.Replace('debloat.Dispose(); debloatSession.Dispose(); ','')
$main=[regex]::Replace($main,'(?m)^\s*DebloatNav\.Visibility = .*?\r?\n','')
$main=$main.Replace('Page.Debloat => DebloatPage, ','').Replace('(DebloatNav, Page.Debloat), ','')
$main=[regex]::Replace($main,'(?ms)    private void DebloatClick\(object sender, RoutedEventArgs e\)\r?\n    \{.*?^\s*\}\r?\n','')
$main=[regex]::Replace($main,'(?m)^\s*if \(debloat\.WorkOverlay && !debloat\.ScanOverlay\).*?\r?\n','')
if($main -match '\bDebloat'){throw 'Debloat references remain in MainWindow.'};Write-Text $mainPath $main
$hostPath=Join-Path $repo 'src/TidyWindow.CleanHost/Program.cs';$adminHostText=Read-Text $hostPath
$adminHostText=[regex]::Replace($adminHostText,'(?m)^\s*:\s*args\.Length > 0 && args\[0\] == TSUGU\.Core\.DebloatHost\.Command\r?\n\s*\?\s*await TSUGU\.Core\.DebloatHost\.RunAsync\(args\)\r?\n','')
if($adminHostText -match '\bDebloat'){throw 'Debloat still registered in AdminHost.'};Write-Text $hostPath $adminHostText
$corePath=Join-Path $repo 'src/TidyWindow.Core/TSUGU.Core.csproj';$core=Read-Text $corePath
$core=$core.Replace('<EmbeddedResource Include="Debloat/debloat-catalog.json" LogicalName="debloat-catalog.json" />','')
if($core -notmatch '<AssemblyName>'){$core=$core.Replace('<Project Sdk="Microsoft.NET.Sdk">','<Project Sdk="Microsoft.NET.Sdk">'+[Environment]::NewLine+'  <PropertyGroup><AssemblyName>TSUGU.Core</AssemblyName></PropertyGroup>')};Write-Text $corePath $core
$runnerPath=Join-Path $repo 'src/TidyWindow.Core/Infrastructure/ProcessRunner.cs';$runner=Read-Text $runnerPath
$module='string.Join(Path.PathSeparator, new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsPowerShell", "Modules"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "Modules") })'
$runner=$runner.Replace('DebloatProcess.SystemModulePath',$module);if($runner -match '\bDebloat'){throw 'ProcessRunner retains Debloat dependency.'};Write-Text $runnerPath $runner
$planPath=Join-Path $repo '.github/ci/test-areas.json';$plan=Read-Text $planPath|ConvertFrom-Json -AsHashtable
$plan.groups.files=@('Cleanup','Scout','Paths');$plan.areas.Remove('Debloat');$plan.areas.Infrastructure.dependsOn=@('Packages')
Write-Text $planPath (($plan|ConvertTo-Json -Depth 40)+[Environment]::NewLine)

# Point updater to TSUGU releases and their actual asset names.
$updatePath=Join-Path $repo 'src/TidyWindow.Core/Updates/AppUpdateService.cs';$update=Read-Text $updatePath
$update=$update.Replace('if (!TryParseTag(tag, out var version) || version.Major < 5) continue;','if (!TryParseTag(tag, out var version)) continue;')
$update=$update.Replace('$"TSUGU-{version}-win-x64.zip"','$"TSUGU-Portable-win-x64-{version}.zip"')
$update=$update.Replace('name.StartsWith("TSUGU-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase);','name.StartsWith("TSUGU-Portable-win-x64-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);')
if($update -match 'Cosmos-0118|TidyWindow'){throw 'Updater still references upstream.'};Write-Text $updatePath $update
$testPath=Join-Path $repo 'tests/TidyWindow.Tests/Updates/AppUpdateTests.cs';$tests=Read-Text $testPath
$tests=$tests.Replace('TSUGU-5.1.0-win-x64.zip','TSUGU-Portable-win-x64-5.1.0.zip');Write-Text $testPath $tests

# Remove the upstream donation panel and its handler; keep TSUGU repository, issues and release links.
$settingsVmPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsViewModel.cs';$settingsVm=Read-Text $settingsVmPath
$settingsVm=[regex]::Replace($settingsVm,'(?m)^\s*public const string BuyMeACoffeeUrl = .*?;\r?\n','')
$settingsVm=$settingsVm.Replace('https://github.com/raynottt/tsugu-windows-toolkit/blob/main/LICENSE','https://github.com/raynottt/tsugu-windows-toolkit/blob/main/THIRD_PARTY_NOTICES.md');Write-Text $settingsVmPath $settingsVm
$settingsCsPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml.cs';$settingsCs=Read-Text $settingsCsPath
$settingsCs=[regex]::Replace($settingsCs,'(?m)^\s*private void CoffeeClick\(object sender, RoutedEventArgs e\).*?\r?\n','');Write-Text $settingsCsPath $settingsCs
$settingsXamlPath=Join-Path $repo 'src/TidyWindow.Desktop/Features/Settings/SettingsView.xaml';$settingsXaml=Read-Text $settingsXamlPath
$coffee='(?s)\s*<Border Style="\{StaticResource Card\}" Padding="20">\s*<Grid ColumnSpacing="14">.*?<TextBlock Text="Buy me a coffee".*?</Grid>\s*</Border>'
$settingsXaml=[regex]::Replace($settingsXaml,$coffee,'')
$settingsXaml=$settingsXaml.Replace('Text="License"','Text="Third-party notices"').Replace('Text="Released under the MIT License."','Text="Licensing and attribution for the upstream components used by TSUGU."').Replace('Content="Read license"','Content="Read notices"')
Write-Text $settingsXamlPath $settingsXaml

# Fix release verification: WinUI publish does not guarantee a Shell/MainWindow.xbf file.
$releasePath=Join-Path $repo '.github/workflows/release.yml';$release=Read-Text $releasePath
$release=[regex]::Replace($release,'(?m)^\s*''Shell/MainWindow\.xbf'',\r?\n','')
$release=$release.Replace('the carried-forward TSUGU package, cleanup and repair foundation.','the TSUGU desktop app, package management, cleanup, diagnostics and repair tools.')
Write-Text $releasePath $release
$buildPath=Join-Path $repo 'installer/build.ps1';$build=Read-Text $buildPath
$build=$build.Replace('''Shell\MainWindow.xbf'', ','').Replace('CleanHost\','AdminHost\').Replace('TSUGU.CleanHost.exe','TSUGU.AdminHost.exe').Replace('TidyWindow.iss','TSUGU.iss')
Write-Text $buildPath $build

# Batch scripts must be launched through cmd.exe on Windows, not directly as executable images.
$scoutPath=Join-Path $repo 'src/TidyWindow.Core/Scout/ScoutWorker.cs';$scout=Read-Text $scoutPath
$scout=$scout.Replace('Reinstall TidyWindow','Reinstall TSUGU').Replace('Rebuild TidyWindow','Rebuild TSUGU')
$old=@'
            var start = new ProcessStartInfo(hostPath)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(hostPath) ?? ""
            };
            start.ArgumentList.Add("--serve");
'@
$new=@'
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
if(-not $scout.Contains($old)){throw 'Scout process start block not found.'};$scout=$scout.Replace($old,$new);Write-Text $scoutPath $scout
$pathCommandsPath=Join-Path $repo 'src/TidyWindow.Core/Paths/PathCommands.cs';$pc=Read-Text $pathCommandsPath
$old=@'
        var start = new ProcessStartInfo(file)

        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true
        };
        start.ArgumentList.Add(command is "java" or "javac" ? "-version" : "--version");
'@
$new=@'
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
if(-not $pc.Contains($old)){throw 'Path Doctor process probe block not found.'};$pc=$pc.Replace($old,$new);Write-Text $pathCommandsPath $pc

# Remove the obsolete icon but retain legal attribution files and the original source notice.
$oldIcon=Join-Path $repo 'src/TidyWindow.Desktop/Assets/Brand/tidywindow.ico';if(Test-Path $oldIcon){Remove-Item $oldIcon -Force}
$remaining=Get-ChildItem (Join-Path $repo 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj,*.json|Select-String '\bDebloat'
if($remaining){ Write-Host 'Remaining Debloat text references (review before release):'; $remaining | ForEach-Object { Write-Host ($_.Path + ':' + $_.LineNumber + ': ' + $_.Line.Trim()) } }
Write-Host 'TSUGU refactor applied.'
