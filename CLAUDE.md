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

**Version files to update together:**
- `setup.iss` → `#define MyAppVersion "x.y.z"`
- `.claude-plugin/plugin.json` → `"version": "x.y.z"`

After version bump, always rebuild:
```
"%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
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
