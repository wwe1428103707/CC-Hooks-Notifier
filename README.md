<p align="right">
  <a href="README_ZH.md">中文</a>
</p>

<div align="center">
  <img src="https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/icon.png" alt="Tray icon" width="48" style="vertical-align: middle; margin-right: 8px;"/>
  <h1 style="display: inline-block; vertical-align: middle;">Claude Code Hooks Notifier</h1>
</div>

<p align="center">
  <b>Never miss a moment from Claude Code.</b><br>
  <sub>Native Windows toast notifications + QQ-style system tray alerts for your AI coding companion.</sub>
</p>

<p align="center">
  <a href="https://github.com/wwe1428103707/CC-Hooks-Notifier/releases"><img src="https://img.shields.io/github/v/release/wwe1428103707/CC-Hooks-Notifier?include_prereleases&label=latest" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/wwe1428103707/CC-Hooks-Notifier" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B-blue" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-9-purple" alt=".NET 9">
</p>

![AppScreenshot](https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/example.png)

---

## What is this?

You are deep in another window — coding, browsing, writing — while Claude Code is churning away in the background. **How do you know when it needs you?**

That is exactly what **CC Hooks Notifier** solves. It sits quietly in your system tray, and the moment Claude Code fires a hook event, it lights up:

- A **native Windows toast** slides in to tell you what happened
- The **tray icon blinks** like QQ/WeChat — you can't miss it
- Hover to see **how many unread notifications** are waiting
- **Single-click** to open the full event dashboard

Think of it as your Claude Code activity monitor. It watches 17 different hook events — from "task is done, come take a look" to "permission needed" to "API error, something broke" — and keeps you in the loop without alt-tabbing back to the terminal.

---

## Features

| Category | Highlights |
|----------|-----------|
| **Notification Center** | QQ-style icon blinking (appear / disappear), unread count on hover, single-click opens event log |
| **Toast Notifications** | Native Windows 10/11 toasts — no browser dependency, works even when terminal is minimized |
| **Permission Dialog** | Interactive popup with Allow / Deny + "Always allow" checkboxes, right when you need it |
| **Event Dashboard** | WebView2-powered UI — real-time counters, event log with read/unread filters, hook toggle switches |
| **17 Hook Events** | PermissionRequest, Notification, StopFailure, PostToolUse, SubagentStart/Stop, TaskCreated/Completed, and more |
| **Priority Levels** | P0 (critical, long blink) / P0.5 (important, short blink) / P1 (toast) / P2 (silent counter) |
| **Tray Menu** | Quick access to notifications, hook config, language switch, blink toggle, auto-start |
| **Multi-language** | English & 简体中文 — switch anytime from tray or settings |
| **Persistence** | Event history survives restarts; unread state saved to disk |

---

## Architecture

Two lightweight executables, one seamless experience:

```
Claude Code hooks
       │
       ▼
┌─────────────────┐     Named Pipe IPC      ┌─────────────────────┐
│  hooks-notify   │ ◄──────────────────────► │  hooks-notifier     │
│  (CLI handler)  │    JSON, single-line     │  (tray process)     │
│                 │                          │                     │
│  • Toast popup  │                          │  • System tray icon │
│  • Permission   │                          │  • Blink animation  │
│    dialog       │                          │  • Event history    │
│  • Fire &       │                          │  • WebView2 UI      │
│    forget       │                          │  • Single instance  │
└─────────────────┘                          └─────────────────────┘
```

---

## Quick Start

### 1. Install

