import { useState, useEffect, useCallback } from "react"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"

// ── Types ──────────────────────────────────────────────────────────
interface Counts { total: number; p0: number; p05: number; toast: number; stateful: number }
interface EventRow { timestamp: string; level: string; eventName: string; summary: string }
interface AppState {
  counts: Counts; subagentCount: number; taskCount: number
  recentEvents: EventRow[]; allEvents: EventRow[]; language: string
}

const defaultState: AppState = {
  counts: { total: 0, p0: 0, p05: 0, toast: 0, stateful: 0 },
  subagentCount: 0, taskCount: 0, recentEvents: [], allEvents: [], language: "en",
}

// ── WebBridge helpers ──────────────────────────────────────────────
const isWebView = typeof (window as any).chrome?.webview?.postMessage === "function"
function sendToCs(msg: object) {
  if (isWebView) (window as any).chrome.webview.postMessage(JSON.stringify(msg))
}

// ── Dashboard ──────────────────────────────────────────────────────
function Dashboard({ state }: { state: AppState }) {
  const cards = [
    { title: "Notifications", value: state.counts.total, accent: "border-t-blue-500" },
    { title: "P0 Blinks", value: state.counts.p0, accent: "border-t-red-500" },
    { title: "Toasts", value: state.counts.toast, accent: "border-t-blue-500" },
    { title: "Subagents", value: state.subagentCount, accent: "border-t-green-500" },
    { title: "Tasks", value: state.taskCount, accent: "border-t-green-500" },
  ]

  const levelIcon = (lvl: string) => {
    switch (lvl) {
      case "P0": return "🔴"
      case "P0.5": return "🟠"
      case "Toast": return "🔵"
      default: return "⚪"
    }
  }

  return (
    <div className="p-6 space-y-6">
      <div className="grid grid-cols-5 gap-4">
        {cards.map((c, i) => (
          <Card key={i} className={`${c.accent} border-t-3`}>
            <CardHeader className="pb-1 pt-3 px-4">
              <CardTitle className="text-xs text-muted-foreground">{c.title}</CardTitle>
            </CardHeader>
            <CardContent className="px-4 pb-4">
              <p className="text-3xl font-bold">{c.value}</p>
            </CardContent>
          </Card>
        ))}
      </div>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Recent Events</CardTitle></CardHeader>
        <CardContent className="px-4 pb-4">
          {state.recentEvents.length === 0 ? (
            <p className="text-sm text-muted-foreground">(no events yet)</p>
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
        <h2 className="text-sm font-semibold">Event Log</h2>
        <p className="text-xs text-muted-foreground">{state.allEvents.length} total</p>
      </div>
      <div className="border rounded-lg overflow-hidden">
        <table className="w-full text-xs font-mono">
          <thead>
            <tr className="bg-muted/50 text-left">
              <th className="px-3 py-2 text-muted-foreground font-medium">Time</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">Level</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">Event</th>
              <th className="px-3 py-2 text-muted-foreground font-medium">Content</th>
            </tr>
          </thead>
          <tbody>
            {state.allEvents.length === 0 ? (
              <tr><td colSpan={4} className="px-3 py-8 text-center text-muted-foreground">(no events)</td></tr>
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

// ── Settings ────────────────────────────────────────────────────────
function Settings({ state, onSetLang }: { state: AppState; onSetLang: (code: string) => void }) {
  return (
    <div className="p-6 space-y-4 max-w-xl">
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Language</CardTitle></CardHeader>
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
        <CardHeader className="pb-2"><CardTitle className="text-sm">Auto-start</CardTitle></CardHeader>
        <CardContent className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground">Start when I log in</p>
          <Switch defaultChecked />
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Hook Executable Path</CardTitle></CardHeader>
        <CardContent className="space-y-2">
          <p className="text-xs font-mono text-muted-foreground break-all">(loaded from C# backend)</p>
          <Button variant="outline" size="sm">Update Path</Button>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Settings File</CardTitle></CardHeader>
        <CardContent>
          <Button variant="outline" size="sm">Open settings.json</Button>
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
            <p className="text-sm text-muted-foreground">Version: 1.4.0</p>
          </div>
        </div>
        <p className="text-sm text-muted-foreground">
          Tech Stack: React + shadcn/ui + WebView2 + C# .NET 9
        </p>
        <p className="text-sm">
          Windows system tray notification service for Claude Code hooks.
          Monitors PermissionRequest, Notification, StopFailure, and more.
        </p>
        <div className="border-t pt-4">
          <h3 className="text-sm font-semibold mb-2">Hook Event Coverage</h3>
          <table className="w-full text-xs">
            <thead>
              <tr className="text-muted-foreground">
                <th className="text-left py-1 pr-4">Level</th>
                <th className="text-left py-1 pr-4">Type</th>
                <th className="text-left py-1">Events</th>
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

  // Listen for C# messages via WebBridge
  const handleCsMessage = useCallback((msg: AppState & { type: string; payload: any }) => {
    if (msg.type === "state_sync") setState(msg.payload)
    else if (msg.type === "event_push") {
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
    }
  }, [])

  useEffect(() => {
    if (isWebView) {
      // Listen for C# messages in WebView2
      window.addEventListener("message", (e) => {
        try { handleCsMessage(JSON.parse(e.data)) } catch {}
      })
      // Also listen via chrome.webview if available
      if ((window as any).chrome?.webview?.addEventListener) {
        ;(window as any).chrome.webview.addEventListener("message", (e: any) => {
          try { handleCsMessage(JSON.parse(e.data)) } catch {}
        })
      }
      // Request initial state
      sendToCs({ type: "get_state" })
    }
  }, [handleCsMessage])

  const setLanguage = (code: string) => {
    sendToCs({ type: "set_lang", payload: code })
    setState(prev => ({ ...prev, language: code }))
  }

  return (
    <div className="min-h-screen bg-[#f5f7fa]">
      <header className="bg-[#212529] text-white h-12 flex items-center px-5 gap-2">
        <span className="text-lg font-bold">⚡ Claude Code Hooks Notifier</span>
        <span className="text-xs text-gray-400 pt-0.5">v1.4.0</span>
      </header>

      <Tabs defaultValue="dashboard" className="w-full">
        <div className="px-6 pt-3 border-b bg-white">
          <TabsList>
            <TabsTrigger value="dashboard">📊 Dashboard</TabsTrigger>
            <TabsTrigger value="eventlog">📋 Event Log</TabsTrigger>
            <TabsTrigger value="settings">⚙ Settings</TabsTrigger>
            <TabsTrigger value="about">ℹ About</TabsTrigger>
          </TabsList>
        </div>

        <TabsContent value="dashboard"><Dashboard state={state} /></TabsContent>
        <TabsContent value="eventlog"><EventLog state={state} /></TabsContent>
        <TabsContent value="settings"><Settings state={state} onSetLang={setLanguage} /></TabsContent>
        <TabsContent value="about"><About /></TabsContent>
      </Tabs>
    </div>
  )
}
