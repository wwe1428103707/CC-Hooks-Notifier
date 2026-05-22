<#
.SYNOPSIS
    Claude Code Hooks Notifier - Setup & Auto-Configuration.
.DESCRIPTION
    Resets existing hooks settings and installs the Windows toast notification
    hook plugin for Claude Code.
    1. Backs up current settings.json
    2. Removes old hook entries
    3. Registers notify.ps1 (or hooks-notifier.exe) for events
    4. Cleans up stale permission entries in settings.local.json
.PARAMETER GlobalScope
    Apply to global settings (~/.claude/settings.json). Default: $true.
.PARAMETER ProjectScope
    Apply to project settings (./.claude/settings.json). Default: $false.
.PARAMETER UseExe
    Use the compiled C# executable (hooks-notifier.exe) instead of PowerShell script.
.PARAMETER SkipBackup
    Skip creating a backup of existing settings.
.PARAMETER OnlyReset
    Only remove existing hooks, don't install new ones.
.PARAMETER DryRun
    Show what would be done without actually modifying files.
#>
param(
    [switch]$GlobalScope  = $true,
    [switch]$ProjectScope = $false,
    [switch]$UseExe       = $false,
    [switch]$SkipBackup   = $false,
    [switch]$OnlyReset    = $false,
    [switch]$DryRun       = $false
)

# ── Paths ─────────────────────────────────────────────────────────────────
$ProjectRoot = $PSScriptRoot

if ($UseExe) {
    $HookExe = [System.IO.Path]::Combine($ProjectRoot, 'bin', 'hooks-notifier.exe')
    if (-not (Test-Path $HookExe)) {
        Write-Error "C# executable not found at: $HookExe`nRun '.\publish.ps1' first, or use PowerShell mode (without -UseExe)."
        exit 1
    }
    $HookCommand = "`"$HookExe`""
    $HookLabel   = "C# EXE"
}
else {
    $HookScript  = [System.IO.Path]::Combine($ProjectRoot, 'hooks', 'notify.ps1')
    if (-not (Test-Path $HookScript)) {
        Write-Error "Hook script not found: $HookScript"
        exit 1
    }
    $HookCommand = "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$HookScript`""
    $HookLabel   = "PowerShell"
}

function Get-UserSettingsPath       { [System.IO.Path]::Combine($env:USERPROFILE, '.claude', 'settings.json') }
function Get-UserLocalSettingsPath  { [System.IO.Path]::Combine($env:USERPROFILE, '.claude', 'settings.local.json') }
function Get-ProjectSettingsPath    { [System.IO.Path]::Combine($ProjectRoot, '.claude', 'settings.json') }

# ── Helpers ──────────────────────────────────────────────────────────────
function Write-Step { Write-Host ">>> $($args[0])" -ForegroundColor Cyan }
function Write-Ok   { Write-Host "  + $($args[0])" -ForegroundColor Green }
function Write-Warn { Write-Host "  ! $($args[0])" -ForegroundColor Yellow }
function Write-Skip { Write-Host "  - $($args[0])" -ForegroundColor Gray }

function New-HookEntry {
    param([string]$EventName, [string]$Matcher)
    @{
        matcher = $Matcher
        hooks   = @(
            @{
                type    = 'command'
                command = $HookCommand
            }
        )
    }
}

# Convert PSCustomObject to Hashtable (PS 5.1 compat)
function ConvertTo-Hashtable {
    param($InputObject)
    if ($null -eq $InputObject) { return @{} }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $ht = @{}
        foreach ($key in $InputObject.Keys) {
            $ht[$key] = ConvertTo-Hashtable $InputObject[$key]
        }
        return $ht
    }
    if ($InputObject -is [PSCustomObject]) {
        $ht = @{}
        foreach ($prop in $InputObject.PSObject.Properties) {
            $ht[$prop.Name] = ConvertTo-Hashtable $prop.Value
        }
        return $ht
    }
    if ($InputObject -is [System.Collections.IList]) {
        $list = @()
        foreach ($item in $InputObject) {
            $list += ConvertTo-Hashtable $item
        }
        return $list
    }
    return $InputObject
}

# ── Backup ───────────────────────────────────────────────────────────────
function Backup-File {
    param([string]$Path)
    if ($SkipBackup) { return }
    if (-not (Test-Path $Path)) { return }
    $backup = "$Path.claude-hooks-notifier-backup"
    if (Test-Path $backup) {
        Write-Warn "Backup already exists: $backup (skipping)"
        return
    }
    Copy-Item -Path $Path -Destination $backup -Force
    Write-Ok "Backup created: $backup"
}

