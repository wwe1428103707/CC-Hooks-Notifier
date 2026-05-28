import { useState, useEffect, useCallback, useMemo, Fragment } from "react"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { t, setLanguage } from "./i18n"
import iconBase64 from "./icon_data"

// ── Types ──────────────────────────────────────────────────────────
interface Counts { total: number; p0: number; p05: number; toast: number; stateful: number }
interface EventRow { timestamp: string; level: string; eventName: string; summary: string; detail: string; isRead: boolean; _idx?: number }
interface Feedback { success: boolean; message: string }
interface AppState {
  counts: Counts; unreadCount: number; subagentCount: number; taskCount: number
  recentEvents: EventRow[]; allEvents: EventRow[]; language: string
  maxEntries?: number
  hookConfig?: Record<string, boolean>
  _feedback?: Feedback | null
}

const defaultState: AppState = {
  counts: { total: 0, p0: 0, p05: 0, toast: 0, stateful: 0 },
  unreadCount: 0, subagentCount: 0, taskCount: 0, recentEvents: [], allEvents: [], language: "en", maxEntries: 500,
}

const isWebView = typeof (window as any).chrome?.webview?.postMessage === "function"
function sendToCs(msg: object) {
  if (isWebView) (window as any).chrome.webview.postMessage(JSON.stringify(msg))
}

// ── Hook Toggle component ─────────────────────────────────────────
function HookToggle({ label, level, enabled, onChange }: { label: string; level: string; enabled: boolean; onChange: (v: boolean) => void }) {
  const hookLevels = [
    { prefix: "P0", color: "text-red-600" },
    { prefix: "P0.5", color: "text-orange-500" },
    { prefix: "P1", color: "text-blue-600" },
    { prefix: "P2", color: "text-muted-foreground" },
  ]
  const lvl = hookLevels.find(h => level.startsWith(h.prefix))
  return (
    <div className="flex items-center justify-between py-1.5 border-b border-gray-100 last:border-0">
      <div className="flex items-center gap-2 text-xs">
        <span className={`${lvl?.color || "text-muted-foreground"} font-mono w-12 shrink-0`}>{level}</span>
        <span className="text-foreground">{label}</span>
      </div>
      <div className={`w-9 h-5 rounded-full cursor-pointer transition-colors shrink-0 ${enabled ? 'bg-green-500' : 'bg-gray-300'}`}
        onClick={() => onChange(!enabled)}>
        <div className={`w-4 h-4 bg-white rounded-full shadow-sm mt-0.5 transition-transform ${enabled ? 'translate-x-[18px]' : 'translate-x-0.5'}`} />
      </div>
    </div>
  )
}

// ── Hook event metadata ────────────────────────────────────────────
const hookMeta: Record<string, string> = {
  "Notification(idle_prompt)": "hook.Task complete",
  "Notification(permission_prompt)": "hook.Permission needed",
  "StopFailure": "hook.API errors",
  "Stop": "hook.Responding finished",
  "TaskCompleted": "hook.Task completed",
  "SessionEnd": "hook.Session ended",
  "Notification(auth_success)": "hook.Auth success",
  "Notification(elicitation_dialog)": "hook.MCP input",
  "Notification(elicitation_complete)": "hook.MCP submitted",
  "PermissionDenied": "hook.Tool denied",
  "PostToolUse(Edit|Write)": "hook.File edited",
  "PostToolUseFailure(Bash|Edit)": "hook.Tool failed",
  "SubagentStop": "hook.Subagent done",
  "SessionStart": "hook.Session start",
  "PostCompact": "hook.Compacted",
  "ConfigChange": "hook.Config changed",
  "SubagentStart": "hook.Subagent start",
  "TaskCreated": "hook.Task created",
  "PreCompact": "hook.Compacting",
}

