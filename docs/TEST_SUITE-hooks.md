# CC Hooks Notifier — 完整 Hook 测试用例

> 版本: v1.0  
> 日期: 2026-05-25  
> 用途: Workflow 测试阶段调用，覆盖全部 17 种 Hook 事件

---

## 测试架构

```
测试输入 (JSON)
  │
  ├──▶ hooks-notify.exe ──┬──▶ stdout (decision JSON)  ← 检查格式
  │                       ├──▶ WinRT Toast              ← 视觉确认
  │                       ├──▶ Permission Dialog        ← 视觉+交互确认
  │                       └──▶ IPC ──▶ hooks-notifier   ← 检查响应
  │                                    │
  │                                    ├──▶ EventHistory.json  ← 检查记录
  │                                    ├──▶ Tooltip unread     ← 检查计数
  │                                    └──▶ Blink animation    ← 视觉确认
```

每个测试用例检查：
1. **输入** — JSON fixture
2. **stdout 输出** — decision 格式正确
3. **IPC 消息** — tray 收到并响应 `{"status":"ok"}`
4. **EventHistory** — 正确记录（Level、EventName、IsRead）
5. **异常情况** — 缺字段、空值、超长文本、Unicode

---

## 0. 测试基础设施

### 0.1 测试辅助脚本

```powershell
# test_runner.ps1 — 发送 JSON 到 hooks-notify.exe 并捕获输出
param(
    [Parameter(Mandatory=$true)] [string]$JsonFile,
    [string]$NotifyPath = "C:\Users\YuFJ\AppData\Local\Programs\ClaudeHooksNotifier\hooks-notify.exe"
)
$json = Get-Content $JsonFile -Raw -Encoding UTF8
$proc = Start-Process -FilePath $NotifyPath -RedirectStandardInput $JsonFile -RedirectStandardOutput "stdout.txt" -NoNewWindow -Wait
Get-Content "stdout.txt"
```

### 0.2 历史记录检查

```powershell
# check_history.ps1 — 检查 event_history.json 最近 N 条
param([int]$LastN = 5)
$path = "C:\Users\YuFJ\AppData\Local\Programs\ClaudeHooksNotifier\event_history.json"
$events = Get-Content $path -Raw | ConvertFrom-Json
$events | Select-Object -Last $LastN | ForEach-Object {
    "$($_.Timestamp) [$($_.Level)] $($_.EventName) isRead=$($_.IsRead)"
}
```

### 0.3 IPC 直连测试

```powershell
# ipc_send.ps1 — 直接发送 IPC 消息到托盘
param([string]$JsonMsg)
$pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", "ClaudeCodeHooks", [System.IO.Pipes.PipeDirection]::InOut)
$pipe.Connect(2000)
$buf = [System.Text.Encoding]::UTF8.GetBytes($JsonMsg + "`n")
$pipe.Write($buf, 0, $buf.Length)
$pipe.Flush()
$reader = New-Object System.IO.StreamReader($pipe)
$reader.ReadLine()
```

---

## 1. PermissionRequest 测试 (P0, P1)

### TC-PERM-001: 基本权限请求 — 单选项

**输入** (test_perm_basic.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "Bash",
  "tool_input": {
    "command": "rm -rf /tmp/build",
    "description": "Clean build artifacts"
  },
  "permission_suggestions": [
    {
      "type": "addRules",
      "behavior": "allow",
      "destination": "user",
      "rules": [{"toolName": "Bash", "ruleContent": "rm -rf /tmp/build"}]
    }
  ]
}
```

**检查项**:
- [ ] 弹窗出现，显示 1 个选项
- [ ] 选项文字清晰可读："rm -rf /tmp/build (Bash)"
- [ ] 单选项自动选中，卡片蓝色高亮
- [ ] 点击 Allow → stdout 包含 `decision.behavior: "allow"`
- [ ] `updatedPermissions` 包含 1 条规则
- [ ] 点击 Deny → stdout 包含 `decision.behavior: "deny"`

---

### TC-PERM-002: 多选项 — 互斥选择

