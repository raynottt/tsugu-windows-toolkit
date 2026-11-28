$ErrorActionPreference = 'Stop'
$repo = (Get-Location).Path
function Read-Text([string]$Path) { [System.IO.File]::ReadAllText($Path) }
function Write-Text([string]$Path, [string]$Content) { [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false)) }
$extensions = @('.cs','.xaml','.csproj','.slnx','.props','.ps1','.iss','.yml','.yaml','.md','.json','.manifest','.toml','.xml','.editorconfig','.config')
$replacements = @(
 @('https://api.github.com/repos/Cosmos-0118/TidyWindow','https://api.github.com/repos/raynottt/tsugu-windows-toolkit'),
 @('https://github.com/Cosmos-0118/TidyWindow','https://github.com/raynottt/tsugu-windows-toolkit'),
 @('TidyWindow.Next.Core','TSUGU.Core'), @('TidyWindow.Next.Desktop','TSUGU.Desktop'), @('TidyWindow.Next.Tests','TSUGU.Tests'),
 @('TidyWindow.CleanHost','TSUGU.AdminHost'), @('TidyWindow.SearchHost','TSUGU.SearchHost'),
 @('TidyWindow.Desktop','TSUGU.Desktop'), @('TidyWindow.Core','TSUGU.Core'), @('TidyWindow.Tests','TSUGU.Tests'), @('TidyWindow.UiSmoke','TSUGU.UiSmoke'),
 @('TidyWindow.Next','TSUGU'), @('TidyWindow','TSUGU'), @('tidywindow','tsugu')
)
$sourceFiles = Get-ChildItem -LiteralPath $repo -Recurse -File | Where-Object {
 $extensions -contains $_.Extension.ToLowerInvariant() -and $_.FullName -notmatch '[\\/](LICENSES|docs[\\/]reference)[\\/]' -and $_.Name -notin @('README.md','THIRD_PARTY_NOTICES.md')
}
foreach ($file in $sourceFiles) {
 $before = Read-Text $file.FullName; $after = $before
 foreach ($pair in $replacements) { $after = $after.Replace($pair[0],$pair[1]) }
 if ($after -cne $before) { Write-Text $file.FullName $after }
}
$directoryMoves = @(
 @('src/TidyWindow.Core','src/TSUGU.Core'), @('src/TidyWindow.Desktop','src/TSUGU.Desktop'),
 @('src/TidyWindow.CleanHost','src/TSUGU.AdminHost'), @('src/TidyWindow.SearchHost','src/TSUGU.SearchHost'),
 @('tests/TidyWindow.Tests','tests/TSUGU.Tests'), @('tests/TidyWindow.UiSmoke','tests/TSUGU.UiSmoke')
)
foreach ($move in $directoryMoves) {
 $from = Join-Path $repo $move[0]; $to = Join-Path $repo $move[1]
 if (Test-Path -LiteralPath $from) { if (Test-Path -LiteralPath $to) { throw "Target exists: $($move[1])" }; Move-Item -LiteralPath $from -Destination $to }
}
$projectRenames = @(
 @('src/TSUGU.Core/TidyWindow.Core.csproj','TSUGU.Core.csproj'),
 @('src/TSUGU.Desktop/TidyWindow.Desktop.csproj','TSUGU.Desktop.csproj'),
 @('src/TSUGU.AdminHost/TidyWindow.CleanHost.csproj','TSUGU.AdminHost.csproj'),
 @('src/TSUGU.SearchHost/TidyWindow.SearchHost.csproj','TSUGU.SearchHost.csproj'),
 @('tests/TSUGU.Tests/TidyWindow.Tests.csproj','TSUGU.Tests.csproj'),
 @('tests/TSUGU.UiSmoke/TidyWindow.UiSmoke.csproj','TSUGU.UiSmoke.csproj')
)
foreach ($item in $projectRenames) { $path = Join-Path $repo $item[0]; if (Test-Path -LiteralPath $path) { Rename-Item -LiteralPath $path -NewName $item[1] } }
$iss = Join-Path $repo 'installer/TidyWindow.iss'
if (Test-Path -LiteralPath $iss) { Rename-Item -LiteralPath $iss -NewName 'TSUGU.iss' }