# ── Read / Write JSON ────────────────────────────────────────────────────
function Read-JsonFile {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return @{} }
    $content = Get-Content -Path $Path -Raw -Encoding utf8
    if ([string]::IsNullOrWhiteSpace($content)) { return @{} }
    try {
        $obj = $content | ConvertFrom-Json
        return ConvertTo-Hashtable $obj
    }
    catch {
        Write-Warn "Failed to parse $Path : $_"
        return @{}
    }
}

function Write-JsonFile {
    param([string]$Path, [hashtable]$Data)
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    $json = $Data | ConvertTo-Json -Depth 10
    $json | Out-File -FilePath $Path -Encoding utf8 -Force
    Write-Ok "Written: $Path"
}

# ── Reset hooks from settings ───────────────────────────────────────────
function Reset-Hooks {
    param([string]$Path, [string]$Label)
    if (-not (Test-Path $Path)) { Write-Skip "$Label : no file, skipping"; return }

    $settings = Read-JsonFile $Path
    if (-not $settings.ContainsKey('hooks') -or $settings['hooks'].Count -eq 0) {
        Write-Skip "$Label : no hooks to reset"
        return
    }

    $hooksCount = $settings['hooks'].Count
    $settings.Remove('hooks')
    Write-JsonFile $Path $settings
    Write-Ok "$Label : hooks removed ($hooksCount event(s))"
}

# ── Install hooks into settings ─────────────────────────────────────────
function Install-Hooks {
    param([string]$Path, [string]$Label)

    $settings = Read-JsonFile $Path

    if (-not $settings.ContainsKey('hooks')) {
        $settings['hooks'] = @{}
    }

    $hooks = $settings['hooks']

    $newHooks = @{
        'PermissionRequest' = @(
            (New-HookEntry 'PermissionRequest' '')
        )
        'Notification' = @(
            (New-HookEntry 'Notification' 'idle_prompt')
            (New-HookEntry 'Notification' 'permission_prompt')
            (New-HookEntry 'Notification' 'auth_success')
            (New-HookEntry 'Notification' 'elicitation_dialog')
            (New-HookEntry 'Notification' 'elicitation_complete')
        )
        'StopFailure' = @(
            (New-HookEntry 'StopFailure' '')
        )
        'PermissionDenied' = @(
            (New-HookEntry 'PermissionDenied' '')
        )
        'PostToolUse' = @(
            (New-HookEntry 'PostToolUse' 'Edit|Write')
        )
        'PostToolUseFailure' = @(
            (New-HookEntry 'PostToolUseFailure' 'Bash|Edit')
        )
    }

    $added = @()
    $skipped = @()
    foreach ($eventName in $newHooks.Keys) {
        $entry = $newHooks[$eventName]
        if ($hooks.ContainsKey($eventName)) {
            $existing = $hooks[$eventName]
            $already = $false
            foreach ($e in $existing) {
                if ($e.ContainsKey('hooks')) {
                    foreach ($h in $e.hooks) {
                        if ($h.command -eq $HookCommand) { $already = $true; break }
                    }
                }
                if ($already) { break }
            }
            if ($already) {
                $skipped += $eventName
                continue
            }
            $existing += $entry
            $added += $eventName
        }
        else {
            $hooks[$eventName] = $entry
            $added += $eventName
        }
    }

    $settings['hooks'] = $hooks
    Write-JsonFile $Path $settings

    foreach ($e in $added)  { Write-Ok "$Label : hook installed for '$e'" }
    foreach ($e in $skipped) { Write-Skip "$Label : hook already installed for '$e'" }
}

# ── Clean stale permissions from local settings ──────────────────────────
function Clean-LocalSettings {
    param([string]$Path)
    if (-not (Test-Path $Path)) { Write-Skip "Local settings: no file, skipping"; return }

    $settings = Read-JsonFile $Path
    if (-not $settings.ContainsKey('permissions') -or -not $settings['permissions'].ContainsKey('allow')) {
        Write-Skip "Local settings: no allow rules to clean"
        return
    }

    $allows = $settings['permissions']['allow']
    $oldPatterns = @('notify_plus', 'notify-plus', 'notify_plus_hook')
    $filtered = @()
    $removedCount = 0
    foreach ($rule in $allows) {
        $keep = $true
        $ruleStr = "$rule"
        foreach ($p in $oldPatterns) {
            if ($ruleStr.ToLower().Contains($p)) { $keep = $false; break }
        }
        if ($keep) { $filtered += $rule } else { $removedCount++ }
    }

    if ($removedCount -eq 0) {
        Write-Skip "Local settings: no stale permission entries found"
        return
    }

    if ($DryRun) {
        Write-Warn "Local settings: would remove $removedCount stale entry/entries"
        return
    }

    $settings['permissions']['allow'] = $filtered
    Write-JsonFile $Path $settings
    Write-Ok "Local settings: removed $removedCount stale permission entry/entries"
}

