# WebView2 + React/shadcn UI 迁移计划

## 依赖关系图

```
Step A: Init webui/ (React+Vite+shadcn)       ← 必须先完成
     │
     ├── Step B: Rewrite MainWindow (WebView2) ← 与 C 系列并行
     │
     └── UI Pages（4 个独立任务，可并行）:
          ├── Step C1: Dashboard Page
          ├── Step C2: Event Log Page
          ├── Step C3: Settings Page
          └── Step C4: About Page
               │
               └── Step D: Wire C# ↔ WebView2 IPC
                    │
                    └── Step E: Build + Installer
```

## 执行策略

| Phase | Steps | 并行度 | 说明 |
|-------|-------|--------|------|
| **Phase 1** | A | 串行 | 初始化项目骨架，安装依赖 |
| **Phase 2** | B + C1/C2/C3/C4 | **5 路并行** | 后端 WebView2 宿主 + 4 个 Tab 页面同时开发 |
| **Phase 3** | D | 串行 | 集成联调，打通双向 IPC |
| **Phase 4** | E | 串行 | 构建、安装包、发布 |

## 步骤详情

### Phase 1 — 基础框架

#### Step A: 初始化 webui/ 项目

| 子任务 | 工具/命令 |
|--------|----------|
| 创建 Vite + React + TS 项目 | `npm create vite@latest webui -- --template react-ts` |
| 安装 TailwindCSS v4 | `npm install tailwindcss @tailwindcss/vite` |
| 初始化 shadcn/ui | `npx shadcn@latest init` |
| 添加组件 | `npx shadcn@latest add card tabs table button select switch` |
| 配置 Vite build 输出到 `bin/webui/` | `vite.config.ts` |
| 验证 dev server 运行 | `npm run dev` |

**输出**: `src/HooksNotifier/webui/` 目录，React 项目骨架

---

### Phase 2 — 并行开发（5 路并发）

#### Step B: 重写 MainWindow（WebView2 宿主）

| 文件 | 变更 |
|------|------|
| `MainWindow.cs` | 替换 WinForms 控件为 `Microsoft.Web.WebView2.WinForms.WebView2` |
| `HooksNotifier.csproj` | 添加 `Microsoft.Web.WebView2` NuGet 引用 |
| 新增 `WebBridge.cs` | C# ↔ JS 消息序列化/路由层 |

**WebBridge 消息协议：**

```
C# → JS (PostWebMessageAsJson):
{
  "type": "event_push" | "state_sync" | "lang_changed",
  "payload": { ... }
}

JS → C# (chrome.webview.postMessage):
{
  "type": "set_lang" | "toggle_autostart" | "configure_hooks" | "open_settings",
  "payload": { ... }
}
```

**同时开发**（由 5 个独立 Agent 并行完成）：

#### Step C1: Dashboard Page

| 文件 | 说明 |
|------|------|
| `webui/src/pages/Dashboard.tsx` | 主页面 |
| `webui/src/components/StatCard.tsx` | 统计卡片（accent border + large number） |
| `webui/src/components/RecentEvents.tsx` | 最近事件列表 |

**数据来源：** `state_sync` 消息携带 `{counts, subagentCount, taskCount, recentEvents[]}`

#### Step C2: Event Log Page

| 文件 | 说明 |
|------|------|
| `webui/src/pages/EventLog.tsx` | 主页面 |
| `webui/src/components/EventTable.tsx` | shadcn Table + 颜色编码 |

**数据来源：** `state_sync` + `event_push` 增量追加

#### Step C3: Settings Page

| 文件 | 说明 |
|------|------|
| `webui/src/pages/Settings.tsx` | 主页面 |
| `webui/src/components/LanguageSelect.tsx` | 语言切换 |
| `webui/src/components/AutoStartToggle.tsx` | 开机自启开关 |
| `webui/src/components/HookPathEditor.tsx` | Hook 路径显示+更新 |

**交互：** JS → C# → 执行操作 → C# → JS 返回结果

#### Step C4: About Page

| 文件 | 说明 |
|------|------|
| `webui/src/pages/About.tsx` | 主页面（静态内容） |

---

### Phase 3 — 集成联调

#### Step D: 打通 C# ↔ WebView2 IPC

| 子任务 | 说明 |
|--------|------|
| 实现 WebBridge.CsToJs() | 推送事件、同步状态、语言切换 |
| 实现 WebBridge.JsToCs() | 分发 JS 请求到对应 C# 模块 |
| 对接 TrayMode | 状态变更时同时推送到 WebView2 |
| 对接 EventHistory | 新事件到达时推送 JS |
| 对接 I18n | 语言切换时通知 JS 刷新 |
| 验证全部 4 个 Tab 页交互 |

---

### Phase 4 — 构建与发布

#### Step E: 构建集成

| 子任务 | 说明 |
|--------|------|
| 更新 `build-all.ps1` | 先 `npm run build`，再 `dotnet publish` |
| 更新 `setup.iss` | 将 `webui/dist/` 复制到 `{app}/webui/` |
| 重建安装包 | `ISCC.exe setup.iss` |
| 版本升级 | 1.4.0 → 1.5.0 |

## 并行执行时序

```
时间 →
─────────────────────────────────────────────────────
Phase 1:  A  ████████
               │
Phase 2:       ├── B  ████████████████
               ├── C1 ████████
               ├── C2 ████████
               ├── C3 ████████
               └── C4 ██████
                         │
Phase 3:                D  ████████████
                            │
Phase 4:                   E  ████████
```

Phase 2 中 B 和 C1-C4 可以同时进行，由不同 Agent 并行执行。

## 技术要点

### WebView2 初始化（MainWindow.cs）

```csharp
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

var webView = new WebView2 { Dock = DockStyle.Fill };
await webView.EnsureCoreWebView2Async();

// 加载 React 构建产物
webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
    "app.local", "webui/dist", CoreWebView2HostResourceAccessKind.DenyCors);

webView.CoreWebView2.Navigate("https://app.local/index.html");

// 监听 JS 消息
webView.CoreWebView2.WebMessageReceived += OnWebMessage;

// 发送消息到 JS
webView.CoreWebView2.PostWebMessageAsJson(json);
```

### shadcn/ui 页面示例（Dashboard.tsx）

```tsx
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"

export function Dashboard() {
  return (
    <div className="grid grid-cols-3 gap-4 p-6">
      <Card>
        <CardHeader className="border-t-4 border-t-blue-500 pb-2">
          <CardTitle className="text-sm text-muted-foreground">
            Notifications
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">{counts.total}</p>
        </CardContent>
      </Card>
      ...
    </div>
  )
}
```

## 版本

| Phase | 版本 | 说明 |
|-------|------|------|
| Phase 1-3 | 1.4.x | 开发中 |
| Phase 4 | 1.5.0 | 发布版本 |