Download the latest installer from [Releases](https://github.com/wwe1428103707/CC-Hooks-Notifier/releases), run it, done. The installer registers everything automatically.

### 2. Configure

```powershell
.\hooks-notifier.exe --configure-hooks
```

This wires up `~/.claude/settings.json` so Claude Code talks to the notifier.

### 3. Done

The tray icon appears. Go ahead — Claude Code events will now reach you wherever you are on Windows.

---

## Requirements

- **Windows 10** (build 17763+) or **Windows 11**
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (pre-installed on Windows 11)
- [Claude Code](https://claude.ai/code)

---

## How the Notification Center Works

<p align="center">
  <b>Event arrives → Icon blinks → Hover shows count → Click to view → All clear</b>
</p>

1. A P0 or P0.5 event fires (e.g. "task complete", "permission needed")
2. The tray bell **blinks** — appear / disappear — just like QQ or WeChat
3. **Hover** the icon: tooltip says *"3 unread notifications"*
4. Right-click: the top menu item reads *"View Notifications (3 unread)"*
5. **Single-click** the tray icon — blinking stops, everything marked read, dashboard opens with new events highlighted
6. Unread state is **persisted to disk** — restart the app and your unread events are still there

---

## Dashboard

| Tab | What you get |
|-----|-------------|
| **Dashboard** | At-a-glance counters: total / unread / P0 / toast / subagents / tasks, plus recent events and per-event enable/disable toggles |
| **Event Log** | Full history table with read/unread highlighting (amber background + colored dot). Filter: All / Unread / P0 / P0.5 / Toast. One-click "Mark All Read". |
| **Settings** | Language (EN / 中文), auto-start, hook executable path management, open settings.json directly |
| **About** | Version info and event coverage table |

---

## Hook Event Coverage

| Event | Priority | What triggers it |
|-------|----------|-----------------|
| Notification(idle_prompt) | **P0** | Claude finishes a round, ready for your next prompt |
| Notification(permission_prompt) | **P0** | Claude is blocked waiting for you to approve a tool |
| StopFailure | **P0** | API error or runtime failure — something went wrong |
| Stop | **P0.5** | Claude completed a response |
| TaskCompleted | **P0.5** | A tracked task was completed |
| SessionEnd | **P0.5** | Session ended (clear / logout / exit) |
| PermissionRequest | P1 | Claude needs tool permission |
| PostToolUse(Edit\|Write) | P1 | A file was edited or written |
| PostToolUseFailure | P1 | A tool call returned an error |
| SubagentStop | P1 | A subagent finished its work |
| PermissionDenied | P1 | You denied a tool request |
| SessionStart | P1 | New session or session resumed |
| PostCompact | P1 | Context compaction completed |
| ConfigChange | P1 | Settings were modified |
| SubagentStart | P2 | A subagent was spawned |
| TaskCreated | P2 | A new task was created |
| PreCompact | P2 | Context is about to be compacted |

---

## Build from Source

```powershell
# React UI
cd webui && npm install && npm run build && cd ..

# Lightweight hook handler
dotnet publish src/NotifyHook/NotifyHook.csproj -c Release -o bin --sc false

# Tray + dashboard
dotnet publish src/HooksNotifier/HooksNotifier.csproj -c Release -o bin --sc false

# (Optional) Build installer
# "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
```

**Dev prerequisites:** .NET 9 SDK, Node.js 20+, Inno Setup 6 (for installer)

---

## Project Structure

```
├── src/
│   ├── HooksNotifier/         # Tray app (WinForms + WebView2)
│   │   ├── TrayMode.cs        # System tray icon, blink, context menu
│   │   ├── MainWindow.cs      # WebView2 dashboard host
│   │   ├── HookConfig.cs      # ~/.claude/settings.json reader/writer
│   │   ├── IpcService.cs      # Named pipe IPC server
│   │   ├── ToastService.cs    # WinRT toast notifications
│   │   ├── IconHelper.cs      # GDI+ bell icon (normal, highlighted, blank)
│   │   ├── EventHistory.cs    # Persistent ring buffer with unread state
│   │   ├── Models.cs          # Shared data models
│   │   └── i18n/              # en.json, zh.json
│   └── NotifyHook/            # Lightweight CLI hook handler
│       └── Program.cs         # Toast + permission dialog + IPC client
├── webui/                     # React 19 + shadcn/ui + Tailwind v4
├── docs/                      # Requirements, roadmap, interface contract
├── setup.iss                  # Inno Setup installer script
├── setup.ps1                  # Configuration helper
└── publish.ps1                # Build helper
```

---

## License

MIT — feel free to use, fork, and contribute.