**输入** (test_perm_multi.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "Bash",
  "tool_input": {
    "command": "git push origin main",
    "description": "Push to remote repository"
  },
  "permission_suggestions": [
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "git push origin main"}]},
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "git push --force"}]},
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "git push --set-upstream origin main"}]},
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": ""}]}
  ]
}
```

**检查项**:
- [ ] 弹窗显示 4 个选项，4 张卡片
- [ ] **只能单选** — 选中一个，其他自动取消
- [ ] 第 1 个默认选中
- [ ] 选项 4 有文本框可输入自定义命令
- [ ] 选项 4 未选中时文本框禁用（灰色）
- [ ] "Always allow" 复选框与按钮**不重叠**
- [ ] 选中第 2 个 → 点击 Allow → `updatedPermissions[0].rules[0].ruleContent === "git push --force"`
- [ ] 取消已选（不选任何）→ 选回第 3 个 → 点击 Allow → 输出正确

---

### TC-PERM-003: 自由输入选项

**输入**: 同 TC-PERM-002

**操作**: 选中选项 4 → 在文本框输入 `docker-compose up -d` → 点击 Allow

**检查项**:
- [ ] stdout `updatedPermissions[0].rules[0].ruleContent` 为 `"docker-compose up -d"`
- [ ] 原始 JSON 中空的 `ruleContent` 被替换为输入内容

---

### TC-PERM-004: "Always Allow" 复选框

**输入**: 同 TC-PERM-002

**操作**: 选第 1 个 → 勾选 "Always allow" → 点击 Allow

**检查项**:
- [ ] stdout 中 `updatedPermissions` 包含选中的规则
- [ ] 下次相同工具请求时规则自动生效（由 Claude Code 处理）

---

### TC-PERM-005: 无权限建议

**输入** (test_perm_no_suggestions.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "WebFetch",
  "tool_input": {
    "url": "https://example.com",
    "description": "Fetch a web page"
  }
}
```

**检查项**:
- [ ] 弹窗仍然显示 Allow/Deny 按钮
- [ ] 无选项区域（不显示 "Choose an option"）
- [ ] 无 "Always allow" 复选框
- [ ] 点击 Allow → stdout `decision.behavior: "allow"`，无 `updatedPermissions`

---

### TC-PERM-006: Unicode 和特殊字符

**输入** (test_perm_unicode.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "Bash",
  "tool_input": {
    "command": "echo '你好世界 🚀'",
    "description": "包含 Unicode 和 emoji 的测试"
  },
  "permission_suggestions": [
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "echo '你好世界 🚀'"}]},
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "echo 'café résumé'"}]},
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "Bash", "ruleContent": "curl -X POST -d '{\"key\":\"val\\u0026more\"}' http://api.test"}]}
  ]
}
```

**检查项**:
- [ ] 弹窗正确显示 Unicode 字符（中文、法语、emoji）
- [ ] JSON 转义字符正确还原
- [ ] stdout 中 Unicode 正确输出

---

### TC-PERM-007: 超长命令和工具名

**输入** (test_perm_long.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "VeryLongToolNameThatExceedsNormalLimitsForTestingPurposes",
  "tool_input": {
    "command": "echo " + "x".repeat(500) + "",
    "description": "A ".repeat(200) + "very long description text"
  },
  "permission_suggestions": [
    {"type": "addRules", "behavior": "allow", "destination": "user", "rules": [{"toolName": "VeryLongToolNameThatExceedsNormalLimitsForTestingPurposes", "ruleContent": "echo " + "x".repeat(500) + ""}]}
  ]
}
```

**检查项**:
- [ ] 弹窗不崩溃
- [ ] 长文本被截断或滚动显示
- [ ] 卡片大小适配长文本

---

### TC-PERM-008: setMode / addDirectories 类型建议

**输入** (test_perm_setmode.json):
```json
{
  "hook_event_name": "PermissionRequest",
  "tool_name": "Bash",
  "permission_suggestions": [
    {"type": "setMode", "behavior": "allow", "mode": "acceptEdits"},
    {"type": "addDirectories", "behavior": "allow", "destination": "project", "directories": ["/src", "/tests", "/docs"]}
  ]
}
```

**检查项**:
- [ ] setMode 显示 "Set permission mode to \"acceptEdits\""
- [ ] addDirectories 显示 "Add 3 working directories to whitelist"
- [ ] 单选互斥正常工作

---

## 2. Notification 测试 (P0, P1)

### TC-NOTI-001: idle_prompt (P0 → long blink)

**输入** (test_noti_idle.json):
```json
{
  "hook_event_name": "Notification",
  "hook_event_type": "idle_prompt",
  "message": "Task complete — ready for your input"
}
```

