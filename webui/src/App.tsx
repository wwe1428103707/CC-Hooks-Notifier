import { useState, useEffect, useCallback } from "react"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { t, setLanguage } from "./i18n"

// ── Types ──────────────────────────────────────────────────────────
interface Counts { total: number; p0: number; p05: number; toast: number; stateful: number }
interface EventRow { timestamp: string; level: string; eventName: string; summary: string }
interface Feedback { success: boolean; message: string }
interface AppState {
  counts: Counts; subagentCount: number; taskCount: number
  recentEvents: EventRow[]; allEvents: EventRow[]; language: string
  hookConfig?: Record<string, boolean>
  _feedback?: Feedback | null
}

const defaultState: AppState = {
  counts: { total: 0, p0: 0, p05: 0, toast: 0, stateful: 0 },
  subagentCount: 0, taskCount: 0, recentEvents: [], allEvents: [], language: "en",
}

const isWebView = typeof (window as any).chrome?.webview?.postMessage === "function"
function sendToCs(msg: object) {
  if (isWebView) (window as any).chrome.webview.postMessage(JSON.stringify(msg))
}

// ── Dashboard ──────────────────────────────────────────────────────
function Dashboard({ state }: { state: AppState }) {
  const cards = [
    { title: t("dashboard.notifications"), value: state.counts.total, accent: "border-t-blue-500" },
    { title: t("dashboard.p0_blinks"), value: state.counts.p0, accent: "border-t-red-500" },
    { title: t("dashboard.toasts"), value: state.counts.toast, accent: "border-t-blue-500" },
    { title: t("dashboard.subagents"), value: state.subagentCount, accent: "border-t-green-500" },
    { title: t("dashboard.tasks"), value: state.taskCount, accent: "border-t-green-500" },
  ]

  const levelIcon = (lvl: string) => {
    switch (lvl) { case "P0": return "🔴"; case "P0.5": return "🟠"; case "Toast": return "🔵"; default: return "⚪" }
  }

  return (
    <div className="p-6 space-y-6">
      <div className="grid grid-cols-5 gap-4">
        {cards.map((c, i) => (
          <Card key={i} className={`${c.accent} border-t-3`}>
            <CardHeader className="pb-1 pt-3 px-4">
              <CardTitle className="text-xs text-muted-foreground">{c.title}</CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-4"><p className="text-3xl font-bold">{c.value}</p></CardContent>
          </Card>
        ))}
      </div>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("dashboard.recent_events")}</CardTitle></CardHeader>
        <CardContent className="px-4 pb-4">
          {state.recentEvents.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t("dashboard.no_events")}</p>
          ) : (
            <div className="space-y-1.5">
              {state.recentEvents.map((e, i) => (
                <div key={i} className="flex items-center gap-2 text-sm font-mono">
                  <span>{levelIcon(e.level)}</span>
                  <span className="text-muted-foreground w-16 shrink-0">[{e.timestamp}]</span>
                  <Badge variant="outline" className="w-24 shrink-0 font-mono text-xs">{e.eventName}</Badge>
                  <span className="truncate text-muted-foreground">{e.summary}</span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

// ── Event Log ──────────────────────────────────────────────────────
function EventLog({ state }: { state: AppState }) {
  const levelColor = (lvl: string) => {
    switch (lvl) {
      case "P0": return "text-red-600 font-semibold"
      case "P0.5": return "text-orange-500 font-semibold"
      case "Toast": return "text-blue-600"
      default: return "text-muted-foreground"
    }
  }

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">{t("event_log.title")}</h2>
        <p className="text-xs text-muted-foreground">{t("event_log.total", String(state.allEvents.length))}</p>
      </div>
      <div className="border rounded-lg overflow-hidden">
        <table className="w-full text-xs font-mono">
          <thead>
            <tr className="bg-muted/50 text-left">
              <th className="px-3 py-2 text-muted-foreground font-medium">{t("event_log.time")}</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">{t("event_log.level")}</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">{t("event_log.event")}</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">{t("event_log.content")}</th>
            </tr>
          </thead>
          <tbody>
            {state.allEvents.length === 0 ? (
              <tr><td colSpan={4} className="px-3 py-8 text-center text-muted-foreground">{t("event_log.no_events")}</td></tr>
            ) : state.allEvents.map((e, i) => (
              <tr key={i} className="border-t hover:bg-muted/30">
                <td className="px-3 py-1.5 text-muted-foreground">{e.timestamp}</td>
                <td className={`px-3 py-1.5 ${levelColor(e.level)}`}>{e.level}</td>
                <td className="px-3 py-1.5">{e.eventName}</td>
                <td className="px-3 py-1.5 text-muted-foreground truncate max-w-md">{e.summary}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ── Settings ───────────────────────────────────────────────────────
// ── Hook event config (P0/P0.5 default ON, P1/P2 default OFF) ──
const hookLevels = [
  { level: "P0 🔔", color: "text-red-600" },
  { level: "P0.5 🔔", color: "text-orange-500" },
  { level: "P1 📢", color: "text-blue-600" },
  { level: "P2 🟢", color: "text-muted-foreground" },
]

function HookToggle({ label, level, enabled, onChange }: { label: string; level: string; enabled: boolean; onChange: (v: boolean) => void }) {
  const lvl = hookLevels.find(h => level.startsWith(h.level.slice(0, 2)))
  return (
    <div className="flex items-center justify-between py-1.5 border-b border-gray-100 last:border-0">
      <div className="flex items-center gap-2 text-xs">
        <span className={lvl?.color || "text-muted-foreground font-mono w-12"}>{level}</span>
        <span className="text-foreground">{label}</span>
      </div>
      <div className={`w-9 h-5 rounded-full cursor-pointer transition-colors ${enabled ? 'bg-green-500' : 'bg-gray-300'}`}
        onClick={() => onChange(!enabled)}>
        <div className={`w-4 h-4 bg-white rounded-full shadow-sm mt-0.5 transition-transform ${enabled ? 'translate-x-[18px]' : 'translate-x-0.5'}`} />
      </div>
    </div>
  )
}

function Settings({ state, onSetLang, onUpdatePath, onOpenSettings, onToggleHook }:
  { state: AppState; onSetLang: (code: string) => void; onUpdatePath: () => void; onOpenSettings: () => void; onToggleHook: (key: string, enabled: boolean) => void }) {
  return (
    <div className="p-6 space-y-4 max-w-xl">
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.language")}</CardTitle></CardHeader>
        <CardContent>
          <Select value={state.language} onValueChange={onSetLang}>
            <SelectTrigger className="w-40"><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="en">English</SelectItem>
              <SelectItem value="zh">中文</SelectItem>
            </SelectContent>
          </Select>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.auto_start")}</CardTitle></CardHeader>
        <CardContent className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">{t("settings.auto_start_desc")}</p>
          <Switch defaultChecked />
        </CardContent>
      </Card>

      {/* Hook configuration card */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Hook Events</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs text-muted-foreground mb-3">Enable/disable notifications for specific hook events</p>
          <div className="space-y-0">
            {state.hookConfig && Object.entries(state.hookConfig).map(([key, enabled]) => {
              const meta = ({
                "Notification(idle_prompt)":        ["P0",   "Task complete"],
                "Notification(permission_prompt)":  ["P0",   "Permission needed"],
                "StopFailure":                      ["P0",   "API errors"],
                "Stop":                             ["P0.5", "Responding finished"],
                "TaskCompleted":                    ["P0.5", "Task completed"],
                "SessionEnd":                       ["P0.5", "Session ended"],
                "Notification(auth_success)":       ["P1",   "Auth success"],
                "Notification(elicitation_dialog)": ["P1",   "MCP input"],
                "Notification(elicitation_complete)":["P1",  "MCP submitted"],
                "PermissionDenied":                 ["P1",   "Tool denied"],
                "PostToolUse":                      ["P1",   "File edited"],
                "PostToolUseFailure":               ["P1",   "Tool failed"],
                "SubagentStop":                     ["P1",   "Subagent done"],
                "SessionStart":                     ["P1",   "Session start"],
                "PostCompact":                      ["P1",   "Compacted"],
                "ConfigChange":                     ["P1",   "Config changed"],
                "SubagentStart":                    ["P2",   "Subagent start"],
                "TaskCreated":                      ["P2",   "Task created"],
                "PreCompact":                       ["P2",   "Compacting"],
              } as Record<string, [string, string]>)
              const [lvl, desc] = meta[key] || ["", key]
              return <HookToggle key={key} label={desc} level={lvl} enabled={enabled} onChange={(v) => onToggleHook(key, v)} />
            })}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.hook_path")}</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <p className="text-xs font-mono text-muted-foreground break-all">{t("settings.hook_path_placeholder")}</p>
          <Button variant="outline" size="sm" onClick={onUpdatePath}>{t("settings.update_path")}</Button>
          {state._feedback && (
            <div className={`mt-2 px-3 py-2 rounded text-xs font-medium ${state._feedback.success ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-red-50 text-red-700 border border-red-200'}`}>
              {state._feedback.success ? '✅ ' : '❌ '}{state._feedback.message}
            </div>
          )}
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.settings_file")}</CardTitle></CardHeader>
        <CardContent>
          <Button variant="outline" size="sm" onClick={onOpenSettings}>{t("settings.open_file")}</Button>
        </CardContent>
      </Card>
    </div>
  )
}

// ── About ───────────────────────────────────────────────────────────
const coverage = [
  { level: "P0 🔔", type: "Long blink (10s)", events: "idle_prompt, permission_prompt, StopFailure" },
  { level: "P0.5 🔔", type: "Short blink (5s)", events: "Stop, TaskCompleted, SessionEnd" },
  { level: "P1 📢", type: "Toast", events: "auth_success, elicitation, PermissionDenied, PostToolUse, etc." },
  { level: "P2 🟢", type: "Stateful", events: "SubagentStart, TaskCreated, PreCompact" },
]

function About() {
  return (
    <div className="p-6 max-w-2xl">
      <Card className="p-6 space-y-4">
        <div className="flex items-center gap-3">
          <span className="text-4xl">⚡</span>
          <div>
            <h2 className="text-xl font-bold">Claude Code Hooks Notifier</h2>
            <p className="text-sm text-muted-foreground">{t("about.version", "1.4.0")}</p>
          </div>
        </div>
        <p className="text-sm text-muted-foreground">{t("about.tech_stack")}</p>
        <p className="text-sm">{t("about.description")}</p>
        <div className="border-t pt-4">
          <h3 className="text-sm font-semibold mb-2">{t("about.coverage")}</h3>
          <table className="w-full text-xs">
            <thead>
              <tr className="text-muted-foreground">
                <th className="text-left py-1 pr-4">{t("about.level")}</th>
                <th className="text-left py-1 pr-4">{t("about.type")}</th>
                <th className="text-left py-1">{t("about.events")}</th>
              </tr>
            </thead>
            <tbody>
              {coverage.map((c) => (
                <tr key={c.level} className="border-t">
                  <td className="py-1.5 pr-4 font-mono">{c.level}</td>
                  <td className="py-1.5 pr-4 text-muted-foreground">{c.type}</td>
                  <td className="py-1.5 text-muted-foreground text-[11px]">{c.events}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
    </div>
  )
}

// ── App Root ────────────────────────────────────────────────────────
export default function App() {
  const [state, setState] = useState<AppState>(defaultState)

  const handleCsMessage = useCallback((msg: AppState & { type: string; payload: any }) => {
    if (msg.type === "state_sync") {
      setLanguage(msg.payload.language)
      setState(msg.payload)
    } else if (msg.type === "event_push") {
      setState(prev => ({
        ...prev,
        recentEvents: [msg.payload, ...prev.recentEvents].slice(0, 5),
        allEvents: [msg.payload, ...prev.allEvents].slice(0, 500),
        counts: {
          ...prev.counts,
          total: prev.counts.total + 1,
          ...(msg.payload.level === "P0" ? { p0: prev.counts.p0 + 1 }
            : msg.payload.level === "P0.5" ? { p05: prev.counts.p05 + 1 }
            : msg.payload.level === "Toast" ? { toast: prev.counts.toast + 1 }
            : { stateful: prev.counts.stateful + 1 }),
        },
      }))
    } else if (msg.type === "lang_changed") {
      setState(prev => ({ ...prev, language: msg.payload }))
    } else if (msg.type === "configure_hooks_result") {
      setState(prev => ({ ...prev, _feedback: msg.payload }))
      setTimeout(() => setState(prev => ({ ...prev, _feedback: null })), 4000)
    } else if (msg.type === "hook_config") {
      setState(prev => ({ ...prev, hookConfig: msg.payload }))
    }
  }, [])

  useEffect(() => {
    if (isWebView) {
      // C# PostWebMessageAsJson delivers e.data as a parsed object, not a string
      const onCsMsg = (e: any) => {
        try {
          const data = typeof e.data === "string" ? JSON.parse(e.data) : e.data
          handleCsMessage(data)
        } catch { /* ignore malformed */ }
      }
      // WebView2: e.data is already an object
      if ((window as any).chrome?.webview?.addEventListener) {
        ;(window as any).chrome.webview.addEventListener("message", onCsMsg)
      }
      // Fallback: window.postMessage (e.data is a string)
      window.addEventListener("message", onCsMsg)
      sendToCs({ type: "get_state" })
    }
  }, [handleCsMessage])

  // Sync language from state on first load
  useEffect(() => { setLanguage(state.language) }, []) // eslint-disable-line

  const setLang = (code: string) => {
    setLanguage(code)
    setState(prev => ({ ...prev, language: code }))
    sendToCs({ type: "set_lang", payload: code })
  }

  return (
    <div className="min-h-screen bg-[#f5f7fa]">
      <header className="bg-[#212529] text-white h-12 flex items-center px-5 gap-2">
        <span className="text-lg font-bold">⚡ {t("window.title")}</span>
        <span className="text-xs text-gray-400 pt-0.5">{t("header.version")}</span>
      </header>

      <Tabs defaultValue="dashboard" className="w-full">
        <div className="px-6 pt-3 border-b bg-white">
          <TabsList>
            <TabsTrigger value="dashboard">📊 {t("tab.dashboard")}</TabsTrigger>
            <TabsTrigger value="eventlog">📋 {t("tab.event_log")}</TabsTrigger>
            <TabsTrigger value="settings">⚙ {t("tab.settings")}</TabsTrigger>
            <TabsTrigger value="about">ℹ {t("tab.about")}</TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="dashboard"><Dashboard state={state} /></TabsContent>
        <TabsContent value="eventlog"><EventLog state={state} /></TabsContent>
        <TabsContent value="settings"><Settings state={state} onSetLang={setLang} onUpdatePath={() => sendToCs({ type: "update_hook_path" })} onOpenSettings={() => sendToCs({ type: "open_settings" })} onToggleHook={(key, enabled) => sendToCs({ type: "set_hook_config", payload: { key, enabled } })} /></TabsContent>
        <TabsContent value="about"><About /></TabsContent>
      </Tabs>
    </div>
  )
}