# ══════════════════════════════════════════════════════════════════════════
#  Main
# ══════════════════════════════════════════════════════════════════════════
Write-Host ''
Write-Host '============================================' -ForegroundColor Cyan
Write-Host '  Claude Code Hooks Notifier - Setup' -ForegroundColor Cyan
Write-Host '============================================' -ForegroundColor Cyan
Write-Host ''

Write-Step "Hook backend: $HookLabel"
Write-Step "Hook command: $HookCommand"
Write-Host ''

# ── Resolve target paths ────────────────────────────────────────────────
$targets = @()
if ($GlobalScope)  { $targets += @{ Path = Get-UserSettingsPath;     Label = 'Global settings' } }
if ($ProjectScope) { $targets += @{ Path = Get-ProjectSettingsPath;  Label = 'Project settings' } }

if ($targets.Count -eq 0) {
    Write-Error 'No target specified. Use -GlobalScope and/or -ProjectScope.'
    exit 1
}

# ── Dry-run info ────────────────────────────────────────────────────────
if ($DryRun) {
    Write-Warn 'DRY RUN - no files will be modified'
    Write-Host ''
}

# ── Phase 1: Backup ─────────────────────────────────────────────────────
Write-Step 'Phase 1: Backup existing settings'
if ($DryRun) {
    Write-Skip 'Would backup settings files'
}
else {
    foreach ($t in $targets) { Backup-File $t.Path }
    if (-not $SkipBackup) { Backup-File (Get-UserLocalSettingsPath) }
}
Write-Host ''

# ── Phase 2: Reset (remove old hooks) ───────────────────────────────────
Write-Step 'Phase 2: Reset old hooks configuration'
foreach ($t in $targets) {
    if ($DryRun) {
        Write-Skip "Would reset hooks in $($t.Label)"
    }
    else {
        Reset-Hooks $t.Path $t.Label
    }
}
Write-Host ''

# ── Phase 3: Install new hooks ──────────────────────────────────────────
if (-not $OnlyReset) {
    Write-Step 'Phase 3: Install new hooks'
    foreach ($t in $targets) {
        if ($DryRun) {
            Write-Skip "Would install hooks in $($t.Label)"
        }
        else {
            Install-Hooks $t.Path $t.Label
        }
    }
    Write-Host ''
}
else {
    Write-Step 'Phase 3: Install new hooks - skipped (OnlyReset mode)'
    Write-Host ''
}

# ── Phase 4: Clean stale permissions ─────────────────────────────────────
Write-Step 'Phase 4: Clean stale permission entries'
if ($DryRun) {
    Write-Skip 'Would clean stale permissions'
}
else {
    Clean-LocalSettings (Get-UserLocalSettingsPath)
}
Write-Host ''

# ── Phase 5: AUMID registration (EXE mode only) ─────────────────────────
if ($UseExe -and -not $OnlyReset -and -not $DryRun) {
    Write-Step 'Phase 5: Register AUMID for toast notifications'
    try {
        $regResult = Start-Process -FilePath $HookExe -ArgumentList '--register' -NoNewWindow -Wait -PassThru
        if ($regResult.ExitCode -eq 0) {
            Write-Ok 'AUMID registration successful'
        }
        else {
            Write-Warn "AUMID registration exited with code $($regResult.ExitCode) (might be non-critical)"
        }
    }
    catch {
        Write-Warn "AUMID registration failed: $_ (notifications may still work)"
    }
    Write-Host ''
}

# ── Summary ──────────────────────────────────────────────────────────────
Write-Step 'Summary'
if ($DryRun) {
    Write-Ok 'Dry run complete - no files changed'
}
else {
    Write-Ok 'Setup complete!'
    Write-Host ''
    Write-Host "  Backend : $HookLabel" -ForegroundColor Yellow
    Write-Host "  Command : $HookCommand" -ForegroundColor Yellow
    Write-Host ''
    if ($UseExe) {
        Write-Host '  AUMID registration attempted. If toasts do not appear,' -ForegroundColor Yellow
        Write-Host '  run this manually: hooks-notifier.exe --register' -ForegroundColor Yellow
        Write-Host ''
    }
    Write-Host '  Restart Claude Code for changes to take effect.' -ForegroundColor Green
}
Write-Host ''