**检查项**:
- [ ] WinRT Toast 弹出："Task complete — ready for your input"
- [ ] IPC 发送 `blinkType: "long"`
- [ ] 托盘图标开始闪烁（出现/消失交替）
- [ ] EventHistory 记录 Level="P0", EventName="Notification"
- [ ] Tooltip 显示 "1 条未读通知"
- [ ] 未读计数 +1

---

### TC-NOTI-002: permission_prompt (P0 → long blink)

**输入** (test_noti_perm_prompt.json):
```json
{
  "hook_event_name": "Notification",
  "hook_event_type": "permission_prompt",
  "title": "Claude needs your permission",
  "message": "Claude is waiting for you to approve a tool call"
}
```

**检查项**:
- [ ] WinRT Toast 显示权限等待信息
- [ ] IPC 发送 `blinkType: "long"`
- [ ] Level="P0"

---

### TC-NOTI-003: auth_success (P1 → no blink)

**输入** (test_noti_auth.json):
```json
{
  "hook_event_name": "Notification",
  "hook_event_type": "auth_success",
  "message": "Authentication successful"
}
```

**检查项**:
- [ ] Toast 弹出不闪烁
- [ ] IPC 发送 `blinkType: "none"`
- [ ] Level="Toast"

---

### TC-NOTI-004: elicitation_dialog (P1)

**输入** (test_noti_elicit.json):
```json
{
  "hook_event_name": "Notification",
  "hook_event_type": "elicitation_dialog",
  "message": "An MCP server needs your input"
}
```

**检查项**:
- [ ] Toast 正常弹出
- [ ] blinkType="none", Level="Toast"

---

## 3. StopFailure 测试 (P0)

### TC-STOPFAIL-001: API 错误带 ErrorDetails

**输入** (test_stopfail_api.json):
```json
{
  "hook_event_name": "StopFailure",
  "error": "API error",
  "error_details": "Overloaded: The server is currently experiencing high load. Please retry in 30 seconds.",
  "reason": "rate_limit"
}
```

**检查项**:
- [ ] Toast 显示错误详情
- [ ] IPC `blinkType: "long"`（P0 关键事件）
- [ ] EventHistory Level="P0"
- [ ] detail 字段包含完整错误信息

---

### TC-STOPFAIL-002: 认证失败

**输入** (test_stopfail_auth.json):
```json
{
  "hook_event_name": "StopFailure",
  "error": "Authentication failed",
  "error_details": "Invalid API key. Please check your credentials.",
  "reason": "auth_error"
}
```

**检查项**:
- [ ] Toast 显示认证失败信息
- [ ] Level="P0", blinkType="long"

---

### TC-STOPFAIL-003: 计费错误

**输入** (test_stopfail_billing.json):
```json
{
  "hook_event_name": "StopFailure",
  "error": "Billing error",
  "error_details": "Your account has insufficient credits.",
  "reason": "billing_error"
}
```

**检查项**:
- [ ] Toast 正确显示
- [ ] Level="P0"

---

## 4. Stop / TaskCompleted / SessionEnd 测试 (P0.5)

### TC-STOP-001: 正常停止

**输入** (test_stop.json):
```json
{
  "hook_event_name": "Stop",
  "hook_event_type": "",
  "last_assistant_message": "I have completed the requested changes. Here is a summary..."
}
```

**检查项**:
- [ ] Toast 弹出
- [ ] IPC `blinkType: "short"`（P0.5）
- [ ] Level="P0.5"
- [ ] detail 包含 last_assistant_message

---

### TC-TASKCOMP-001: 任务完成

**输入** (test_task_completed.json):
```json
{
  "hook_event_name": "TaskCompleted",
  "task_subject": "Fix login authentication bug"
}
```

**检查项**:
- [ ] Toast 显示 "Task completed: Fix login authentication bug"
- [ ] blinkType="short", Level="P0.5"

---

### TC-SESSIONEND-001: 用户退出

**输入** (test_session_end.json):
```json
{
  "hook_event_name": "SessionEnd",
  "hook_event_type": "prompt_input_exit"
}
```

**检查项**:
- [ ] Level="P0.5", blinkType="short"
- [ ] Summary 包含 "Session ended"

---

## 5. Subagent 测试 (P2)

### TC-SUB-001: SubagentStart

