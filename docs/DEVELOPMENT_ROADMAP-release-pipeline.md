# Development Roadmap: Release Pipeline + Phase 2 Enhancements

> Generated: 2026-05-25
> Based on: `REQUIREMENTS-release-pipeline.md` (2026-05-25) and `REQUIREMENTS-tray-notification-center.md` (2026-05-24)
> Current state: v1.12.0 (tray notification center Phase 1 already shipped)

---

## 1. Current Status Assessment

### Already Delivered in v1.12.0 (Notification Center Phase 1)

The following features from `REQUIREMENTS-tray-notification-center.md` are **already implemented** and require no further work:

| Requirement | Status | Evidence |
|-------------|--------|----------|
| FR-1.1 Persistent blink on new event | DONE | `TrayMode.StartBlinking()` — 500ms blue/orange toggle |
| FR-1.2 No auto-stop timer | DONE | No timer-based stop; only stops on user action |
| FR-1.3/1.4 Multiple-event blink handling | DONE | Blink idempotent; no reset/stack on repeated calls |
| FR-2.1 Tooltip with unread count | DONE | `TrayMode.UpdateTooltip()` shows `N unread notifications` |
| FR-2.2 Unread count update timing | DONE | Counts on new IPC, open dashboard, mark-all-read |
| FR-2.3 In-process counter only | DONE | `EventHistory.UnreadCount` — no panel dependency |
| FR-3.1 Single-click opens dashboard | DONE | `TrayMode.OpenDashboard()` — stops blink, marks read, opens/focuses |
| FR-3.2 Double-click matches click | DONE | Both events call `OpenDashboard()` |
| FR-3.3 Context menu "View Notifications" | DONE | `_unreadMenuItem` with dynamic unread count |
| FR-4.1 Read/unread dot in EventLog | DONE | `App.tsx` — `unreadDot()` renders colored circle; `isRead` passed in payload |
| FR-4.2 Unread-first sort | DONE | `EventLog` `useMemo` sort: unread before read |
| FR-4.3 Filter tabs (All/Unread/P0/P0.5/Toast) | DONE | `filters` array in `EventLog` component |
| FR-4.4 Event detail expand | DONE | `expanded` state toggles detail row |
| FR-4.5 Unread badge + "Mark All Read" | DONE | Badge button in EventLog header |
| FR-5.1 `EventEntry.IsRead` field | DONE | `record EventEntry(..., bool IsRead = false)` |
| FR-5.2 Persistence to event_history.json | DONE | Included in `SaveToFile()` serialization |
| FR-5.3 Mark-read triggers (click, button, expand) | DONE | `mark_all_read` / `mark_read` IPC handlers in `MainWindow.cs` |
| FR-5.4 Unread cap at 999 | DONE | `UpdateTooltip()` shows `999+` above threshold |
| EventEntry + EventHistory new methods | DONE | `IsRead`, `UnreadCount`, `MarkAllRead`, `MarkRead`, `GetUnread` |
| Dashboard unread card | DONE | `{ title: "Unread", value: state.unreadCount }` |
| C#-JS unread communication | DONE | `PushEvent` + `GetCurrentState` send `isRead` and `unreadCount` |
| IPC Ping/Pong | NOT DONE | Required for smoke testing; `TrayMode.OnIpcMessage` missing `ping` case |

### Still To Do

| Area | Item | Priority |
|------|------|----------|
| Release Pipeline | `scripts/release.ps1` (all 8 stages) | HIGH |
| Release Pipeline | `scripts/verify-release.ps1` (smoke test) | HIGH |
| Release Pipeline | IPC Ping/Pong in TrayMode.cs | HIGH |
| Release Pipeline | `CHANGELOG.md` creation | HIGH |
| Release Pipeline | `.github/workflows/build.yml` (CI/CD) | HIGH |
| Release Pipeline | CLAUDE.md + publish.ps1 updates | MEDIUM |
| Notification Center P2.1 | GDI+ badge number on tray icon | LOW |

