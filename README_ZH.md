<p align="right">
  <a href="README.md">English</a>
</p>

<div align="center">
  <img src="https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/icon.png" alt="Tray icon" width="48" style="vertical-align: middle; margin-right: 8px;"/>
  <h1 style="display: inline-block; vertical-align: middle;">Claude Code Hooks Notifier</h1>
</div>

用于 [Claude Code](https://claude.ai/code) 的 Windows 系统托盘通知服务。为 Claude Code 事件（权限请求、任务完成、报错、子代理活动等）显示 WinRT Toast 通知。

![应用截图](https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/example.png)

## 功能特性

- **QQ 风格通知中心** — 未读计数 tooltip、持续闪烁（图标↔消失）、悬停查看未读数、单击打开面板
- **WinRT Toast 通知** — 所有 hook 事件的原生 Windows 10/11 通知
- **系统托盘图标** — 铃铛图标以出现/消失交替闪烁，右键菜单显示计数
- **交互式权限弹窗** — 允许/拒绝工具调用，支持"始终允许"选项
- **WebView2 仪表盘** — 实时事件历史，支持已读/未读状态、筛选标签、Hook 开关控制、设置
- **17 种 Hook 事件** — 覆盖 PermissionRequest、Notification、StopFailure、PostToolUse、SubagentStart/Stop、TaskCreated/Completed 等
- **优先级分级** — P0（关键，长闪烁）/ P0.5（重要，短闪烁）/ P1（Toast）/ P2（后台）
- **多语言支持** — 英文 (English) 和中文 (简体中文)
- **命名管道 IPC** — 轻量 Hook 处理器与托盘进程通信
- **开机自启** — 安装时可选择登录时自动启动
- **闪烁开关** — 可在托盘菜单中开启/关闭图标闪烁

## 架构

两个组件协同工作：

| 组件 | 文件 | 说明 |
|------|------|------|
| **hooks-notify** | `src/NotifyHook/` | 轻量级 CLI，由 Claude Code hooks 调用。显示 Toast 和权限弹窗，通过命名管道向托盘发送事件。 |
| **hooks-notifier** | `src/HooksNotifier/` | 后台托盘进程（WinForms + WebView2）。单实例运行（互斥锁），显示托盘图标、菜单和仪表盘界面。 |

**IPC**: 命名管道 `\\.\pipe\ClaudeCodeHooks` — JSON 单行 UTF-8 格式。

## 系统要求

- Windows 10（build 17763+）或 Windows 11
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)（框架依赖部署）
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 11 预装）
- [Claude Code](https://claude.ai/code)

## 安装

### 方式一：安装包（推荐）

1. 从 [Releases](https://github.com/wwe1428103707/CC-Hooks-Notifier/releases) 下载最新的 `ClaudeCodeHooksNotifier-Setup.exe`
2. 运行安装包 — 它会自动注册 AUMID 用于 Toast 通知
3. 勾选"登录时自动启动"可开启开机自启

### 方式二：Claude Code 插件

如果你使用 Claude Code 的插件系统，通过插件配置安装。

### 方式三：从源码构建

```powershell
# 构建 React UI
cd webui
npm install
npm run build
cd ..

# 构建 hooks-notify（轻量 Hook 处理器）
dotnet publish src\NotifyHook\NotifyHook.csproj --configuration Release --output bin --self-contained false

# 构建 hooks-notifier（托盘 + 仪表盘）
dotnet publish src\HooksNotifier\HooksNotifier.csproj --configuration Release --output bin --self-contained false

# （可选）构建安装包
# "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
```

## 使用方法

### 运行模式

| 命令 | 说明 |
|------|------|
| `hooks-notifier --tray` | 启动后台托盘进程（安装后自动启动） |
| `hooks-notifier --hook` | 处理 hook 事件（stdin JSON，stdout JSON）。由 Claude Code 调用。 |
| `hooks-notifier --register` | 注册 AUMID 用于 WinRT Toast 通知 |
| `hooks-notifier --configure-hooks` | 更新 `~/.claude/settings.json` 中的 hook 路径为当前 EXE |

### 与 Claude Code 集成

安装后运行：

```powershell
# 自动配置 hooks 使用 hooks-notify.exe
.\hooks-notifier.exe --configure-hooks
```

或使用自带的配置脚本：

```powershell
.\setup.ps1 -GlobalScope -UseExe
```

这会更新 `~/.claude/settings.json`，将 Claude Code 事件接入通知系统。

## Hook 事件表

| 事件 | 优先级 | 说明 |
|------|--------|------|
| Notification(idle_prompt) | P0 | 任务完成 — 等待输入 |
| Notification(permission_prompt) | P0 | Claude 等待授权 |
| StopFailure | P0 | API 错误或故障 |
| Stop | P0.5 | Claude 响应完毕 |
| TaskCompleted | P0.5 | 任务完成 |
| SessionEnd | P0.5 | 会话结束 |
| PermissionRequest | P1 | 工具需要授权 |
| PostToolUseFailure | P1 | 工具执行失败 |
| PostToolUse(Edit\|Write) | P1 | 文件已编辑/写入 |
| SubagentStop | P1 | 子代理完成 |
| PermissionDenied | P1 | 工具调用被拒 |
| SessionStart | P1 | 会话开始 |
| SubagentStart | P2 | 子代理已创建 |
| TaskCreated | P2 | 新任务已创建 |
| PreCompact | P2 | 上下文即将压缩 |

## 仪表盘

单击托盘图标或使用右键菜单打开 WebView2 仪表盘：

- **仪表盘**标签 — 通知/子代理/任务/未读计数卡片、最近事件、Hook 事件开关
- **事件日志**标签 — 完整事件历史，已读/未读状态（橙色高亮 + 彩色圆点），按 全部/未读/P0/P0.5/Toast 筛选，一键全部标为已读
- **设置**标签 — 语言选择、开机自启开关、Hook 路径管理
- **关于**标签 — 版本信息、技术栈

## 通知中心

当 P0 或 P0.5 事件到达时：

1. 托盘图标开始闪烁（出现/消失交替，类似 QQ/微信）
2. 鼠标悬停图标 — tooltip 显示未读数量（如"3 条未读通知"）
3. 右键菜单顶部显示"查看通知（N 条未读）"
4. 单击托盘图标 — 闪烁停止，全部标记已读，打开面板并高亮未读事件
5. 未读状态持久化（保存在 `event_history.json` 中），重启后恢复

## 开发

### 前置条件

- .NET 9 SDK
- Node.js 20+
- Inno Setup 6（用于构建安装包）

### 项目结构

```
├── src/
│   ├── HooksNotifier/         # 托盘应用 (WinForms + WebView2)
│   │   ├── TrayMode.cs        # 系统托盘图标和菜单
│   │   ├── MainWindow.cs      # WebView2 仪表盘窗口
│   │   ├── HookConfig.cs      # 读写 settings.json hooks 配置
│   │   ├── IpcService.cs      # 命名管道 IPC 服务端
│   │   ├── ToastService.cs    # WinRT Toast 通知
│   │   ├── IconHelper.cs      # GDI+ 铃铛图标渲染
│   │   ├── EventHistory.cs    # 内存事件历史
│   │   ├── Models.cs          # 共享数据模型
│   │   └── i18n/              # 语言文件 (en, zh)
│   └── NotifyHook/            # 轻量 Hook 处理器
│       └── Program.cs         # Toast + 权限弹窗 + IPC
├── webui/                     # React + shadcn/ui 仪表盘
├── installer/                 # Inno Setup 中文语言包
├── setup.iss                  # Inno Setup 脚本
├── setup.ps1                  # 配置脚本
└── publish.ps1                # 构建辅助脚本
```

## 贡献者

感谢以下人士对本项目的贡献：

- [@wwe1428103707](https://github.com/wwe1428103707) — 项目创建者和维护者

## 许可证

MIT