**输入** (test_subagent_start.json):
```json
{
  "hook_event_name": "SubagentStart",
  "hook_event_type": "Explore",
  "task_subject": "Find all API endpoint definitions"
}
```

**检查项**:
- [ ] IPC `type: "stateful"`, `blinkType: "none"`
- [ ] 托盘菜单 Subagent 状态更新为 "Subagent: Explore"
- [ ] EventHistory Level="Stateful"（不闪烁、不 Toast）

---

### TC-SUB-002: SubagentStop

**输入** (test_subagent_stop.json):
```json
{
  "hook_event_name": "SubagentStop",
  "hook_event_type": "Explore",
  "last_assistant_message": "Found 12 API endpoint definitions across 5 files."
}
```

**检查项**:
- [ ] 托盘菜单 Subagent 状态重置为 "Subagent: IDLE"
- [ ] Level="Toast"（仅通知，不闪烁）

---

## 6. TaskCreated 测试 (P2)

### TC-TASK-001: 任务创建

**输入** (test_task_created.json):
```json
{
  "hook_event_name": "TaskCreated",
  "hook_event_type": "pending",
  "task_subject": "Refactor authentication middleware"
}
```

**检查项**:
- [ ] 托盘菜单 Task 状态更新
- [ ] Level="Stateful"

---

## 7. PostToolUse 测试 (P1)

### TC-TOOL-001: 文件编辑成功

**输入** (test_posttool_edit.json):
```json
{
  "hook_event_name": "PostToolUse",
  "hook_event_subtype": "Edit",
  "tool_name": "Edit",
  "tool_input": {
    "file_path": "D:\\project\\src\\main.ts",
    "description": "Update imports"
  }
}
```

**检查项**:
- [ ] Toast 显示 "Edited: D:\\project\\src\\main.ts"
- [ ] Level="Toast", blinkType="none"

---

### TC-TOOL-002: Bash 工具失败

**输入** (test_posttool_fail.json):
```json
{
  "hook_event_name": "PostToolUseFailure",
  "hook_event_subtype": "Bash",
  "tool_name": "Bash",
  "error": "Exit code 1",
  "error_details": "Command not found: invalid-cmd"
}
```

**检查项**:
- [ ] Toast 显示 "Tool failed: Bash"
- [ ] IPC body 包含错误详情
- [ ] Level="Toast"

---

## 8. 组合/复杂场景测试

### TC-COMPLEX-001: 快速连续事件（不丢失）

**输入序列**:
1. SessionStart
2. SubagentStart × 3
3. TaskCreated × 2
4. PermissionRequest（多选项）
5. Notification(idle_prompt)

**检查项**:
- [ ] 所有事件记录在 EventHistory 中，无丢失
- [ ] 每个事件 Level 正确
- [ ] 批量事件后 tooltip 未读计数正确（仅 P0+P0.5 计数）

---

### TC-COMPLEX-002: 闪烁开关测试

**前提**: 在托盘菜单中关闭 "Blink on notification"

**输入**: 发送 StopFailure（P0, long blink）

**检查项**:
- [ ] Toast 正常弹出
- [ ] EventHistory 正常记录
- [ ] **托盘图标不闪烁**
- [ ] 重新开启闪烁开关后，下次事件正常闪烁

---

### TC-COMPLEX-003: 中英文切换一致性

**操作**:
1. 切换语言为 中文 → 发送 PermissionRequest → 检查弹窗中文显示
2. 切换语言为 English → 发送 PermissionRequest → 检查弹窗英文显示
3. 切换语言 → 检查托盘 tooltip 语言一致
4. 切换语言 → 检查托盘菜单语言一致

**检查项**:
- [ ] 弹窗标题、按钮、标签随语言切换
- [ ] Tooltip 格式随语言切换（"3 条未读通知" vs "3 unread notifications"）
- [ ] 菜单项随语言切换

---

### TC-COMPLEX-004: 最大日志条数裁剪

**前提**: 设置最大日志条数为 200

**操作**: 发送 250 个 Notification 事件

**检查项**:
- [ ] EventHistory 只保留最近 200 条
- [ ] 旧的 50 条被裁剪
- [ ] 未读计数统计正确（仅在保留的 200 条中计算）

---

### TC-COMPLEX-005: 重启持久化

**操作**:
1. 发送 5 个事件（包含 P0+P0.5+Toast）
2. 退出 hooks-notifier.exe
3. 重新启动 hooks-notifier.exe --tray
4. 检查

