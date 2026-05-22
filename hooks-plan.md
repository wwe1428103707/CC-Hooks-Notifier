# Hooks Notifier — 功能扩展需求方案

## 现状
当前只处理了 2 个 hook 事件：
| 事件 | Matcher | 行为 |
|------|---------|------|
| `PermissionRequest` | `""` (all) | Toast + WinForms 对话框 |
| `Notification` | `idle_prompt` | Toast + 托盘闪烁 |

## 目标
为更多的 hook 事件适配通知/功能，覆盖 Claude Code 全生命周期。

## 设计原则
- **不阻塞**：所有 hook 处理都不应影响 Claude Code 的正常流程
- **通知分级**：根据事件重要性用不同方式提醒用户
- **可配置**：用户可以通过托盘菜单选择开启/关闭特定通知

## 通知分级

| 级别 | 行为 | 适用场景 |
|------|------|---------|
| **🔔 P0 闪烁** | 长闪烁 10s (20 ticks @ 500ms) + Toast | 紧急、需要用户立即处理的事件 |
| **🔔 P0.5 闪烁** | 短闪烁 5s (10 ticks @ 500ms) + Toast | 重要里程碑事件，柔和提醒 |
| **📢 Toast** | 仅弹出 Toast 通知 | 信息性通知 |
| **🟢 状态** | 仅更新托盘菜单状态文字 | 低优先级状态变更 |

### P0 vs P0.5 区别

| 特性 | P0 🔔 | P0.5 🔔 |
|------|-------|---------|
| 闪烁时长 | 10 秒 (20 ticks) | 5 秒 (10 ticks) |
| 闪烁速度 | 500ms 间隔 | 500ms 间隔 |
| Toast 标题前缀 | `"🚨 "` | `"✅ "` |
| 适用场景 | 需立即处理（权限、错误） | 重要里程碑（完成、结束） |

## 新增功能列表

### P0 — 核心功能（必须实现）

| # | Hook 事件 | Matcher | 通知级别 | 说明 |
|---|-----------|---------|---------|------|
| 1 | `Notification` | `permission_prompt` | 🔔 闪烁 | Claude 等待权限批准 |
| 2 | `Notification` | `auth_success` | 📢 Toast | 认证成功 |
| 3 | `Notification` | `elicitation_dialog` | 📢 Toast | MCP 表单弹出 |
| 4 | `Notification` | `elicitation_complete` | 📢 Toast | MCP 表单完成 |
| 5 | `StopFailure` | `rate_limit`, `server_error`, `authentication_failed` | 🔔 闪烁 | API 错误告警 |
| 6 | `PermissionDenied` | `""` (all) | 📢 Toast | 工具调用被拒绝 |

### P0.5 — 重要里程碑（短闪烁 + Toast）

| # | Hook 事件 | Matcher | 通知级别 | 说明 |
|---|-----------|---------|---------|------|
| 7 | `Stop` | `""` (all) | 🔔 P0.5 闪烁 | Claude 完成本轮响应 |
| 8 | `TaskCompleted` | N/A | 🔔 P0.5 闪烁 | 任务标记为完成 |
| 9 | `SessionEnd` | `clear`, `logout`, `prompt_input_exit` | 🔔 P0.5 闪烁 | 会话结束 |

### P1 — 进阶功能

| # | Hook 事件 | Matcher | 通知级别 | 说明 |
|---|-----------|---------|---------|------|
| 10 | `PostToolUse` | `Edit\|Write` | 📢 Toast | 文件被编辑/写入时通知 |
| 11 | `PostToolUseFailure` | `Bash\|Edit` | 📢 Toast | 工具调用失败告警 |
| 12 | `SubagentStart` | `""` (all) | 🟢 状态 | 子代理启动 |
| 13 | `SubagentStop` | `""` (all) | 📢 Toast | 子代理完成 |
| 14 | `TaskCreated` | N/A | 🟢 状态 | 任务创建 |

### P2 — 扩展功能

