<#
.SYNOPSIS
    Build and publish the C# Hooks Notifier executable.
.DESCRIPTION
    Compiles the C# project and optionally runs '--register' to set up
    the AppUserModelId (AUMID) for toast notifications.
.PARAMETER Configuration
    Build configuration: Debug or Release. Default: Release.
.PARAMETER OutputDir
    Output directory for the published executable. Default: ./bin/
.PARAMETER SelfContained
    Publish as self-contained (includes .NET runtime). Default: $false.
.PARAMETER Register
    Also run --register to create Start Menu shortcut for AUMID.
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir,
    [switch]$SelfContained,
    [switch]$Register
)

$ProjectRoot = $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot 'src' 'HooksNotifier' 'HooksNotifier.csproj'

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot 'bin'
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Building HooksNotifier ($Configuration)..." -ForegroundColor Cyan

$publishArgs = @(
    'publish', "`"$ProjectPath`"",
    '--configuration', $Configuration,
    '--output', "`"$OutputDir`""
)

if ($SelfContained) {
    $publishArgs += '--self-contained', 'true'
    $publishArgs += '-p:RuntimeIdentifier=win-x64'
}
else {
    $publishArgs += '--self-contained', 'false'
}

$nugetConfig = Join-Path (Split-Path $ProjectPath) 'nuget.config'
if (Test-Path $nugetConfig) {
    $publishArgs += '--configfile', "`"$nugetConfig`""
}

$result = Start-Process -FilePath 'dotnet' -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
if ($result.ExitCode -ne 0) {
    Write-Error "Build failed (exit code: $($result.ExitCode))"
    exit $result.ExitCode
}

$exePath = Join-Path $OutputDir 'hooks-notifier.exe'
if (Test-Path $exePath) {
    Write-Host "  Published: $exePath" -ForegroundColor Green
} else {
    # Check for DLL (framework-dependent)
    $dllPath = Join-Path $OutputDir 'hooks-notifier.dll'
    if (Test-Path $dllPath) {
        Write-Host "  Published: $dllPath (framework-dependent)" -ForegroundColor Yellow
    }
}

# Register AUMID
if ($Register -and (Test-Path $exePath)) {
    Write-Host "Registering AUMID..." -ForegroundColor Cyan
    & $exePath --register
}

Write-Host ''
Write-Host "Done. Output directory: $OutputDir" -ForegroundColor Green
Write-Host ''
Write-Host "To configure hooks to use this executable:"
Write-Host "  .\setup.ps1 -GlobalScope -UseExe"
Write-Host ''