**检查项**:
- [ ] EventHistory 恢复所有事件
- [ ] IsRead 状态正确恢复（之前标记已读的保持已读）
- [ ] 未读计数正确
- [ ] Tooltip 显示正确的未读数
- [ ] 最大日志条数设置恢复

---

### TC-COMPLEX-006: 并发 IPC 消息

**操作**: 同时从 5 个 PowerShell 进程发送 IPC 消息到托盘

**检查项**:
- [ ] 所有 5 条消息收到 `{"status":"ok"}`
- [ ] EventHistory 记录 5 条，无丢失
- [ ] 托盘未崩溃

---

## 9. 边界条件测试

### TC-EDGE-001: 空 JSON 输入

**输入**:
```json
{}
```
**检查项**:
- [ ] hooks-notify.exe 退出码 0
- [ ] 无崩溃

---

### TC-EDGE-002: stdin 为空

**操作**: 不发送任何输入

**检查项**:
- [ ] 退出码 0
- [ ] 无 WinForms 弹窗（因为无 PermissionRequest）

---

### TC-EDGE-003: 不可解析的 JSON

**输入**: `{invalid json!!!`

**检查项**:
- [ ] 退出码 1
- [ ] 无崩溃

---

### TC-EDGE-004: 未知 hook_event_name

**输入**:
```json
{
  "hook_event_name": "UnknownEventType",
  "tool_name": "TestTool"
}
```

**检查项**:
- [ ] 退出码 0
- [ ] 无崩溃（default case 处理）

---

### TC-EDGE-005: blink 关闭时收到 P0

**前提**: blink 已关闭

**输入**: StopFailure

**检查项**:
- [ ] Toast 正常
- [ ] EventHistory 正常记录
- [ ] 图标不闪烁
- [ ] Tooltip 仍更新未读计数

---

### TC-EDGE-006: 999+ 未读计数上限

**操作**: 连续发送 1100 个 P0 事件

**检查项**:
- [ ] Tooltip 显示 "999+ 条未读通知"（不显示 1100）
- [ ] 右键菜单显示 "查看通知（999+ 条未读）"

---

### TC-EDGE-007: detail 字段为空字符串/null

**输入** (StopFailure 不带 error_details):
```json
{
  "hook_event_name": "StopFailure",
  "error": "Some error"
}
```

**检查项**:
- [ ] EventHistory.detail 为空字符串
- [ ] IPC body 不崩溃

---

## 10. BlinkType 映射正确性测试

### TC-BLINK-001: 所有事件的 BlinkType 映射

测试每个 Hook 事件类型的 BlinkType：

| HookEventName | SubType | 期望 BlinkType | 期望 Level |
|---------------|---------|---------------|-----------|
| StopFailure | — | `"long"` | P0 |
| Notification | idle_prompt | `"long"` | P0 |
| Notification | permission_prompt | `"long"` | P0 |
| PermissionRequest | — | `"long"` | P0 |
| Stop | — | `"short"` | P0.5 |
| TaskCompleted | — | `"short"` | P0.5 |
| SessionEnd | — | `"short"` | P0.5 |
| All others | — | `"none"` | Toast/Stateful |

**方法**: 对每种事件发送输入 → 检查 IPC 消息中的 `blinkType` 字段 → 检查 EventHistory 中 `Level` 字段

---

## 11. 性能测试

### TC-PERF-001: 500 事件批量写入

**操作**: 快速发送 500 个 Notification 事件

**检查项**:
- [ ] 所有事件写入 event_history.json
- [ ] 写入时间 < 5 秒
- [ ] 未读计数计算正确

---

### TC-PERF-002: WebView2 面板打开延迟

**前提**: 已有 500 条事件历史

**操作**: 单击托盘图标打开面板

**检查项**:
- [ ] 面板在 3 秒内打开
- [ ] 事件列表加载完整

---

## 附录 A: 测试清单（快速检查）