| # | Hook 事件 | Matcher | 通知级别 | 说明 |
|---|-----------|---------|---------|------|
| 15 | `SessionStart` | `startup`, `resume` | 📢 Toast | 会话开始/恢复 |
| 16 | `PreCompact` | `auto` | 🟢 状态 | 上下文压缩开始 |
| 17 | `PostCompact` | `""` (all) | 📢 Toast | 上下文压缩完成 |
| 18 | `ConfigChange` | `""` (all) | 📢 Toast | 配置文件被外部修改 |

## 技术实现方案

### 1. 注册更多 Hook 事件

更新 `setup.ps1` 中的 hooks 配置，添加更多事件注册：

```json
{
  "hooks": {
    "Notification": [
      { "matcher": "idle_prompt",     "hooks": [...] },
      { "matcher": "permission_prompt","hooks": [...] },
      { "matcher": "auth_success",     "hooks": [...] },
      { "matcher": "elicitation_*",    "hooks": [...] }
    ],
    "StopFailure": [
      { "matcher": "", "hooks": [...] }
    ],
    "PermissionDenied": [
      { "matcher": "", "hooks": [...] }
    ],
    "PostToolUse": [
      { "matcher": "Edit|Write", "hooks": [...] }
    ],
    "PostToolUseFailure": [
      { "matcher": "Bash|Edit", "hooks": [...] }
    ]
  }
}
```

所有事件都指向同一个 `hooks-notifier.exe`（无参数默认 --hook 模式），由程序内部根据 `hook_event_name` 分发。

### 2. 更新 HookMode.cs

扩展事件分发逻辑：

```csharp
switch (data.HookEventName)
{
    case "Notification":          → HandleNotification(data)     ← 已有
    case "PermissionRequest":     → HandlePermissionRequest(data) ← 已有
    case "PermissionDenied":      → HandlePermissionDenied(data)  ← 新增
    case "StopFailure":           → HandleStopFailure(data)       ← 新增
    case "PostToolUse":           → HandlePostToolUse(data)       ← 新增
    case "PostToolUseFailure":    → HandlePostToolUseFailure(data)← 新增
    case "SubagentStart":         → HandleSubagentStart(data)     ← 新增
    case "SubagentStop":          → HandleSubagentStop(data)      ← 新增
    case "TaskCreated":           → HandleTaskCreated(data)       ← 新增
    case "TaskCompleted":         → HandleTaskCompleted(data)     ← 新增
    default:                       → HandleDefault(data)          ← 已有
}
```

### 3. 更新 TrayMode.cs — 托盘状态显示

在托盘菜单中增加状态指示区域：

```
Hooks Notifier — running
──────────────────────────
🔔 3 notifications since last check
Subagent: IDLE
Task: IDLE
──────────────────────────
Configure Hooks
Update Hook Path
Open at Login
──────────────────────────
Exit
```

### 4. 新增 IPC 消息类型

扩展 IPC 协议以支持更多消息类型：

```json
// 当前: "type": "toast" → Toast + 闪烁
// 新增:
// "type": "toast"     → Toast + 可选闪烁
// "type": "stateful"  → 更新托盘菜单状态文字
// "type": "batch_end" → 批量工具调用完成总结
```

## 实施步骤

### Step 1: 扩展 Notification matchers
- 修改 `setup.ps1`，注册更多 Notification matchers
- 修改 `HookMode.cs`，显示不同的通知内容

### Step 2: 添加 StopFailure 支持
- 注册 StopFailure hook
- 实现错误类型分类显示

### Step 3: 添加 PermissionDenied 支持
- 注册 PermissionDenied hook
- 实现被拒绝工具的 Toast 通知

### Step 4: 添加 PostToolUse / PostToolUseFailure 支持
- 注册相关 hook
- 实现文件变更通知

### Step 5: 添加 Subagent/Task 生命周期通知
- 注册 SubagentStart/Stop
- 注册 TaskCreated/Completed
- 更新托盘菜单状态

### Step 6: 托盘菜单状态更新
- 实现实时状态显示
- 添加通知计数
