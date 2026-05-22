<div align="center">
  <img src="https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/icon.png" alt="Tray icon" width="48" style="vertical-align: middle; margin-right: 8px;"/>
  <h1 style="display: inline-block; vertical-align: middle;">Claude Code Hooks Notifier</h1>
</div>

Windows system tray notification service for [Claude Code](https://claude.ai/code) hooks. Displays WinRT toast notifications for Claude Code events — permission requests, task completions, errors, subagent activity, and more.

![AppScreenshot](https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/example.png)

## Features

- **WinRT Toast Notifications** — native Windows 10/11 toasts for all hook events
- **System Tray Icon** — bell icon with blinking animation on new events, context menu with counters
- **Interactive Permission Dialog** — allow/deny tool calls with "always allow" options
- **WebView2 Dashboard** — real-time event history, hook toggle controls, settings
- **17 Hook Events** — covers PermissionRequest, Notification, StopFailure, PostToolUse, SubagentStart/Stop, TaskCreated/Completed, and more
- **Priority Levels** — P0 (critical) / P1 (important) / P2 (informational) with different blink behavior
- **Multi-language** — English and Chinese (简体中文)
- **Named Pipe IPC** — lightweight hook handler communicates with tray process
- **Auto-start** — optional login startup via installer

## Architecture

Two components work together:

| Component | File | Description |
|-----------|------|-------------|
| **hooks-notify** | `src/NotifyHook/` | Lightweight CLI called by Claude Code hooks. Shows toasts and permission dialogs. Sends events to tray via named pipe. |
| **hooks-notifier** | `src/HooksNotifier/` | Background tray process (WinForms + WebView2). One instance only (mutex). Shows tray icon, menu, and dashboard UI. |

**IPC**: Named pipe `\\.\pipe\ClaudeCodeHooks` — JSON single-line, UTF-8.

## Requirements

- Windows 10 (build 17763+) or Windows 11
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (framework-dependent deployment)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Windows 11)
- [Claude Code](https://claude.ai/code)

## Installation

### Option 1: Installer (recommended)

1. Download the latest `ClaudeCodeHooksNotifier-Setup.exe` from [Releases](https://github.com/wwe1428103707/CC-Hooks-Notifier/releases)
2. Run the installer — it registers the AUMID for toast notifications automatically
3. Check "Start automatically when I log in" for auto-start

### Option 2: Claude Code plugin

If you use Claude Code's plugin system, install via the plugin configuration.

### Option 3: Build from source

```powershell
# Build React UI
cd webui
npm install
npm run build
cd ..

# Build hooks-notify (lightweight hook handler)
dotnet publish src\NotifyHook\NotifyHook.csproj --configuration Release --output bin --self-contained false

# Build hooks-notifier (tray + dashboard)
dotnet publish src\HooksNotifier\HooksNotifier.csproj --configuration Release --output bin --self-contained false

# (Optional) Build installer
# "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
```

## Usage

### Modes

| Command | Description |
|---------|-------------|
| `hooks-notifier --tray` | Start background tray process (auto-start after install) |
| `hooks-notifier --hook` | Process a hook event (stdin JSON, stdout JSON). Called by Claude Code. |
| `hooks-notifier --register` | Register AUMID for WinRT toast notifications |
| `hooks-notifier --configure-hooks` | Update `~/.claude/settings.json` hook paths to current EXE |

### Setup with Claude Code

After installation, run:

```powershell
# Auto-configure hooks to use hooks-notify.exe
.\hooks-notifier.exe --configure-hooks
```

Or use the included setup script:

```powershell
.\setup.ps1 -GlobalScope -UseExe
```

This updates `~/.claude/settings.json` to hook into Claude Code events.

## Hook Events

| Event | Priority | Description |
|-------|----------|-------------|
| Notification(idle_prompt) | P0 | Task complete — ready for input |
| Notification(permission_prompt) | P0 | Claude waiting for approval |
| StopFailure | P0 | API error or failure |
| Stop | P0.5 | Claude finished responding |
| TaskCompleted | P0.5 | Task done |
| SessionEnd | P0.5 | Session ended |
| PermissionRequest | P1 | Tool needs permission |
| PostToolUseFailure | P1 | Tool execution failed |
| PostToolUse(Edit\|Write) | P1 | File edited/written |
| SubagentStop | P1 | Subagent finished |
| PermissionDenied | P1 | Tool call denied |
| SessionStart | P1 | Session started |
| SubagentStart | P2 | Subagent created |
| TaskCreated | P2 | New task created |
| PreCompact | P2 | Context about to compact |

## Dashboard

The tray icon context menu includes a "Dashboard" option that opens a WebView2 window with:

- **Dashboard** tab — service status, notification/subagent/task counters, recent events
- **Event Log** tab — full event history with timestamps, level, and content
- **Settings** tab — language picker, auto-start toggle, hook path management
- **About** tab — version info

## Development

### Prerequisites

- .NET 9 SDK
- Node.js 20+
- Inno Setup 6 (for installer builds)

### Project structure

```
├── src/
│   ├── HooksNotifier/         # Tray app (WinForms + WebView2)
│   │   ├── TrayMode.cs        # System tray icon and menu
│   │   ├── MainWindow.cs      # WebView2 dashboard window
│   │   ├── HookConfig.cs      # Read/write settings.json hooks
│   │   ├── IpcService.cs      # Named pipe IPC server
│   │   ├── ToastService.cs    # WinRT toast notifications
│   │   ├── IconHelper.cs      # GDI+ bell icon rendering
│   │   ├── EventHistory.cs    # In-memory event history
│   │   ├── Models.cs          # Shared data models
│   │   └── i18n/              # Language files (en, zh)
│   └── NotifyHook/            # Lightweight hook handler
│       └── Program.cs         # Toast + permission dialog + IPC
├── webui/                     # React + shadcn/ui dashboard
├── installer/                 # Inno Setup Chinese language pack
├── setup.iss                  # Inno Setup script
├── setup.ps1                  # Configuration script
└── publish.ps1                # Build helper
```

## Contributors

Thanks to the following people who have contributed to this project:

- [@wwe1428103707](https://github.com/wwe1428103707) — project creator and maintainer

## License

MIT
