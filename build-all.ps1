<#
.SYNOPSIS
    Build everything: hooks-notifier.exe + Setup installer.
#>
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)

$Root = $PSScriptRoot

# 1. Build & publish hooks-notifier
Write-Host "=== Building hooks-notifier ===" -ForegroundColor Cyan
$result = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'publish',
    "$Root\src\HooksNotifier\HooksNotifier.csproj",
    "--configuration", $Configuration,
    "--output", "$Root\bin",
    "--self-contained", "false",
    "--configfile", "$Root\src\HooksNotifier\nuget.config"
) -NoNewWindow -Wait -PassThru

if ($result.ExitCode -ne 0) {
    Write-Error "hooks-notifier build failed (exit $($result.ExitCode))"
    exit $result.ExitCode
}

# 2. Build & publish hooks-notify (lightweight hook handler)
Write-Host "=== Building hooks-notify ===" -ForegroundColor Cyan
$resultNotify = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'publish',
    "$Root\src\NotifyHook\NotifyHook.csproj",
    "--configuration", $Configuration,
    "--output", "$Root\bin",
    "--self-contained", "false"
) -NoNewWindow -Wait -PassThru

if ($resultNotify.ExitCode -ne 0) {
    Write-Error "hooks-notify build failed (exit $($resultNotify.ExitCode))"
    exit $resultNotify.ExitCode
}

# 3. Build setup installer
Write-Host "=== Building Setup installer ===" -ForegroundColor Cyan
$result2 = Start-Process -FilePath 'dotnet' -ArgumentList @(
    'publish',
    "$Root\src\Setup\Setup.csproj",
    "--configuration", $Configuration,
    "--output", "$Root\setup",
    "--self-contained", "false"
) -NoNewWindow -Wait -PassThru

# Remove any extra files (single-file publish may leave .deps.json)
Get-ChildItem "$Root\setup" -Filter "*.dll" | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem "$Root\setup" -Filter "*.pdb" | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem "$Root\setup" -Filter "*.json" | Remove-Item -Force -ErrorAction SilentlyContinue

if ($result2.ExitCode -ne 0) {
    Write-Error "Setup build failed (exit $($result2.ExitCode))"
    exit $result2.ExitCode
}

Write-Host ""
Write-Host "=== Build complete ===" -ForegroundColor Green
Write-Host "  hooks-notifier.exe: $Root\bin\hooks-notifier.exe"
Write-Host "  Setup installer:    $Root\setup\ClaudeCodeHooksNotifier-Setup.exe"
Write-Host ""
Write-Host "Run the Setup to install: setup\ClaudeCodeHooksNotifier-Setup.exe" -ForegroundColor Yellow