```
□ TC-PERM-001  单选项权限请求
□ TC-PERM-002  4 选项互斥选择
□ TC-PERM-003  自由输入自定义命令
□ TC-PERM-005  无建议的权限请求
□ TC-PERM-006  Unicode 特殊字符
□ TC-PERM-008  setMode / addDirectories
□ TC-NOTI-001  idle_prompt (P0 闪烁)
□ TC-NOTI-003  auth_success (不闪烁)
□ TC-STOPFAIL-001  API 错误
□ TC-STOP-001  正常停止
□ TC-TASKCOMP-001  任务完成
□ TC-SESSIONEND-001  会话结束
□ TC-SUB-001  Subagent 启动
□ TC-SUB-002  Subagent 停止
□ TC-TASK-001  任务创建
□ TC-TOOL-001  文件编辑
□ TC-TOOL-002  工具失败
□ TC-COMPLEX-001  快速连续事件
□ TC-COMPLEX-002  闪烁开关
□ TC-COMPLEX-003  中英文切换
□ TC-COMPLEX-005  重启持久化
□ TC-BLINK-001  BlinkType 映射
□ TC-EDGE-001~007  所有边界条件
□ TC-PERF-001  500 事件性能
```

---

## 附录 B: 生成测试 JSON 夹具的 PowerShell

```powershell
# generate_fixtures.ps1 — 生成所有测试 JSON 文件到 test_fixtures/ 目录

$fixtures = @{
    "test_perm_basic" = @{
        hook_event_name = "PermissionRequest"
        tool_name = "Bash"
        tool_input = @{ command = "rm -rf /tmp/build"; description = "Clean build artifacts" }
        permission_suggestions = @(
            @{ type = "addRules"; behavior = "allow"; destination = "user"
               rules = @(@{ toolName = "Bash"; ruleContent = "rm -rf /tmp/build" }) }
        )
    }
    "test_perm_multi" = @{
        hook_event_name = "PermissionRequest"
        tool_name = "Bash"
        tool_input = @{ command = "git push origin main"; description = "Push to remote" }
        permission_suggestions = @(
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "git push origin main" }) },
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "git push --force" }) },
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "git push --set-upstream origin main" }) },
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "" }) }
        )
    }
    "test_noti_idle" = @{
        hook_event_name = "Notification"
        hook_event_type = "idle_prompt"
        message = "Task complete — ready for your input"
    }
    "test_stopfail_api" = @{
        hook_event_name = "StopFailure"
        error = "API error"
        error_details = "Overloaded: The server is currently experiencing high load."
        reason = "rate_limit"
    }
    "test_stop" = @{
        hook_event_name = "Stop"
        last_assistant_message = "I have completed the requested changes."
    }
    "test_task_completed" = @{
        hook_event_name = "TaskCompleted"
        task_subject = "Fix login authentication bug"
    }
    "test_session_end" = @{
        hook_event_name = "SessionEnd"
        hook_event_type = "prompt_input_exit"
    }
    "test_subagent_start" = @{
        hook_event_name = "SubagentStart"
        hook_event_type = "Explore"
    }
    "test_subagent_stop" = @{
        hook_event_name = "SubagentStop"
        hook_event_type = "Explore"
        last_assistant_message = "Found 12 API endpoint definitions."
    }
    "test_task_created" = @{
        hook_event_name = "TaskCreated"
        task_subject = "Refactor authentication middleware"
    }
    "test_posttool_edit" = @{
        hook_event_name = "PostToolUse"
        hook_event_subtype = "Edit"
        tool_name = "Edit"
        tool_input = @{ file_path = "D:\project\src\main.ts" }
    }
    "test_posttool_fail" = @{
        hook_event_name = "PostToolUseFailure"
        hook_event_subtype = "Bash"
        tool_name = "Bash"
        error = "Exit code 1"
        error_details = "Command not found: invalid-cmd"
    }
    "test_perm_unicode" = @{
        hook_event_name = "PermissionRequest"
        tool_name = "Bash"
        tool_input = @{ command = "echo '你好世界'"; description = "Unicode test" }
        permission_suggestions = @(
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "echo '你好世界 🚀'" }) },
            @{ type = "addRules"; behavior = "allow"; destination = "user"; rules = @(@{ toolName = "Bash"; ruleContent = "echo 'café'" }) }
        )
    }
    "test_edge_empty" = @{}
    "test_edge_unknown" = @{ hook_event_name = "UnknownEventType" }
}

$outDir = "D:\CC Hooks Notifier\test_fixtures"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

foreach ($name in $fixtures.Keys) {
    $fixtures[$name] | ConvertTo-Json -Depth 5 | Out-File "$outDir\$name.json" -Encoding UTF8
    Write-Host "Generated: $name.json"
}
Write-Host "Done. $($fixtures.Count) fixtures in $outDir"
```
