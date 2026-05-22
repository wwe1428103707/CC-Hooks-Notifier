# 配置面板与仪表盘 — 需求报告

## 1. 概述

为托盘程序添加一个可交互的窗口界面，取代当前仅通过右键菜单和系统 Toast 进行交互的方式。用户可通过双击托盘图标打开该窗口，浏览实时状态、查看事件历史、修改配置。

## 2. 用户故事

| ID | 用户故事 | 优先级 |
|----|---------|--------|
| US1 | 作为用户，我想双击托盘图标打开一个窗口，在此窗口中可以看到当前 hook 活动的概况 | P0 |
| US2 | 作为用户，我想在窗口中查看最近发生的 hook 事件列表，以便回顾 Claude 的活动 | P0 |
| US3 | 作为用户，我想在窗口中切换语言、配置开机自启、更新 hook 路径 | P0 |
| US4 | 作为用户，我希望事件列表实时更新，无需手动刷新 | P1 |
| US5 | 作为用户，我想查看程序的版本信息和简短说明 | P1 |
| US6 | 作为用户，我想从窗口中快速跳转到 Claude 的设置文件进行编辑 | P2 |
| US7 | 作为用户，我可以在窗口中清除事件历史或重置计数器 | P2 |

## 3. 窗口布局

```
┌──────────────────────────────────────────────────────┐
│  Claude Code Hooks Notifier  — [—][□][×]             │
├──────────────────────────────────────────────────────┤
│  [📊 Dashboard]  [📋 Event Log]  [⚙ Settings]  [ℹ About] │
├──────────────────────────────────────────────────────┤
│                                                      │
│  (tab content area)                                  │
│                                                      │
│                                                      │
├──────────────────────────────────────────────────────┤
│  Status: Running  |  Language: English  |  v1.4.0    │
└──────────────────────────────────────────────────────┘
```

### 3.1 窗口规格

| 属性 | 值 |
|------|-----|
| 尺寸 | 900 × 600 |
| 最小尺寸 | 800 × 500 |
| 启动位置 | 屏幕居中 |
| 边框 | FixedDialog（可调整大小？Sizable） |
| 置顶 | 否（可配置） |

### 3.2 Tab 栏

使用 `TabControl`，4 个 Tab 页。

## 4. Tab 详情

### 4.1 📊 Dashboard（仪表盘）

实时状态概览，替代当前右键菜单中的状态文字。

| 区域 | 内容 |
|------|------|
| **连接状态** | 🟢 Running / 🔴 Stopped（IPC 服务状态） |
| **通知统计** | 总通知数、闪烁数、Toast 数 |
| **子代理** | 当前活跃子代理、总启动次数、最近结束的代理 |
| **任务** | 总任务数、最近任务描述 |
| **最近事件** | 最新 5 条事件摘要（时间 + 类型 + 简述） |

布局示例：

```
┌──────────────────────────────────────────────────┐
│  🟢 Service Running                               │
│                                                   │
│  ┌──────────────┐  ┌──────────────┐               │
│  │ Notifications │  │  Subagents   │               │
│  │    23 total   │  │  5 spawned   │               │
│  │    8 blinks   │  │  4 finished  │               │
│  └──────────────┘  └──────────────┘               │
│                                                   │
│  ┌──────────────┐  ┌──────────────┐               │
│  │    Tasks     │  │   Session    │               │
│  │   12 total   │  │   Started    │               │
│  │   Last: Fix  │  │   09:30:22   │               │
│  └──────────────┘  └──────────────┘               │
│                                                   │
│  Recent Events:                                    │
│  [09:32] ✅ TaskCompleted — Implement auth         │
│  [09:31] 🔔 Notification — idle_prompt            │
│  [09:30] 📝 SubagentStart — Explore               │
└──────────────────────────────────────────────────┘
```

### 4.2 📋 Event Log（事件日志）

可滚动的历史事件列表，显示最近的 hook 事件。

| 列 | 说明 |
|----|------|
| 时间 | HH:mm:ss 格式 |
| 级别 | 🔔 P0 / 🔔 P0.5 / 📢 Toast / 🟢 Stateful |
| 事件 | 事件名称（Notification, StopFailure 等）|
| 内容 | 事件摘要文本 |

**功能需求：**

| 功能 | 说明 |
|------|------|
| 滚动 | 自动滚动到底部，可手动滚动查看历史 |
| 最大条目 | 内存中保留最近 500 条 |
| 清空 | "Clear" 按钮清空列表和计数器 |
| 实时更新 | 通过 IPC `stateful` 消息实时追加 |

### 4.3 ⚙ Settings（设置）

配置选项页面。

| 配置项 | 控件 | 说明 |
|--------|------|------|
| 语言 | 下拉框（English / 中文）| 切换后立即生效 |
| 开机自启 | 开关/复选框 | 同当前右键菜单 |
| Hook 路径 | 文本框 + "Update" 按钮 | 显示当前路径，点击更新 |
| 通知级别 | 复选框组 | 开启/关闭特定级别通知 |
| 配置目录 | 文本框 + "Open" 按钮 | 显示 settings.json 路径，点击打开 |

### 4.4 ℹ About（关于）

| 内容 | 说明 |
|------|------|
| 应用名称 | Claude Code Hooks Notifier |
| 版本 | v1.4.0 |
| 说明 | Windows 系统托盘通知服务 |
| 技术栈 | .NET 9, WinRT, WinForms |
| 版权 | 见 `.claude-plugin/plugin.json` |

## 5. 技术实现

### 5.1 文件结构

