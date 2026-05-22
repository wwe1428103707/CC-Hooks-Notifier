import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"

// ── Types shared with C# backend (WebBridge protocol) ──────────────
interface StatCounts { total: number; p0: number; p05: number; toast: number; stateful: number }
interface EventEntry { timestamp: string; level: string; eventName: string; summary: string }
interface StatePayload { counts: StatCounts; subagentCount: number; taskCount: number; recentEvents: EventEntry[]; allEvents: EventEntry[]; language: string }

// ── Dashboard Tab ──────────────────────────────────────────────────
function Dashboard({ state }: { state: StatePayload }) {
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
        {cards.map(c => (
          <Card key={c.title} className={c.accent + " border-t-3"}>
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
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Recent Events</CardTitle>
        </CardHeader>
        <CardContent className="px-4 pb-4">
          {state.recentEvents.length === 0 ? (
            <p className="text-sm text-muted-foreground">(no events yet)</p>
          ) : (
            <div className="space-y-1">
              {state.recentEvents.map((e, i) => (
                <div key={i} className="flex items-center gap-2 text-sm font-mono">
                  <span>{levelIcon(e.level)}</span>
                  <span className="text-muted-foreground w-16 shrink-0">[{e.timestamp}]</span>
                  <Badge variant="outline" className="w-22 shrink-0 font-mono text-xs">{e.eventName}</Badge>
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

// ── Event Log Tab ──────────────────────────────────────────────────
function EventLog({ state }: { state: StatePayload }) {
  const levelColor = (lvl: string) => {
    switch (lvl) {
      case "P0": return "text-red-600"
      case "P0.5": return "text-orange-500"
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
            ) : state.allEvents.slice().reverse().map((e, i) => (
              <tr key={i} className="border-t hover:bg-muted/30">
                <td className="px-3 py-1.5 text-muted-foreground">{e.timestamp}</td>
                <td className={`px-3 py-1.5 font-semibold ${levelColor(e.level)}`}>{e.level}</td>
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

// ── Settings Tab ───────────────────────────────────────────────────
function Settings({ state }: { state: StatePayload }) {
  return (
    <div className="p-6 space-y-4 max-w-xl">
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Language</CardTitle></CardHeader>
        <CardContent>
          <select className="flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-xs">
            <option value="en" selected={state.language === "en"}>English</option>
            <option value="zh" selected={state.language === "zh"}>中文</option>
          </select>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Auto-start</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs text-muted-foreground">Start Claude Code Hooks Notifier when I log in</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="pb-2"><CardTitle className="text-sm">Hook Executable Path</CardTitle></CardHeader>
        <CardContent>
          <p className="text-xs font-mono text-muted-foreground">(connected to C# backend)</p>
        </CardContent>
      </Card>
    </div>
  )
}

// ── About Tab ──────────────────────────────────────────────────────
function About() {
  const coverage = [
    { level: "P0 🔔", type: "Long blink (10s)", events: "idle_prompt, permission_prompt, StopFailure" },
    { level: "P0.5 🔔", type: "Short blink (5s)", events: "Stop, TaskCompleted, SessionEnd" },
    { level: "P1 📢", type: "Toast", events: "auth_success, elicitation, PermissionDenied, PostToolUse" },
    { level: "P2 🟢", type: "Stateful", events: "SubagentStart, TaskCreated, PreCompact" },
  ]

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
        <p className="text-sm text-muted-foreground">Tech Stack: React + shadcn/ui + WebView2 + C# .NET 9</p>
        <p className="text-sm">Windows system tray notification service for Claude Code hooks.</p>
        <Separator />
        <h3 className="text-sm font-semibold">Hook Event Coverage</h3>
        <table className="w-full text-xs">
          <thead><tr className="text-muted-foreground"><th className="text-left py-1">Level</th><th className="text-left py-1">Type</th><th className="text-left py-1">Events</th></tr></thead>
          <tbody>{coverage.map(c => <tr key={c.level} className="border-t"><td className="py-1.5 font-mono">{c.level}</td><td className="py-1.5 text-muted-foreground">{c.type}</td><td className="py-1.5 text-muted-foreground text-[11px]">{c.events}</td></tr>)}</tbody>
        </table>
      </Card>
    </div>
  )
}

// ── Separator component (inline since it's small) ──────────────────
function Separator() { return <div className="border-t my-2" /> }

// ── App Root ───────────────────────────────────────────────────────
export default function App() {
  // Phase 2: this will receive state from C# via WebBridge.
  // For now, use empty defaults for dev server testing.
  const state: StatePayload = {
    counts: { total: 0, p0: 0, p05: 0, toast: 0, stateful: 0 },
    subagentCount: 0, taskCount: 0,
    recentEvents: [], allEvents: [],
    language: "en",
  }

  return (
    <div className="min-h-screen bg-[#f5f7fa]">
      {/* Header */}
      <header className="bg-[#212529] text-white h-12 flex items-center px-5 gap-2">
        <span className="text-lg font-bold">⚡ Claude Code Hooks Notifier</span>
        <span className="text-xs text-gray-400 pt-0.5">v1.4.0</span>
      </header>

      {/* Tabs */}
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
        <TabsContent value="settings"><Settings state={state} /></TabsContent>
        <TabsContent value="about"><About /></TabsContent>
      </Tabs>
    </div>
  )
}