const hookLevels: Record<string, string> = {
  "Notification(idle_prompt)": "P0",
  "Notification(permission_prompt)": "P0",
  "StopFailure": "P0",
  "Stop": "P0.5",
  "TaskCompleted": "P0.5",
  "SessionEnd": "P0.5",
  "Notification(auth_success)": "P1",
  "Notification(elicitation_dialog)": "P1",
  "Notification(elicitation_complete)": "P1",
  "PermissionDenied": "P1",
  "PostToolUse(Edit|Write)": "P1",
  "PostToolUseFailure(Bash|Edit)": "P1",
  "SubagentStop": "P1",
  "SessionStart": "P1",
  "PostCompact": "P1",
  "ConfigChange": "P1",
  "SubagentStart": "P2",
  "TaskCreated": "P2",
  "PreCompact": "P2",
}

// ── Dashboard ──────────────────────────────────────────────────────
function Dashboard({ state, onToggleHook }: { state: AppState; onToggleHook: (key: string, v: boolean) => void }) {
  const cards = [
    { title: t("dashboard.notifications"), value: state.counts.total, accent: "border-t-blue-500" },
    { title: t("dashboard.unread"), value: state.unreadCount ?? 0, accent: "border-t-amber-500" },
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
      <div className="grid grid-cols-6 gap-4">
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
                <div key={i} className="flex items-center gap-2 text-sm font-mono min-w-0">
                  <span className="shrink-0">{levelIcon(e.level)}</span>
                  <span className="text-muted-foreground w-14 shrink-0 text-[11px]">[{e.timestamp}]</span>
                  <Badge variant="outline" className="shrink-0 font-mono text-xs max-w-[160px] truncate">{e.eventName}</Badge>
                  <span className="flex-1 min-w-0 truncate text-muted-foreground">{e.summary}</span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Hook Event Configuration */}
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("hooks.title")}</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs text-muted-foreground mb-3">{t("hooks.subtitle")}</p>
          <div className="grid grid-cols-2 gap-x-4">
            {state.hookConfig && Object.entries(state.hookConfig).map(([key, enabled]) => {
              const i18nKey = hookMeta[key]
              const label = i18nKey ? t(i18nKey) : key
              const lvl = hookLevels[key] || ""
              return <HookToggle key={key} label={label} level={lvl} enabled={enabled} onChange={(v) => onToggleHook(key, v)} />
            })}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

