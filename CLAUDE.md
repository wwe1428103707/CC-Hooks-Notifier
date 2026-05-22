# Claude Code Hooks Notifier

## Project Overview
Windows system tray notification service for Claude Code hooks.  
Bell icon with blinking animation, WinRT toast notifications, named pipe IPC.

## Versioning

Version format: `MAJOR.MINOR.PATCH` (SemVer).

After rebuilding the installer (`ISCC.exe setup.iss`), use these rules:

| Change Type | Version Bump | Examples |
|-------------|-------------|----------|
| **Patch** | `x.y.z → x.y.z+1` | Bug fixes, UI tweaks, logging improvements, dependency updates |
| **Minor** | `x.y.z → x.y+1.0` | New features (tray menu items, IPC messages, new modes), non-breaking additions |
| **Major** | `x.y.z → x+1.0.0` | Breaking changes (CLI flag rename, IPC protocol change, config format change) |

**All version files MUST be updated together:** (use `grep -rn` to verify no stale versions remain)

| File | Field |
|------|-------|
| `setup.iss` | `#define MyAppVersion "x.y.z"` |
| `.claude-plugin/plugin.json` | `"version": "x.y.z"` |
| `src/HooksNotifier/TrayMode.cs` | `I18n.Get("about.version", "x.y.z")` |
| `webui/src/App.tsx` | `t("about.version", "x.y.z")` |
| `webui/src/i18n.ts` | `"header.version": "vx.y.z"` (both en and zh) |

After version bump, always rebuild and verify:
```powershell
# Rebuild all components
cd src\HooksNotifier && dotnet publish --configuration Release --output ..\..\bin --self-contained false
cd ..\NotifyHook && dotnet publish --configuration Release --output ..\..\bin --self-contained false
cd ..\..\webui && npm run build
# Build installer
"%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
# Verify no stale versions
grep -rn '1\.4\.0\|1\.5\.0\|1\.3\.0\|1\.2\.0' --include="*.cs" --include="*.ts" --include="*.tsx" --exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin .
```

## Build

```powershell
# 1. Build C# project
cd src\HooksNotifier && dotnet publish --configuration Release --output ..\..\bin --self-contained false

# 2. Build installer
cd ..\.. && "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
```

## Key Modes

| Mode | Description |
|------|-------------|
| `--hook` | Process hook event (stdin JSON, stdout JSON). Called by Claude Code. |
| `--tray` | Background tray process. One instance only (mutex). |
| `--register` | Register AUMID for WinRT toast notifications. |
| `--configure-hooks` | Update ~/.claude/settings.json hook paths to current EXE. |

## IPC Protocol

Named pipe: `\\.\pipe\ClaudeCodeHooks`

Message format: JSON single-line, UTF-8, `\n` terminated.

## Architecture Notes

- WinExe (no console window for --tray mode)
- WinRT `Windows.UI.Notifications` for toasts (no external deps)
- `System.IO.Pipes` for hook→tray IPC
- GDI+ bell icon (blue normal, orange highlight for blinking)
- Inno Setup for installer packaging
