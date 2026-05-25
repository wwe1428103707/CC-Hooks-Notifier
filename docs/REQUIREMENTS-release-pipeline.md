# 开发到发布流水线 — 需求报告

> 版本: v1.0
> 日期: 2026-05-25
> 状态: 待评审

---

## 1. 需求概述

为 CC Hooks Notifier 建立正式化、可审计的开发到发布流水线。当前构建和发布操作分散在 `CLAUDE.md`、`publish.ps1`、`setup.ps1` 三处，缺乏统一编排、无自动化质量门禁、无变更日志、无 CI/CD 配置。这导致文件遗漏、安装包损坏、版本不一致等风险无法被提前发现。

### 当前行为 vs 目标行为

| 维度 | 当前 | 目标 |
|------|------|------|
| 构建流程 | 分散在三处：CLAUDE.md 手动命令、publish.ps1（只构建 HooksNotifier）、setup.ps1 | 单一入口脚本 `scripts/release.ps1`，按顺序编排完整构建 |
| 版本管理 | 5 个文件手动逐一修改，无自动化验证 | 统一版本戳写 + 自动化一致性检查 |
| 构建前置检查 | 无 | 工作区干净检查、依赖工具存在检查、所有制品文件存在检查 |
| 安装包验证 | 无（ISCC 完成后即认为成功） | 安装后自动冒烟测试：启动 tray、IPC ping、进程存活检查 |
| 变更日志 | 无（仅 git log） | CHANGELOG.md 按 Conventional Commits 自动生成 |
| 发布标签 | 无 | 版本提交后自动创建 git tag |
| CI/CD | 无 | GitHub Actions 配置文件，每次 push 自动构建和 lint |

---

## 2. 功能需求

### 2.1 统一发布编排 (Release Orchestration)

**FR-1.1** 提供 `scripts/release.ps1` 作为发布唯一入口。该脚本按以下阶段顺序执行，任何阶段失败则终止：

```
阶段 0: 前置检查
阶段 1: 版本戳写
阶段 2: 全量构建
阶段 3: 制品验证
阶段 4: 安装包构建
阶段 5: 冒烟测试
阶段 6: 变更日志更新
阶段 7: 提交与标签（可选）
```

**FR-1.2** 支持命令行参数：
- `-Version "x.y.z"` — 指定发布版本（必填）
- `-SkipSmoke` — 跳过冒烟测试（调试用）
- `-Force` — 工作区有未提交更改时仍继续
- `-NoCommit` — 不自动提交和打标签（手动模式）
- `-Config "Release"` — 构建配置，默认 Release
- `-WhatIf` — 只打印将要执行的操作，不实际执行

**FR-1.3** 脚本应在执行每条关键操作之前输出 `[阶段编号/总阶段数] 操作描述` 格式的日志，方便定位失败位置。

**FR-1.4** 每个阶段失败时输出清晰的错误消息，包含建议的修复步骤。

### 2.2 前置检查 (Preflight Checks)

**FR-2.1** 工作区干净检查：执行 `git status --porcelain`，如果输出非空且未传 `-Force`，则终止并提示用户提交或暂存更改。

**FR-2.2** 工具存在检查：验证以下工具在 PATH 中可访问，缺失任何一个则终止并提示安装：

| 工具 | 检查命令 | 用途 |
|------|---------|------|
| dotnet | `Get-Command dotnet` | 构建 .NET 项目 |
| node | `Get-Command node` | 构建 WebUI |
| npm | `Get-Command npm` | 安装 WebUI 依赖 |
| ISCC | `Get-Command ISCC.exe` | 构建安装包 |
| git | `Get-Command git` | 版本管理和标签 |

**FR-2.3** ISCC 降级处理：如果 `ISCC.exe` 不在 PATH 中，尝试以下备选路径并按顺序查找：
- `$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe`
- `$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe`
- `$env:ProgramFiles\Inno Setup 6\ISCC.exe`

**FR-2.4** 上次标签检查：如果已有 git tag 匹配目标版本（如 `v1.12.0`），则警告用户版本重复，除非传 `-Force`。

### 2.3 版本管理 (Version Stamping)

**FR-3.1** 版本写操作：将指定版本号写入以下 5 个文件。脚本应使用精确字符串匹配替换，确保不误改无关内容。