---

## 2. Task Decomposition

### L0 — Foundation (no dependencies)

| ID | Task | Files | Est. | Dependencies |
|----|------|-------|------|-------------|
| L0.1 | Create `scripts/release.ps1` skeleton | `scripts/release.ps1` | M | None |
| L0.2 | Implement preflight checks module | `scripts/release.ps1` | M | L0.1 |
| L0.3 | Implement version-stamping module (5 files + verify) | `scripts/release.ps1` | M | L0.1 |
| L0.4 | Create `scripts/ReleaseInfo.ps1` class | `scripts/ReleaseInfo.ps1` | S | L0.1 |
| L0.5 | IPC Ping/Pong in TrayMode | `src/HooksNotifier/TrayMode.cs` | S | None |
| L0.6 | CHANGELOG.md initial creation (historical entries) | `CHANGELOG.md` | S | None |

### L1 — Build & Verify (depends on L0)

| ID | Task | Files | Est. | Dependencies |
|----|------|-------|------|-------------|
| L1.1 | Full build orchestration (npm ci, dotnet publish x2) | `scripts/release.ps1` | M | L0.1 |
| L1.2 | Artifact verification (setup.iss File parsing + existence) | `scripts/release.ps1` | M | L0.1, L1.1 |
| L1.3 | Installer build + verification | `scripts/release.ps1` | S | L1.2 |
| L1.4 | Create `scripts/verify-release.ps1` | `scripts/verify-release.ps1` | M | L0.5 |

### L2 — Changelog & Commit (depends on L1)

| ID | Task | Files | Est. | Dependencies |
|----|------|-------|------|-------------|
| L2.1 | Changelog auto-generation (git log parse + classify) | `scripts/release.ps1` | M | L0.5 |
| L2.2 | Commit + tag automation | `scripts/release.ps1` | S | L2.1 |
| L2.3 | End-to-end dry-run validation | Manual | S | L2.1, L2.2 |

### L3 — CI/CD (depends on L2)

| ID | Task | Files | Est. | Dependencies |
|----|------|-------|------|-------------|
| L3.1 | Create `.github/workflows/build.yml` | `.github/workflows/build.yml` | M | L2.2 |
| L3.2 | GitHub Release on tag push | `.github/workflows/build.yml` | S | L3.1 |
| L3.3 | End-to-end CI dry run on fork | Manual | M | L3.2 |

### P2 — Phase 2 Enhancements (independent)

| ID | Task | Files | Est. | Dependencies |
|----|------|-------|------|-------------|
| P2.1 | GDI+ badge number on tray icon | `src/HooksNotifier/IconHelper.cs` | M | None |

---

## 3. Dependency Graph (ASCII)

