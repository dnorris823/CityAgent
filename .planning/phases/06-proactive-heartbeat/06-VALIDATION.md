---
phase: 6
slug: proactive-heartbeat
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-28
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Manual in-game verification (CS2 mod — no automated test runner) |
| **Config file** | none — no test framework installed |
| **Quick run command** | `cd src && dotnet build -c Release` |
| **Full suite command** | Build + deploy + launch CS2 + behavioral checklist |
| **Estimated runtime** | ~15 seconds (build only); ~10 minutes (full in-game) |

---

## Sampling Rate

- **After every task commit:** Run `cd src && dotnet build -c Release`
- **After every plan wave:** Build + deploy + minimal in-game smoke (panel shows heartbeat message after interval)
- **Before `/gsd:verify-work`:** Full behavioral checklist below must be green
- **Max feedback latency:** 15 seconds (build); ~10 min (in-game wave smoke)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| Settings fields | 01 | 1 | HB-02 | compile | `dotnet build -c Release` | ✅ | ⬜ pending |
| HeartbeatSystem scaffold | 02 | 1 | HB-01 | compile | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| Timer + in-flight guard | 02 | 1 | HB-01 | compile + in-game | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| Screenshot conflict check | 02 | 2 | HB-01 | compile | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| UISystem drain integration | 03 | 2 | HB-01 | compile + in-game | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| Silence gate (IsHeartbeatSilent) | 03 | 2 | HB-01 | compile | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| Backoff counter | 02 | 2 | HB-03 | compile + log inspection | `dotnet build -c Release` | ❌ W0 | ⬜ pending |
| Mod.cs scheduling | 01 | 1 | HB-01 | compile | `dotnet build -c Release` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- No automated test infrastructure exists for CS2 mods — all behavioral validation is manual in-game
- [ ] `cd src && dotnet build -c Release` must succeed (zero errors) before any in-game verification

*Existing infrastructure covers all automated verification possible in this context (build compile gate).*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Heartbeat message appears in panel after interval without player input | HB-01 | Requires CS2 running with city loaded | Enable heartbeat (5 min default), wait full interval, confirm advisor bubble appears |
| `m_HeartbeatInFlight` prevents double-fire | HB-01 | Requires log inspection during API call | Trigger slow API response, check CS2 log for no second `[HeartbeatSystem] firing` |
| `[silent]` suppression — no empty bubble | HB-01 | Requires in-game observation | Observe panel across multiple heartbeat cycles; no empty assistant bubbles |
| `HeartbeatEnabled=false` default fires nothing | HB-02 | Requires fresh game load | Launch CS2, wait > 5 min, confirm no advisor messages without enabling heartbeat |
| Enable at runtime → first fire after full interval | HB-02 | Requires game running | Enable in settings mid-session, record timestamp, confirm fire after N minutes |
| Change interval at runtime → new interval applies | HB-02 | Requires game running | Change from 5→2 min while running, confirm fire at new cadence |
| One message per cycle with multiple city issues | HB-03 | Requires a city with active issues | Use a city with traffic, budget, demand problems; confirm single aggregated message |
| API error → 3-cycle backoff, silent | HB-03 | Requires broken API key or network off | Temporarily invalidate API key, observe log shows no flood, panel shows no error, resumes after 3 cycles |

---

## Validation Sign-Off

- [ ] All tasks compile cleanly (`dotnet build -c Release` exits 0)
- [ ] Sampling continuity: build gate after each task
- [ ] Wave 0 note acknowledged: no automated behavioral tests possible in CS2 mod context
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s (build gate)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
