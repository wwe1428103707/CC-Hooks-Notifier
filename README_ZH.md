<p align="right">
  <a href="README.md">English</a>
</p>

<div align="center">
  <img src="https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/icon.png" alt="Tray icon" width="48" style="vertical-align: middle; margin-right: 8px;"/>
  <h1 style="display: inline-block; vertical-align: middle;">Claude Code Hooks Notifier</h1>
</div>

<p align="center">
  <b>不再错过 Claude Code 的每一个关键时刻。</b><br>
  <sub>为你的 AI 编程伙伴配上原生 Windows 通知 + QQ 风格托盘提醒。</sub>
</p>

<p align="center">
  <a href="https://github.com/wwe1428103707/CC-Hooks-Notifier/releases"><img src="https://img.shields.io/github/v/release/wwe1428103707/CC-Hooks-Notifier?include_prereleases&label=latest" alt="Release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/wwe1428103707/CC-Hooks-Notifier" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2B-blue" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-9-purple" alt=".NET 9">
</p>

![应用截图](https://raw.githubusercontent.com/wwe1428103707/CC-Hooks-Notifier/master/example.png)

---

## 这是什么？

你正埋头在另一个窗口——写代码、看文档、浏览网页——Claude Code 在后台吭哧吭哧地干活。**你怎么知道它什么时候需要你？**

这就是 **CC Hooks Notifier** 要解决的问题。它安静地待在你的系统托盘里，当 Claude Code 触发 hook 事件的那一刻，它立刻活跃起来：

- **原生 Windows Toast** 滑出来告诉你发生了什么
- **托盘图标闪烁** 像 QQ/微信一样——你不可能错过
- **鼠标悬停** 看有多少条未读通知在等你
- **单击托盘** 打开完整的事件面板

把它想象成 Claude Code 的专属传呼机。它监控 17 种不同的 hook 事件——从"任务完成了，来看看"到"需要你授权"再到"API 报错了，快处理"——不用来回切窗口，一切尽在掌握。

---

## 功能亮点

| 分类 | 亮点 |
|------|------|
| **通知中心** | QQ 风格图标闪烁（出现/消失交替），悬停显示未读计数，单击直达事件日志 |
| **Toast 通知** | 原生 Windows 10/11 通知——不依赖浏览器，终端最小化也能收到 |
| **权限弹窗** | 现代深色主题弹窗，单选按钮卡片选项，支持自由输入，可选择"始终允许"持久化 |
| **事件面板** | WebView2 驱动——实时计数器、带已读/未读筛选的事件日志、Hook 开关控制、可配置最大日志条数 |
| **高 DPI** | PerMonitorV2 感知——在 125%/150%/200% 缩放下渲染清晰 |
| **17 种事件** | PermissionRequest、Notification、StopFailure、PostToolUse、SubagentStart/Stop、TaskCreated/Completed 全覆盖 |
| **优先级分级** | P0（关键，长闪烁）/ P0.5（重要，短闪烁）/ P1（Toast）/ P2（静默计数） |
| **托盘菜单** | 快速查看通知、配置 Hook、切换语言、闪烁开关、开机自启 |
| **双语言** | 英文 & 简体中文——托盘菜单或设置面板随时切换 |
| **持久化** | 事件历史重启不丢失，未读状态存盘保留 |

---

## 架构一览

两个轻量可执行文件，无缝协作：

```
Claude Code hooks
       │
       ▼
┌─────────────────┐      命名管道 IPC       ┌─────────────────────┐
│  hooks-notify   │ ◄──────────────────────► │  hooks-notifier     │
│  (CLI 处理器)    │     JSON 单行传输        │  (托盘进程)          │
│                 │                          │                     │
│  • Toast 弹窗   │                          │  • 系统托盘图标      │
│  • 权限对话框   │                          │  • 闪烁动画          │
│  • 即发即忘     │                          │  • 事件历史          │
│                 │                          │  • WebView2 界面    │
└─────────────────┘                          └─────────────────────┘
```

---

## 快速上手

### 1. 安装

从 [Releases](https://github.com/wwe1428103707/CC-Hooks-Notifier/releases) 下载最新安装包，运行，搞定。安装程序会自动注册所有必要组件。

### 2. 配置

```powershell
.\hooks-notifier.exe --configure-hooks
```

这一条命令自动更新 `~/.claude/settings.json`，让 Claude Code 接入通知系统。

### 3. 完成

托盘图标出现。现在起 Claude Code 的每一个事件都会准确送达你的桌面。

---

## 系统要求

- **Windows 10**（build 17763+）或 **Windows 11**
- [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/)（Windows 11 已预装）
- [Claude Code](https://claude.ai/code)

---

## 通知中心怎么玩

<p align="center">
  <b>事件到达 → 图标闪烁 → 悬停看数量 → 点击看详情 → 一键清空</b>
</p>

1. P0 或 P0.5 事件触发（比如"任务完成"、"需要授权"）
2. 托盘铃铛**闪烁**——出现 / 消失交替——跟 QQ、微信一模一样
3. **悬停**图标：tooltip 显示 *"3 条未读通知"*
4. 右键菜单顶部显示 *"查看通知（3 条未读）"*
5. **单击**托盘图标——闪烁停止，全部标为已读，面板打开并高亮未读事件
6. 未读状态**持久化到磁盘**——重启程序，未读事件还在

---

## 仪表盘

| 标签 | 内容 |
|------|------|
| **仪表盘** | 概览卡片：总数 / 未读 / P0 / Toast / 子代理 / 任务，近期事件列表，Hook 开关控制 |
| **事件日志** | 完整历史表格，已读/未读高亮（琥珀色背景 + 彩色圆点）。筛选：全部 / 未读 / P0 / P0.5 / Toast。一键"全部标为已读" |
| **设置** | 语言切换（EN / 中文）、开机自启、最大日志条数（100–5000）、Hook 路径管理、打开 settings.json |
| **关于** | 版本信息与事件覆盖表 |

---

## Hook 事件全覆盖

| 事件 | 优先级 | 触发场景 |
|------|--------|---------|
| Notification(idle_prompt) | **P0** | Claude 完成一轮响应，等待你的下一个指令 |
| Notification(permission_prompt) | **P0** | Claude 被卡住了，等你批准工具调用 |
| StopFailure | **P0** | API 报错或运行时故障——出事了 |
| Stop | **P0.5** | Claude 完成了一轮响应 |
| TaskCompleted | **P0.5** | 一个被追踪的任务已完成 |
| SessionEnd | **P0.5** | 会话结束（clear / logout / exit） |
| PermissionRequest | P1 | Claude 需要工具权限 |
| PostToolUse(Edit\|Write) | P1 | 文件被编辑或写入 |
| PostToolUseFailure | P1 | 工具调用返回了错误 |
| SubagentStop | P1 | 子代理完成了它的工作 |
| PermissionDenied | P1 | 你拒绝了某个工具请求 |
| SessionStart | P1 | 新会话开始或会话恢复 |
| PostCompact | P1 | 上下文压缩完成 |
| ConfigChange | P1 | 设置被修改 |
| SubagentStart | P2 | 子代理被创建 |
| TaskCreated | P2 | 新任务被创建 |
| PreCompact | P2 | 上下文即将压缩 |

---

## 从源码构建

```powershell
# React UI
cd webui && npm install && npm run build && cd ..

# 轻量 Hook 处理器
dotnet publish src/NotifyHook/NotifyHook.csproj -c Release -o bin --sc false

# 托盘 + 仪表盘
dotnet publish src/HooksNotifier/HooksNotifier.csproj -c Release -o bin --sc false

# （可选）构建安装包
# "%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" setup.iss
```

**开发环境：**.NET 9 SDK、Node.js 20+、Inno Setup 6（用于构建安装包）

---

## 项目结构

```
├── src/
│   ├── HooksNotifier/         # 托盘应用 (WinForms + WebView2)
│   │   ├── TrayMode.cs        # 系统托盘图标、闪烁动画、右键菜单
│   │   ├── MainWindow.cs      # WebView2 仪表盘宿主
│   │   ├── HookConfig.cs      # ~/.claude/settings.json 读写
│   │   ├── IpcService.cs      # 命名管道 IPC 服务端
│   │   ├── ToastService.cs    # WinRT Toast 通知
│   │   ├── IconHelper.cs      # GDI+ 铃铛图标（正常、橙色、透明）
│   │   ├── EventHistory.cs    # 持久化环形缓冲区（含未读状态）
│   │   ├── Models.cs          # 共享数据模型
│   │   └── i18n/              # en.json, zh.json
│   └── NotifyHook/            # 轻量 CLI Hook 处理器
│       └── Program.cs         # Toast + 权限弹窗 + IPC 客户端
├── webui/                     # React 19 + shadcn/ui + Tailwind v4
├── docs/                      # 需求报告、开发路线、接口契约
├── setup.iss                  # Inno Setup 安装脚本
├── setup.ps1                  # 配置辅助脚本
└── publish.ps1                # 构建辅助脚本
```

---

## 贡献者

感谢以下人士对本项目的贡献：

- [@wwe1428103707](https://github.com/wwe1428103707) — 项目创建者和维护者

---

## 许可证

MIT — 自由使用、Fork、贡献。