```
                    ┌──────────────┐
                    │   L0.5 IPC   │
                    │  Ping/Pong   │
                    └──────┬───────┘
                           │
┌────────────┐    ┌───────┴────────┐    ┌────────────┐
│   L0.1     │    │   L0.6         │    │   P2.1     │
│ release.ps1│    │  CHANGELOG.md  │    │  Badge icon│
│ skeleton   │    │  (historical)  │    │ (standalone│
└─────┬──────┘    └────────────────┘    └────────────┘
      │
      ├─────────────────────────────────────┐
      │                                     │
┌─────┴──────┐                      ┌───────┴───────┐
│   L0.2     │                      │    L0.3       │
│ Preflight  │                      │ Version stamp │
│ checks     │                      │ (5 files)     │
└─────┬──────┘                      └───────┬───────┘
      │                                     │
      └──────────┬──────────┬────────────────┘
                 │          │
                 │   L0.4   │
                 │ReleaseInfo│
                 └────┬─────┘
                      │
          ┌───────────┴───────────┐
          │                       │
    ┌─────┴──────┐         ┌──────┴──────┐
    │   L1.1     │         │   L1.4      │
    │ Build      │         │verify-release│
    │ orchestrate│         │.ps1         │
    └─────┬──────┘         └──────┬──────┘
          │                       │
    ┌─────┴──────┐                │
    │   L1.2     │                │
    │ Artifact   │                │
    │ verify     │                │
    └─────┬──────┘                │
          │                       │
    ┌─────┴──────┐                │
    │   L1.3     │                │
    │ Installer  │                │
    └─────┬──────┘                │
          │                       │
    ┌─────┴──────┐                │
    │   L2.1     │◄───────────────┘
    │ Changelog  │
    │ auto-gen   │
    └─────┬──────┘
          │
    ┌─────┴──────┐
    │   L2.2     │
    │ Commit+tag │
    └─────┬──────┘
          │
    ┌─────┴──────┐      ┌────────────┐
    │   L3.1     │      │   L3.2     │
    │ build.yml  │      │ GH Release │
    │ (push/PR)  │      │ (tag push) │
    └─────┬──────┘      └─────┬──────┘
          │                   │
          └───────┬───────────┘
                  │
          ┌───────┴───────┐
          │   L3.3        │
          │  E2E CI test  │
          └───────────────┘
```

### Parallel Paths

**Independent (can run concurrently):**
- `L0.1` (skeleton) + `L0.5` (IPC Ping) + `L0.6` (CHANGELOG) + `P2.1` (badge icon)
- `L0.2` (preflight) + `L0.3` (version stamp) — both write into `release.ps1`, must be sequential or merged carefully
- `L1.1` (build) + `L1.4` (verify-release) — no file conflict
- `L3.1` (push/PR workflow) + `L3.2` (tag workflow) — merge into same YAML

**Sequential (blocked):**
- L0.1 -> L0.2 -> L0.3 -> L1.1 -> L1.2 -> L1.3 (linear, same file)
- L2.1 -> L2.2 (changelog feeds commit)
- L3.1 -> L3.2 -> L3.3 (CI workflow chain)

---

## 4. Parallelization Strategy

### Interface-First Approach

The C# change (L0.5, IPC Ping/Pong) and the release script (L0.1-L0.4) are completely independent. They touch different files/subsystems. A single developer can interleave them freely; two developers can take one each.

### Recommended: 1 Developer (Full-Stack)

The release pipeline work is predominantly PowerShell scripting with one small C# change. The task graph is mostly linear (same file `release.ps1` is touched by 4+ tasks). A single developer is optimal and avoids context-switching overhead.

Estimated total: **~18-22 hours** over 3-4 days.

### Alternative: 2 Developers (With Careful Partitioning)