```
src/HooksNotifier/
├── MainWindow.cs          # 主窗口（TabControl + 4 tabs）
├── MainWindow.Dashboard.cs  # Dashboard tab 逻辑（分部类）
├── MainWindow.EventLog.cs   # Event Log tab 逻辑（分部类）
├── MainWindow.Settings.cs   # Settings tab 逻辑（分部类）
├── MainWindow.About.cs      # About tab 逻辑（分部类）
└── i18n/*.json              # 新增窗口相关字符串 key
```

使用 `partial class` 将每个 Tab 的逻辑拆分到单独文件。

### 5.2 IPC 扩展

新增 IPC 消息类型用于事件历史传递：

```json
// 当前 stateful 消息扩展 eventHistory 字段
{
  "type": "stateful",
  "eventName": "Notification",
  "eventType": "idle_prompt",
  "title": "Task complete",
  "body": "...",
  "blinkType": "long",
  "timestamp": "2026-05-22T10:30:00Z"
}
```

### 5.3 MainWindow 生命周期

```
TrayMode.Run()
  │
  ├── Build tray icon (existing)
  ├── IPC server (existing)
  │
  └── On double-click → MainWindow.Show() / MainWindow.Activate()
       │
       ├── Tab 1: Dashboard
       │   ├── Labels with counters
       │   └── Recent event list (last 5)
       │
       ├── Tab 2: Event Log
       │   ├── DataGridView / ListView
       │   └── 500 event ring buffer
       │
       ├── Tab 3: Settings
       │   ├── Language dropdown
       │   ├── Auto-start checkbox
       │   ├── Hook path text + Update button
       │   └── Notification level toggles
       │
       └── Tab 4: About
           └── Static labels
```

### 5.4 事件历史缓冲区

```csharp
internal class EventHistory
{
    private readonly List<EventEntry> _entries = new();
    private const int MaxEntries = 500;

    public void Add(EventEntry entry) { ... }
    public EventEntry[] GetRecent(int count) { ... }
    public void Clear() { ... }
}

internal record EventEntry(
    DateTime Timestamp,
    string Level,       // "P0", "P0.5", "Toast", "Stateful"
    string EventName,
    string Summary
);
```

### 5.5 MainWindow 与 TrayMode 通信

MainWindow 由 TrayMode 管理：

- TrayMode 持有 MainWindow 引用（或使用事件）
- 每次 IPC `stateful` 消息到达时，TrayMode 同时更新：
  1. 菜单状态文字（已有）
  2. MainWindow 上的 Dashboard 和 EventLog（如果窗口已打开）
- MainWindow 通过 `BeginInvoke` 接收 UI 线程更新

```csharp
// TrayMode 中：
private static MainWindow? _mainWindow;

// 双击托盘时：
_trayIcon.DoubleClick += (_, _) =>
{
    StopBlinking();
    if (_mainWindow == null || _mainWindow.IsDisposed)
    {
        _mainWindow = new MainWindow();
        _mainWindow.Show();
    }
    else
    {
        _mainWindow.Activate();
    }
};
```

## 6. i18n 新增 Key

约 20 个新增 key 需要添加到 `en.json` 和 `zh.json`：

| Key | 英文 | 中文 |
|-----|------|------|
| `window.title` | Claude Code Hooks Notifier | Claude Code Hooks Notifier |
| `tab.dashboard` | Dashboard | 仪表盘 |
| `tab.event_log` | Event Log | 事件日志 |
| `tab.settings` | Settings | 设置 |
| `tab.about` | About | 关于 |
| `dashboard.service_running` | Service Running | 服务运行中 |
| `dashboard.notifications` | Notifications | 通知 |
| `dashboard.subagents` | Subagents | 子代理 |
| `dashboard.tasks` | Tasks | 任务 |
| `dashboard.total` | {0} total | 共 {0} 个 |
| `dashboard.last` | Last: {0} | 最近: {0} |
| `dashboard.recent_events` | Recent Events | 最近事件 |
| `event_log.time` | Time | 时间 |
| `event_log.level` | Level | 级别 |
| `event_log.event` | Event | 事件 |
| `event_log.content` | Content | 内容 |
| `event_log.clear` | Clear | 清空 |
| `settings.language` | Language | 语言 |
| `settings.auto_start` | Start at login | 开机自启 |
| `settings.hook_path` | Hook Executable Path | Hook 执行路径 |
| `settings.update` | Update | 更新 |
| `settings.open_file` | Open File | 打开文件 |
| `about.version` | Version: {0} | 版本: {0} |
| `about.tech_stack` | Tech Stack: .NET 9, WinRT, WinForms | 技术栈: .NET 9, WinRT, WinForms |

## 7. 实施步骤

| Step | 内容 | 文件 |
|------|------|------|
| 1 | 创建 `EventHistory` 类（事件环缓冲区） | `EventHistory.cs` |
| 2 | 创建 `MainWindow` 骨架（TabControl + 4 tabs 布局） | `MainWindow.cs` |
| 3 | 实现 Dashboard tab（计数器 + 最近事件列表） | `MainWindow.Dashboard.cs` |
| 4 | 实现 Event Log tab（历史列表 + 清空） | `MainWindow.EventLog.cs` |
| 5 | 实现 Settings tab（语言、自启、hook 路径） | `MainWindow.Settings.cs` |
| 6 | 实现 About tab | `MainWindow.About.cs` |
| 7 | 集成到 TrayMode（双击打开、IPC 事件推送） | `TrayMode.cs` |
| 8 | 新增 i18n 字符串（约 25 个 key） | `en.json`, `zh.json` |
| 9 | 更新 event_history 表（将 onMessage 信息存储到EventHistory中） | `TrayMode.cs` |

## 8. 版本

| 版本 | 内容 |
|------|------|
| 1.4.x | 功能实现 |
| 1.5.0 | 发布版本 |