| 文件 | 匹配模式 | 替换示例 |
|------|---------|---------|
| `setup.iss` | `#define MyAppVersion "x.y.z"` | `#define MyAppVersion "1.13.0"` |
| `.claude-plugin/plugin.json` | `"version": "x.y.z"` | `"version": "1.13.0"` |
| `src/HooksNotifier/TrayMode.cs` | `I18n.Get("about.version", "x.y.z")` | `I18n.Get("about.version", "1.13.0")` |
| `webui/src/App.tsx` | `t("about.version", "x.y.z")` | `t("about.version", "1.13.0")` |
| `webui/src/i18n.ts` | `"header.version": "vx.y.z"`（en 行） | `"header.version": "v1.13.0"` |
| `webui/src/i18n.ts` | `"header.version": "vx.y.z"`（zh 行） | `"header.version": "v1.13.0"` |

**FR-3.2** 一致性验证：版本写完成后，执行 `grep -rn` 扫描上述 5 个文件，确认新版本号出现且旧版本号不再出现。如果验证失败，终止并列出不一致的文件。

**FR-3.3** 陈旧版本扫描：执行 `grep -rn` 检查 `--exclude-dir=node_modules --exclude-dir=obj --exclude-dir=bin` 范围内是否有其他 `.cs` / `.ts` / `.tsx` 文件仍引用旧版本号。如有，输出警告列表（不终止）。

### 2.4 全量构建 (Full Build)

**FR-4.1** 严格按以下顺序构建三个组件，任一失败则终止：

| 顺序 | 组件 | 命令 | 输出目录 |
|------|------|------|---------|
| 1 | WebUI | `cd webui && npm install && npm run build` | `webui/dist/` |
| 2 | NotifyHook | `dotnet publish src/NotifyHook/NotifyHook.csproj -c Release -o bin --self-contained false` | `bin/` |
| 3 | HooksNotifier | `dotnet publish src/HooksNotifier/HooksNotifier.csproj -c Release -o bin --self-contained false` | `bin/` |

**FR-4.2** WebUI 构建前自动执行 `npm ci`（若存在 `package-lock.json`）或 `npm install`，确保依赖可复现。

**FR-4.3** 每个 dotnet publish 使用 `--nologo` 参数减少输出噪音，但保留错误信息。

**FR-4.4** 构建过程中记录每个步骤的耗时（Wall Clock Time），方便后续优化。

### 2.5 制品验证 (Artifact Verification)

**FR-5.1** 解析 `setup.iss` 的 `[Files]` 段，提取所有 `Source:` 路径。对每个路径做存在性检查：

```powershell
# 示例：setup.iss 中的条目
Source: "bin\HooksNotifier.exe"; DestDir: "{app}"; Flags: ignoreversion
# 验证: Test-Path "bin\HooksNotifier.exe" → 必须为 $true
```

**FR-5.2** 如果任何制品文件缺失，输出缺失文件列表，建议重新执行失败的构建步骤，终止。

**FR-5.3** 验证 `bin/` 目录下的 .exe 文件不是 0 字节，且文件版本号包含目标版本号（通过 `(Get-Item file).VersionInfo.FileVersion` 检查）。

### 2.6 安装包构建 (Installer Build)

**FR-6.1** 执行 `& "ISCC.exe" "setup.iss"` 构建安装包。

**FR-6.2** 验证安装包已生成：检查 `ClaudeCodeHooksNotifier-Setup-x.y.z.exe` 存在于项目根目录。

**FR-6.3** 验证安装包数字签名：如果已配置签名证书，自动执行 `signtool sign`。若无证书，输出警告（不终止）。

### 2.7 冒烟测试 (Smoke Test)

**FR-7.1** `scripts/verify-release.ps1` 提供以下冒烟测试流程：

```
步骤 1: 启动 hooks-notifier.exe --tray（后台进程）
步骤 2: 等待 2 秒，确认进程存活（无立即崩溃）
步骤 3: 发送 IPC ping 消息（通过命名管道）
步骤 4: 等待 1 秒，检查进程仍存活
步骤 5: 发送 IPC 测试通知消息
步骤 6: 等待 500ms，检查进程仍存活
步骤 7: 终止 hooks-notifier.exe 进程
步骤 8: 报告测试结果（通过/失败 + 各步骤耗时）
```