// ── Event Log ──────────────────────────────────────────────────────
function EventLog({ state, onClearHistory, onMarkAllRead, onMarkRead }: {
  state: AppState
  onClearHistory: () => void
  onMarkAllRead: () => void
  onMarkRead: (idx: number) => void
}) {
  const levelColor = (lvl: string) => {
    switch (lvl) {
      case "P0": return "text-red-600 font-semibold"
      case "P0.5": return "text-orange-500 font-semibold"
      case "Toast": return "text-blue-600"
      default: return "text-muted-foreground"
    }
  }

  const unreadDot = (lvl: string) => {
    switch (lvl) {
      case "P0": return "bg-red-500"
      case "P0.5": return "bg-orange-500"
      case "Toast": return "bg-blue-500"
      default: return "bg-gray-400"
    }
  }

  const filters = ["all", "unread", "P0", "P0.5", "Toast", "Stateful"]
  const filterLabels: Record<string, string> = {
    all: t("event_log.filter_all"),
    unread: t("event_log.filter_unread"),
    P0: t("event_log.filter_p0"),
    "P0.5": t("event_log.filter_p05"),
    Toast: t("event_log.filter_toast"),
    Stateful: t("event_log.filter_stateful"),
  }
  const [filter, setFilter] = useState("all")
  const [expanded, setExpanded] = useState<string | null>(null)
  const toggle = (key: string) => setExpanded(expanded === key ? null : key)

  const filteredEvents = useMemo(() => {
    let list = [...state.allEvents]
    if (filter === "unread") list = list.filter(e => !e.isRead)
    else if (filter !== "all") list = list.filter(e => e.level === filter)
    // Unread first
    list.sort((a, b) => {
      if (!a.isRead && b.isRead) return -1
      if (a.isRead && !b.isRead) return 1
      return 0
    })
    return list
  }, [state.allEvents, filter])

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">{t("event_log.title")}</h2>
        <div className="flex items-center gap-2">
          {(state.unreadCount ?? 0) > 0 && (
            <button onClick={onMarkAllRead}
              className="text-xs px-2 py-1 rounded bg-amber-100 hover:bg-amber-200 text-amber-800 font-medium cursor-pointer transition-colors">
              {t("event_log.mark_all_read")} ({state.unreadCount})
            </button>
          )}
          <p className="text-xs text-muted-foreground">{t("event_log.total", String(state.allEvents.length))}</p>
          <button onClick={onClearHistory}
            className="text-xs px-2 py-1 rounded border border-gray-300 hover:bg-gray-100 text-muted-foreground cursor-pointer">
            {t("event_log.clear")}
          </button>
        </div>
      </div>

      {/* Filter tabs */}
      <div className="flex gap-1">
        {filters.map(f => (
          <button key={f} onClick={() => setFilter(f)}
            className={`text-xs px-2.5 py-1 rounded cursor-pointer transition-colors ${
              filter === f ? 'bg-gray-800 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
            }`}>
            {filterLabels[f] || f}
          </button>
        ))}
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
            {filteredEvents.length === 0 ? (
              <tr><td colSpan={4} className="px-3 py-8 text-center text-muted-foreground">{t("event_log.no_events")}</td></tr>
            ) : filteredEvents.map((e, i) => {
              const key = `${e.timestamp}-${e.eventName}-${i}`
              return (
              <Fragment key={key}>
                <tr className={`border-t hover:bg-muted/30 cursor-pointer ${!e.isRead ? 'bg-amber-50' : ''}`}
                  onClick={() => {
                    toggle(key)
                    if (!e.isRead && e._idx != null) onMarkRead(e._idx)
                  }}>
                  <td className="px-3 py-1.5 text-muted-foreground">
                    <span className="flex items-center gap-1">
                      {!e.isRead && <span className={`inline-block w-2 h-2 rounded-full shrink-0 ${unreadDot(e.level)}`} />}
                      {e.timestamp}
                    </span>
                  </td>
                  <td className={`px-3 py-1.5 ${levelColor(e.level)}`}>{e.level}</td>
                  <td className="px-3 py-1.5">{e.eventName}</td>
                  <td className="px-3 py-1.5 text-muted-foreground truncate max-w-md">
                    <span className="flex items-center gap-1">
                      <span className="truncate">{e.summary}</span>
                      {e.summary.length > 50 && <span className="text-[10px] text-gray-400 shrink-0">{expanded === key ? "▲" : "▸"}</span>}
                    </span>
                  </td>
                </tr>
                {expanded === key && (
                  <tr className="bg-gray-50 border-t border-gray-200">
                    <td colSpan={4} className="px-4 py-3 text-xs text-gray-700 whitespace-pre-wrap break-all leading-relaxed">
                      {e.detail || e.summary}
                    </td>
                  </tr>
                )}
              </Fragment>
            )})}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ── Settings ───────────────────────────────────────────────────────
function Settings({ state, onSetLang, onUpdatePath, onOpenSettings, onSetMaxEntries }:
  { state: AppState; onSetLang: (code: string) => void; onUpdatePath: () => void; onOpenSettings: () => void; onSetMaxEntries: (v: number) => void }) {
  const maxEntryOptions = [100, 200, 500, 1000, 2000, 5000]
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
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.max_entries")}</CardTitle></CardHeader>
        <CardContent className="space-y-1">
          <p className="text-xs text-muted-foreground">{t("settings.max_entries_desc")}</p>
          <div className="flex gap-2 flex-wrap">
            {maxEntryOptions.map(n => (
              <button key={n} onClick={() => onSetMaxEntries(n)}
                className={`text-xs px-2.5 py-1 rounded cursor-pointer transition-colors ${(state.maxEntries ?? 500) === n ? 'bg-gray-800 text-white' : 'bg-gray-100 text-gray-600 hover:bg-gray-200'}`}>
                {n}
              </button>
            ))}
          </div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">{t("settings.auto_start")}</CardTitle></CardHeader>
        <CardContent className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">{t("settings.auto_start_desc")}</p>
          <Switch defaultChecked />
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
          <img src={iconBase64} alt="" className="w-10 h-10" />
          <div>
            <h2 className="text-xl font-bold">Claude Code Hooks Notifier</h2>
            <p className="text-sm text-muted-foreground">{t("about.version", "1.13.1")}</p>
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
function HelpModal({ onClose }: { onClose: () => void }) {
  const sections = [
    { key: "overview", icon: "ℹ️" },
    { key: "levels", icon: "🔔" },
    { key: "permission", icon: "🛡️" },
    { key: "events", icon: "⚙️" },
    { key: "event_log", icon: "📋" },
    { key: "arch", icon: "🏗️" },
    { key: "lang", icon: "🌐" },
  ]

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" onClick={onClose}>
      <div className="bg-white rounded-xl shadow-2xl max-w-2xl w-full mx-4 max-h-[80vh] flex flex-col" onClick={e => e.stopPropagation()}>
        <div className="flex items-center justify-between px-6 py-4 border-b shrink-0">
          <h2 className="text-lg font-bold">{t("help.title")}</h2>
          <button onClick={onClose} className="text-2xl leading-none text-gray-400 hover:text-gray-700 cursor-pointer">&times;</button>
        </div>
        <div className="overflow-y-auto px-6 py-4 space-y-4">
          {sections.map(s => (
            <div key={s.key}>
              <h3 className="text-sm font-semibold text-gray-800 mb-1">{s.icon} {t(`help.${s.key}`)}</h3>
              <p className="text-xs text-gray-600 leading-relaxed">{t(`help.${s.key}_desc`)}</p>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

export default function App() {
  const [state, setState] = useState<AppState>(defaultState)
  const [showHelp, setShowHelp] = useState(false)

  const handleCsMessage = useCallback((msg: { type: string; payload: any }) => {
    if (msg.type === "state_sync") {
      setLanguage(msg.payload.language)
      setState(msg.payload)
    } else if (msg.type === "event_push") {
      setState(prev => ({
        ...prev,
        unreadCount: msg.payload.unreadCount ?? (prev.unreadCount + 1),
        recentEvents: [msg.payload, ...prev.recentEvents].slice(0, 5),
        allEvents: [msg.payload, ...prev.allEvents].slice(0, prev.maxEntries ?? 500),
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
        <span className="text-lg font-bold">{t("window.title")}</span>
        <span className="text-xs text-gray-400 pt-0.5">{t("header.version")}</span>
        <div className="flex-1" />
        <button onClick={() => setShowHelp(true)}
          className="text-xs text-gray-300 hover:text-white bg-gray-700 hover:bg-gray-600 px-2.5 py-1 rounded cursor-pointer transition-colors">
          ? {t("help.button")}
        </button>
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

        <TabsContent value="dashboard"><Dashboard state={state} onToggleHook={(key, v) => sendToCs({ type: "set_hook_config", payload: { key, enabled: v } })} /></TabsContent>
        <TabsContent value="eventlog"><EventLog state={state} onClearHistory={() => sendToCs({ type: "clear_history" })} onMarkAllRead={() => sendToCs({ type: "mark_all_read" })} onMarkRead={(idx) => sendToCs({ type: "mark_read", payload: { index: idx } })} /></TabsContent>
        <TabsContent value="settings"><Settings state={state} onSetLang={setLang} onUpdatePath={() => sendToCs({ type: "update_hook_path" })} onOpenSettings={() => sendToCs({ type: "open_settings" })} onSetMaxEntries={(v) => sendToCs({ type: "set_max_entries", payload: { value: v } })} /></TabsContent>
        <TabsContent value="about"><About /></TabsContent>
      </Tabs>
      {showHelp && <HelpModal onClose={() => setShowHelp(false)} />}
    </div>
  )
}
