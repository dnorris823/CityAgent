---
phase: 4
slug: web-search-tool
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-27
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None — CS2 mod; no automated test runner. Compile + in-game only. |
| **Config file** | none |
| **Quick run command** | `cd "C:/Coding Projects/CityAgent/Working/CityAgent/src" && dotnet build -c Release` |
| **Full suite command** | Build + deploy to mod folder + in-game verification (manual) |
| **Estimated runtime** | ~30 seconds (build); ~5 minutes (full in-game cycle) |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build -c Release` (zero compile errors)
- **After every plan wave:** Build + deploy + load CS2 + confirm settings section visible
- **Before `/gsd:verify-work`:** Full in-game test — ask Claude "how do cities reduce highway noise?" and confirm response cites a source retrieved via search
- **Max feedback latency:** ~30 seconds (compile) per task

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 4-01-xx | 01 | 1 | SRCH-02 | compile | `dotnet build -c Release` | ✅ existing | ⬜ pending |
| 4-02-xx | 02 | 1 | SRCH-01 | compile | `dotnet build -c Release` | ✅ existing | ⬜ pending |
| 4-03-xx | 03 | 2 | SRCH-01,03 | in-game manual | `dotnet build -c Release` (compile gate) | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

None — no new test infrastructure needed. Existing compile-then-in-game pattern covers all phase requirements.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `search_web(query)` calls Brave API and returns structured results | SRCH-01 | No test runner in CS2 mod environment | Load a city, ask Claude "how do cities reduce highway noise?" — confirm response references an external source |
| Brave Search API key field visible in mod settings | SRCH-02 | UI rendered by CS2's built-in settings system | Open Options → CityAgent → Web Search section — confirm `Brave Search API Key` field and `Web Search Enabled` toggle are present |
| Claude invokes search autonomously without player prompting | SRCH-03 | Requires live LLM decision-making | Ask Claude an urban planning question without explicitly saying "search for this" — confirm Claude calls `search_web` on its own |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s (compile gate per task)
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