**FR-7.2** IPC ping 协议格式（阶段 3 发送）：

```json
{"Protocol":1,"Type":"ping","Timestamp":"2026-05-25T12:00:00Z"}
```

**FR-7.3** 进程无响应判定：如果 `hooks-notifier.exe --tray` 启动后在 5 秒内退出（退出代码非零），判定为冒烟测试失败，输出进程退出代码和最后 20 行 stdout/stderr。

**FR-7.4** 冒烟测试失败后，提供诊断建议：
- 检查 `bin/HooksNotifier.exe` 是否可独立运行
- 检查依赖的 .NET 运行时是否安装
- 检查命名管道是否被其他进程占用

### 2.8 变更日志 (Changelog)

**FR-8.1** 在项目根目录创建 `CHANGELOG.md`，格式遵循 [Keep a Changelog](https://keepachangelog.com/) + [SemVer](https://semver.org/)。

**FR-8.2** `scripts/release.ps1` 提供两种变更日志更新模式：

**模式 A — 自动生成（推荐）：**
```powershell
# 解析 git log 从上一个 tag 到 HEAD
# 按 Conventional Commits 前缀分类：
#   feat: → Added
#   fix:  → Fixed
#   docs: → Changed（文档类）
#   refactor: → Changed
#   chore: → Changed
#   perf:  → Changed
# 未知前缀 → 归入 Other
# 排除: 包含 "bump version" 或 "release:" 的提交
```

**模式 B — 手动编辑模式：**
- 脚本输出自上一个 tag 以来的所有 commit 列表（包含 hash + 消息）
- 提示用户手动编辑 `CHANGELOG.md`，确认后再继续

**FR-8.3** 变更日志条目格式：

```markdown
## [1.13.0] - 2026-05-25

### Added
- 统一发布编排脚本 scripts/release.ps1
- 冒烟测试脚本 scripts/verify-release.ps1
- CHANGELOG.md 文件

### Fixed
- publish.ps1 未构建 NotifyHook 和 webui 的问题

### Changed
- CLAUDE.md 构建指南改为引用 release.ps1
```

### 2.9 提交与标签 (Commit & Tag)

**FR-9.1** 当未传 `-NoCommit` 时，脚本自动执行：

```bash
git add -A
git commit -m "chore: bump version to x.y.z and rebuild installer"
git tag -a "vx.y.z" -m "release: vx.y.z"
```

**FR-9.2** 提交和标签完成后，输出摘要：

```
=== Release v1.13.0 Complete ===
Commit:    a1b2c3d4 (chore: bump version to 1.13.0 and rebuild installer)
Tag:       v1.13.0
Installer: ClaudeCodeHooksNotifier-Setup-1.13.0.exe
Smoke:     PASS (all 8 steps)
Duration:  2m 34s

Next step: git push origin master --tags
```

**FR-9.3** 工作流结束时输出"下一步"提示，建议用户执行 `git push` 发布。

### 2.10 CI/CD 集成 (CI/CD Pipeline)

**FR-10.1** 创建 `.github/workflows/build.yml`，配置 GitHub Actions：

| 触发器 | 工作流 |
|--------|--------|
| `push: branches: [master]` | 完整构建 + lint + 制品上传 |
| `pull_request: branches: [master]` | 构建 + lint（不上传制品） |
| `tags: v*` | 构建 + lint + 制品上传 + Release 创建 |

**FR-10.2** CI 工作流步骤：
1. Checkout 代码
2. 安装 .NET SDK (8.0)
3. 安装 Node.js (20.x)
4. 构建 WebUI (`npm ci && npm run build`)
5. 构建 NotifyHook (`dotnet publish`)
6. 构建 HooksNotifier (`dotnet publish`)
7. 制品验证（检查 setup.iss 引用的文件）
8. Lint 检查（`dotnet format --verify-no-changes`，如有）
9. 上传构建制品 (`actions/upload-artifact`)

**FR-10.3** 当 push tag v* 时，额外创建 GitHub Release，附件为安装包 EXE。

---

## 3. 数据模型变更

### 3.1 ReleaseInfo（新增文件 `scripts/ReleaseInfo.ps1`）

```powershell
# 新增：Release 信息对象（PowerShell 类）
class ReleaseInfo {
    [string] $Version            # "1.13.0"
    [string] $Tag                # "v1.13.0"
    [string] $PreviousTag        # "v1.12.0"（从 git describe --tags --abbrev=0 获取）
    [string[]] $Commits          # 自上一个 tag 以来的 commit hashes
    [hashtable] $ClassifiedCommits  # @{ "feat" = @(); "fix" = @(); ... }
    [TimeSpan] $Duration         # 总耗时
    [bool] $SmokeTestPassed      # 冒烟测试结果
}

# 使用示例
$info = [ReleaseInfo]::new()
$info.Version = "1.13.0"
$info.PreviousTag = "v1.12.0"
```

### 3.2 构建状态文件（新增 `build-state.json`，仅用于 CI，不提交到仓库）

```json
{
  "version": "1.13.0",
  "timestamp": "2026-05-25T12:00:00Z",
  "stages": {
    "preflight": { "status": "pass", "duration_ms": 312 },
    "version_stamp": { "status": "pass", "duration_ms": 45 },
    "build": { "status": "pass", "duration_ms": 82300 },
    "artifact_verify": { "status": "pass", "duration_ms": 120 },
    "installer": { "status": "pass", "duration_ms": 5700 },
    "smoke_test": { "status": "pass", "duration_ms": 3100 },
    "changelog": { "status": "pass", "duration_ms": 200 },
    "commit_tag": { "status": "pass", "duration_ms": 800 }
  },
  "total_duration_ms": 92577,
  "installer_path": "ClaudeCodeHooksNotifier-Setup-1.13.0.exe",
  "smoke_test_detail": {
    "process_start": "pass",
    "ipc_ping": "pass",
    "ipc_notify": "pass",
    "process_terminate": "pass"
  }
}
```

### 3.3 无 C#/TypeScript 数据模型变更

本次需求全部为工具链变更，不涉及 `EventEntry`、`IpcMessage` 等运行时模型。

---

## 4. IPC 协议变更

### 4.1 新增 IPC Ping 消息类型

当前 IPC 消息类型包括 `blink`、`state_sync` 等。为冒烟测试目的，新增轻量 Ping/Pong 消息。

**Request（冒烟测试 → tray 进程）：**

```json
{
  "Protocol": 1,
  "Type": "ping",
  "Timestamp": "2026-05-25T12:00:00Z"
}
```

**Response（tray 进程 → 冒烟测试，通过命名管道回写）：**

```json
{
  "Protocol": 1,
  "Type": "pong",
  "Timestamp": "2026-05-25T12:00:00.123Z",
  "ServerVersion": "1.13.0",
  "ProcessUptimeMs": 23400
}
```

### 4.2 TrayMode 变更

在 `TrayMode.cs` 的 `OnIpcMessage` 中新增对 `Type == "ping"` 的分支处理：

```csharp
// 当前：switch(message.Type) 中处理 blink / state_sync
// 新增 case:
case "ping":
    var pong = new
    {
        Protocol = 1,
        Type = "pong",
        Timestamp = DateTime.UtcNow.ToString("o"),
        ServerVersion = I18n.Get("about.version", "1.12.0"),
        ProcessUptimeMs = (DateTime.Now - processStartTime).TotalMilliseconds
    };
    await writer.WriteLineAsync(JsonSerializer.Serialize(pong));
    break;
```

### 4.3 对现有协议的影响

- Ping/Pong 是轻量级心跳，不改变现有消息处理流程
- 仅冒烟测试使用，不影响运行时行为
- 无需修改 EventHistory、MainWindow 等现有模块

---

## 5. UI/UX 设计规格

### 5.1 控制台输出 — release.ps1 运行界面

```
=== CC Hooks Notifier Release Pipeline ===
Version: 1.13.0
Working dir: D:\CC Hooks Notifier

[1/8] Preflight checks .........................
  ✓ Working tree is clean
  ✓ dotnet found (8.0.304)
  ✓ node found (20.12.0)
  ✓ npm found (10.5.0)
  ✓ ISCC found (Inno Setup 6.3.2)
  ✓ git found (2.45.0)

[2/8] Version stamping ........................
  ✓ setup.iss → #define MyAppVersion "1.13.0"
  ✓ plugin.json → "version": "1.13.0"
  ✓ TrayMode.cs → about.version "1.13.0"
  ✓ App.tsx → about.version "1.13.0"
  ✓ i18n.ts (en) → header.version "v1.13.0"
  ✓ i18n.ts (zh) → header.version "v1.13.0"
  ✓ Consistency check passed (no stale versions)

[3/8] Building webui .........................
  ✓ npm ci completed (12.3s)
  ✓ npm run build completed (8.1s)

[4/8] Building NotifyHook .....................
  ✓ dotnet publish completed (15.2s)

[5/8] Building HooksNotifier ..................
  ✓ dotnet publish completed (22.4s)

[6/8] Verifying artifacts .....................
  ✓ bin\HooksNotifier.exe (1.13.0.0, 284 KB)
  ✓ bin\NotifyHook.exe (1.13.0.0, 72 KB)
  ✓ webui\dist\index.html
  ✓ all 18 files in setup.iss [Files] section present

[7/8] Building installer .....................
  ✓ ClaudeCodeHooksNotifier-Setup-1.13.0.exe (2.1 MB)

[8/8] Smoke test .............................
  ✓ Process started (PID 12345)
  ✓ IPC ping → pong (23ms)
  ✓ IPC notify → no crash
  ✓ Process terminated cleanly

=== Release v1.13.0 Complete ===
Commit:    a1b2c3d4 (chore: bump version to 1.13.0 and rebuild installer)
Tag:       v1.13.0
Installer: ClaudeCodeHooksNotifier-Setup-1.13.0.exe
Smoke:     PASS (all 8 steps)
Duration:  2m 34s

Next step: git push origin master --tags
```

### 5.2 错误输出示例 — 前置检查失败

```
=== CC Hooks Notifier Release Pipeline ===
Version: 1.13.0

[1/8] Preflight checks .........................
  ✗ Working tree is dirty: 2 untracked files, 1 modified
    Untracked: scripts/release.ps1
    Untracked: test-tmp.log
    Modified:  src/HooksNotifier/TrayMode.cs

  → Commit or stash your changes first, or use -Force to override.
  → Aborting.
```

### 5.3 错误输出示例 — 制品缺失

```
[6/8] Verifying artifacts .....................
  ✗ Missing files referenced by setup.iss:
    - bin\HooksNotifier.exe (NOT FOUND)
    - webui\dist\index.html (NOT FOUND)

  → Run build steps again: npm run build && dotnet publish
  → If the file was removed from setup.iss intentionally, update setup.iss.
  → Aborting.
```

### 5.4 无需变更用户界面元素

- Tray icon 右键菜单无变化
- WebUI 主面板无变化
- 通知弹窗（Toast）无变化
- 托盘 tooltip 无变化

---

## 6. 实现计划

### 阶段 0：基础脚本框架（推荐首批交付）

| 任务 | 涉及文件 | 预估工作量 |
|------|---------|-----------|
| **L0.1** 创建 `scripts/release.ps1` 骨架（阶段编排 + 日志输出 + 参数解析） | `scripts/release.ps1` | 中 |
| **L0.2** 实现前置检查模块（git status、工具存在性、ISCC 降级路径） | `scripts/release.ps1` | 中 |
| **L0.3** 实现版本戳写模块（5 文件替换 + 一致性验证 + 陈旧版本扫描） | `scripts/release.ps1` | 中 |
| **L0.4** 更新 `CLAUDE.md` 构建和版本章节，引用新脚本 | `CLAUDE.md` | 小 |
| **L0.5** 更新 `publish.ps1` 使其也构建 NotifyHook 和 webui（或改为引用 release.ps1） | `publish.ps1` | 小 |

### 阶段 1：构建与验证（依赖 L0）

| 任务 | 涉及文件 | 预估工作量 |
|------|---------|-----------|
| **L1.1** 实现全量构建编排（npm install/ci、dotnet publish × 2，含超时和耗时记录） | `scripts/release.ps1` | 中 |
| **L1.2** 实现制品验证（解析 setup.iss [Files] 段 + 文件存在性 + 版本号检查） | `scripts/release.ps1` | 中 |
| **L1.3** 实现安装包构建和验证 | `scripts/release.ps1` | 小 |
| **L1.4** 创建 `CHANGELOG.md` 文件（首次手动填写历史版本） | `CHANGELOG.md` | 小 |

### 阶段 2：验证与发布（依赖 L1）

| 任务 | 涉及文件 | 预估工作量 |
|------|---------|-----------|
| **L2.1** 实现 TrayMode Ping/Pong IPC 处理 | `src/HooksNotifier/TrayMode.cs` | 小 |
| **L2.2** 创建 `scripts/verify-release.ps1`（进程启动 + IPC ping + 测试通知 + 进程终止） | `scripts/verify-release.ps1` | 中 |
| **L2.3** 实现变更日志自动生成（git log 解析 + Conventional Commit 分类） | `scripts/release.ps1` | 中 |
| **L2.4** 实现提交与标签自动化 | `scripts/release.ps1` | 小 |

### 阶段 3：CI/CD（依赖 L2）

| 任务 | 涉及文件 | 预估工作量 |
|------|---------|-----------|
| **L3.1** 创建 `.github/workflows/build.yml`（push/PR/tag 三触发器） | `.github/workflows/build.yml` | 中 |
| **L3.2** 配置 GitHub Release 创建（tag push 时附加上传 EXE） | `.github/workflows/build.yml` | 小 |
| **L3.3** 端到端测试：在干净环境中运行 release.ps1 验证完整流程 | 手动验证 | 中 |

---

## 7. 风险与注意事项

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| **ISCC.exe 路径不确定** | 中 | FR-2.3 定义降级路径搜索顺序，搜索失败时输出清晰安装指引 |
| **dotnet publish 超时/失败** | 低 | 每个构建步骤单独 try/catch，输出完整错误信息和构建日志路径 |
| **npm install 网络问题** | 中 | 建议 `npm ci`（更快更可靠），如果失败回退到 `npm install` |
| **版本号误改写（如替换了错误位置的数字）** | 中 | FR-3.2 一致性验证：只确认 5 个目标文件；FR-3.3 陈旧版本扫描做反向检查 |
| **冒烟测试在 CI 环境不可用（无 GUI）** | 中 | 冒烟测试支持 `-Headless` 模式（只测试进程启动和 IPC，不依赖托盘图标） |
| **git tag 冲突（重复版本号）** | 低 | FR-2.4 前置检查中检测已有 tag，除非 `-Force` 否则拒绝 |
| **publish.ps1 原有用户习惯被破坏** | 低 | 保留 `publish.ps1` 不删除，仅在其头部加注释指向 `release.ps1` |
| **CHANGELOG.md 自动分类不准确** | 低 | 自动分类后输出预览，提供 y/N 确认；用户可选择手动编辑模式 |
| **CI 中 ISCC license 问题** | 中 | GitHub Actions 使用 `jrsoftware.isscript` 或预装 ISCC，需验证 license 条款允许 CI 使用 |
| **WebUI 构建需要在 Windows 环境（ISCC Windows-only）** | 低 | CI runner 限定 `windows-latest`，这是已有约束 |

---

## 8. 验收标准

1. `scripts/release.ps1 -Version "1.13.0"` 在干净的 master 分支上完整执行 8 个阶段并成功输出摘要
2. 工作区有未提交更改时，不带 `-Force` 的执行立即终止并提示
3. 缺少 dotnet/node/npm/ISCC/git 任一工具时，前置检查失败并输出安装指引
4. 5 个版本文件全部被正确更新为新版本号，一致性验证通过
5. 陈旧版本扫描能正确检测到未被更新的文件并输出警告
6. 全量构建按正确顺序执行：webui → NotifyHook → HooksNotifier
7. 制品验证能检测缺失文件并终止（可手动删除一个测试文件验证）
8. 安装包成功生成到项目根目录，文件名包含正确版本号
9. `verify-release.ps1` 冒烟测试通过（进程启动、IPC ping、IPC 通知、进程终止）
10. `CHANGELOG.md` 自动生成包含了自上一个 tag 以来的合理分类提交列表
11. 不带 `-NoCommit` 运行时，自动创建 commit 和 annotated tag
12. 带 `-NoCommit` 运行时，不修改 git 历史，只输出待执行的命令摘要
13. 带 `-WhatIf` 运行时，不修改任何文件或 git 历史
14. CI 配置文件 `.github/workflows/build.yml` 在 push 到 master 时正确触发
15. CI 中 tag push 时正确创建 GitHub Release 并附加安装包