| Developer | Tasks | Rationale |
|-----------|-------|-----------|
| Dev A (PowerShell) | L0.1, L0.2, L0.3, L0.4, L1.1, L1.2, L1.3, L2.1, L2.2 | Owns `release.ps1` end-to-end |
| Dev B (C# + Frontend + CI) | L0.5, L0.6, L1.4, L3.1, L3.2, P2.1 | IPC, verify-release, CI/CD, phase 2 |

**Merge point at L2.1**: `verify-release.ps1` (Dev B) must be complete before changelog auto-gen (Dev A) in the full pipeline, but can be tested standalone.

**Risks of 2-dev:**
- `release.ps1` is a single file that grows to 400-600 lines; parallel edits risk merge conflicts
- `verify-release.ps1` depends on IPC Ping (L0.5), which is a trivial change (one `case "ping"` block) but has a delivery dependency
- Net time savings: ~4-6 hours (not a 2x speedup due to serial bottlenecks)

### Recommendation: 1 Developer, but parallelize P2.1 (badge icon) as a separate spike

The badge icon (P2.1) is completely independent and can be done at any time by a second person or as a future task.

---

## 5. Testing Strategy

### 5.1 Unit Test Cases

The project has no formal test project. For the release pipeline, the primary testing mechanism will be `-WhatIf` mode and manual dry-run.

| Test Case | What to Verify | How |
|-----------|---------------|-----|
| Version stamping accuracy | All 5 files updated; old version no longer present | `release.ps1 -Version "9.99.99" -WhatIf` then grep |
| Preflight workspace dirty | Script rejects dirty workspace | `touch test.txt && release.ps1 -Version "9.99.99"` (should fail) |
| Preflight tool missing | Clear error message per missing tool | Temporarily rename dotnet/node and run |
| ISCC fallback paths | Script finds ISCC via fallback path | Remove ISCC from PATH; verify it finds `Program Files` |
| Existing tag conflict | Script warns on duplicate version | Run with version equal to latest tag |
| Build order | WebUI before NotifyHook before HooksNotifier | Check stdout order in `-WhatIf` mode |
| Artifact missing detection | Script stops with file list | Delete `bin/HooksNotifier.exe` before L1.2 |
| Changelog classification | `feat:` -> Added, `fix:` -> Fixed, etc. | Run on known git log and verify output |
| --WhatIf safety | No files modified, no git changes | `release.ps1 -Version "9.99.99" -WhatIf` then `git status --porcelain` |

### 5.2 Integration Test Scenarios

| Scenario | Steps | Expected Result |
|----------|-------|-----------------|
| Full release dry run | `release.ps1 -Version "9.99.99" -NoCommit -SkipSmoke` | All 7 stages complete; no git changes |
| Full release with smoke | `release.ps1 -Version "9.99.99" -NoCommit` | IPC ping succeeds; tray process starts and stops cleanly |
| Bad version stamp | Manually corrupt one file, then run | Version stamping should fix it; stale version scan warns |
| Force dirty workspace | Modify file, then run `-Force` | Script proceeds past dirty check |
| verify-release.ps1 only | `verify-release.ps1` standalone | Process start -> IPC ping -> IPC notify -> terminate, all pass |

### 5.3 Regression Checklist

Before cutting a release, verify:

- [ ] `hooks-notifier.exe --tray` starts without crash
- [ ] System tray bell icon appears
- [ ] Right-click context menu displays correctly
- [ ] "View Notifications" shows correct unread count
- [ ] Tooltip shows correct unread count
- [ ] Blinking starts on new IPC `blinkType: "long"` message
- [ ] Single-click stops blink, marks all read, opens dashboard
- [ ] Dashboard EventLog shows unread dots
- [ ] "Mark All Read" clears unread dots and resets tooltip
- [ ] Dashboard unread counter card reflects actual count
- [ ] Filter tabs work (All/Unread/P0/P0.5/Toast)
- [ ] Event detail expand works
- [ ] Language switch (EN/ZH) works in both tray menu and dashboard
- [ ] `hooks-notify.exe` piping works: `echo '{"hook_event_name":"StopFailure"}' | bin/hooks-notify.exe`
- [ ] IPC stateful messages update dashboard counters
- [ ] `event_history.json` persistence survives restart
- [ ] Installer (`ISCC.exe setup.iss`) builds successfully
- [ ] Installed app runs correctly from `{localappdata}\Programs\ClaudeHooksNotifier`

---

## 6. Timeline

### Task Size Legend

| Size | Hours | Description |
|------|-------|-------------|
| S (Small) | 1-2 | Single function, well-understood |
| M (Medium) | 3-5 | Module with branching logic |
| L (Large) | 6-10 | Complex multi-step feature |

### Estimated Hours

| ID | Task | Size | Hours | Notes |
|----|------|------|-------|-------|
| L0.1 | release.ps1 skeleton | M | 4 | Parameter parsing, stage framework, logging, error handling |
| L0.2 | Preflight checks | M | 3 | git status, tool detection, ISCC fallback, tag check |
| L0.3 | Version stamping (5 files) | M | 5 | Regex replacement, consistency check, stale version scan |
| L0.4 | ReleaseInfo.ps1 class | S | 1 | PowerShell class with version/tag/commit tracking |
| L0.5 | IPC Ping/Pong | S | 1 | One `case "ping"` block in TrayMode.cs |
| L0.6 | CHANGELOG.md initial | S | 1 | Historical entries for v1.0.0 through v1.12.0 |
| L1.1 | Build orchestration | M | 3 | npm ci, dotnet publish x2, timing |
| L1.2 | Artifact verification | M | 3 | Parse setup.iss [Files], check existence + version |
| L1.3 | Installer build + verify | S | 1 | ISCC execution and output validation |
| L1.4 | verify-release.ps1 | M | 4 | Process lifecycle, IPC ping/notify, timeouts |
| L2.1 | Changelog auto-generation | M | 3 | git log parsing, CC classification, preview |
| L2.2 | Commit + tag | S | 1 | git add/commit/tag, summary output |
| L2.3 | E2E dry-run validation | S | 2 | Run full pipeline, fix issues |
| L3.1 | build.yml (push/PR) | M | 3 | GitHub Actions workflow, 3 triggers |
| L3.2 | GH Release on tag | S | 1 | Release creation with artifact upload |
| L3.3 | E2E CI test | M | 3 | Push to fork, verify CI passes |
| P2.1 | Badge number icon | M | 4 | GDI+ number rendering, 16x16/32x32 variants |

**Total estimated: ~43 hours**

### Gantt Chart — 1 Developer

```
Day 1 (8h)     Day 2 (8h)     Day 3 (8h)     Day 4 (8h)     Day 5 (8h)
┌──────────────┬──────────────┬──────────────┬──────────────┬──────────────┐
L0.1 (4h)      │              │              │              │              │
├──────┤       │              │              │              │              │
L0.5 (1h)      │              │              │              │              │
├─┤            │              │              │              │              │
L0.6 (1h)      │              │              │              │              │
├─┤            │              │              │              │              │
     L0.2 (3h) │              │              │              │              │
     ├─────┤   │              │              │              │              │
          L0.3(5h)            │              │              │              │
          ├──────────┤         │              │              │              │
               L0.4 (1h)      │              │              │              │
               ├─┤            │              │              │              │
                    L1.1 (3h) │              │              │              │
                    ├─────┤   │              │              │              │
                         L1.2(3h)            │              │              │
                         ├─────┤              │              │              │
                              L1.3 (1h)      │              │              │
                              ├─┤             │              │              │
                                   L1.4 (4h) │              │              │
                                   ├──────┤  │              │              │
                                        L2.1(3h)           │              │
                                        ├─────┤             │              │
                                             L2.2 (1h)     │              │
                                             ├─┤            │              │
                                                  L2.3(2h) │              │
                                                  ├───┤    │              │
                                                       L3.1(3h)           │
                                                       ├─────┤             │
                                                            L3.2(1h)       │
                                                            ├─┤            │
                                                                 L3.3(3h)  │
                                                                 ├─────┤   │
```
**1-dev total: ~5 days**

### Gantt Chart — 2 Developers

```
Dev A (PowerShell focus):
Day 1             Day 2             Day 3
L0.1 (4h)
├──────┤
     L0.2 (3h)
     ├─────┤
          L0.3 (5h)
          ├──────────┤
               L0.4 (1h)   L1.1 (3h)
               ├─┤         ├─────┤
                                L1.2(3h)   L1.3(1h)   L2.1(3h)   L2.2(1h)
                                ├─────┤    ├─┤         ├─────┤    ├─┤

Dev B (C# + CI + Phase 2):
Day 1             Day 2             Day 3
L0.5 (1h)   P2.1 (4h)   L0.6 (1h)
├─┤         ├───────┤    ├─┤
     L1.4 (4h)
     ├──────┤
                    L3.1 (3h)   L3.2 (1h)   L3.3 (3h)
                    ├─────┤    ├─┤          ├──────┤
```
**2-dev total: ~3 days** (saves ~2 days vs 1-dev)

### Critical Path

The serial bottleneck is `release.ps1` development: L0.1 -> L0.2 -> L0.3 -> L1.1 -> L1.2 -> L1.3 -> L2.1 -> L2.2. This is ~21 hours of sequential work. Everything else (IPC, verify-release.ps1, CI/CD, CHANGELOG, badge icon) can be parallelized around this spine.

---

## 7. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `release.ps1` version regex matches wrong location in files | Medium | High | Test with `-WhatIf` first; verify with `grep` consistency check |
| ISCC.exe not found in CI (license restriction) | Medium | High | Pre-install ISCC in runner; document Windows-only constraint |
| `verify-release.ps1` IPC ping fails on headless CI | Medium | Medium | Add `-Headless` mode (no tray icon check, just pipe + process) |
| publish.ps1 users confused by new workflow | Low | Medium | Keep publish.ps1 with deprecation comment directing to release.ps1 |
| npm ci fails on Windows path-length limit | Low | Low | Enable `git config core.longpaths true` in CI |
| .NET 9 SDK not available on `windows-latest` runner | Low | High | Pin `actions/setup-dotnet@v4` with explicit `dotnet-version: '9.0.x'` |
| GDI+ badge number unreadable at 16x16 | High | Low | Phase 2 optional; can skip or limit to 32x32 only |

---

## 8. Recommended Sprint Plan

### Sprint 1 (Days 1-3): Core Pipeline

| Day | Focus | Deliverables |
|-----|-------|-------------|
| Day 1 AM | release.ps1 skeleton + preflight | `-Version` param, stage framework, tool checks, dirty workspace check |
| Day 1 PM | Version stamping + ReleaseInfo.ps1 | 5-file update, consistency check, `-WhatIf` protection |
| Day 2 AM | Build orchestration + artifact verification | npm ci, dotnet publish x2, setup.iss [Files] parsing |
| Day 2 PM | Installer build + verify-release.ps1 | ISCC execution, IPC Ping/Pong, smoke test script |
| Day 3 AM | CHANGELOG + commit automation | git log parsing, CC classification, git commit/tag |
| Day 3 PM | E2E dry-run + bug fixes | Full pipeline run, fix edge cases |

### Sprint 2 (Days 4-5): CI/CD + Polish

| Day | Focus | Deliverables |
|-----|-------|-------------|
| Day 4 AM | GitHub Actions build.yml | 3-trigger workflow, artifact upload |
| Day 4 PM | GitHub Release on tag | Release creation, EXE attachment |
| Day 5 AM | E2E CI test on fork | Verify green CI run, fix failures |
| Day 5 PM | Phase 2 badge icon (if time) | GDI+ number rendering |

---

## Key File Paths

- `D:\CC Hooks Notifier\scripts\release.ps1` — Main release orchestration script (to be created)
- `D:\CC Hooks Notifier\scripts\verify-release.ps1` — Smoke test script (to be created)
- `D:\CC Hooks Notifier\scripts\ReleaseInfo.ps1` — PowerShell release info class (to be created)
- `D:\CC Hooks Notifier\CHANGELOG.md` — Changelog file (to be created)
- `D:\CC Hooks Notifier\.github\workflows\build.yml` — CI/CD workflow (to be created)
- `D:\CC Hooks Notifier\src\HooksNotifier\TrayMode.cs` — Add `case "ping"` for IPC Ping/Pong (line ~172)
- `D:\CC Hooks Notifier\src\HooksNotifier\IconHelper.cs` — GDI+ badge number drawing (Phase 2)
- `D:\CC Hooks Notifier\publish.ps1` — Deprecation comment to add (line 1)
- `D:\CC Hooks Notifier\CLAUDE.md` — Build/version section update (lines 41-64)
