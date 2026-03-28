---
phase: 3
slug: extended-city-data-tools
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-27
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | dotnet build (C# compile-time) + manual in-game verification |
| **Config file** | src/CityAgent.csproj |
| **Quick run command** | `cd src && dotnet build -c Release` |
| **Full suite command** | `cd src && dotnet build -c Release` (+ in-game smoke test) |
| **Estimated runtime** | ~15 seconds (build only) |

> Note: CS2 mods run inside Unity — there is no unit test framework. Automated verification is build success + type-check. Behavioral verification is manual in-game.

---

## Sampling Rate

- **After every task commit:** Run `cd src && dotnet build -c Release`
- **After every plan wave:** Run full build + in-game load test
- **Before `/gsd:verify-work`:** Full build green + in-game ECS data confirmed
- **Max feedback latency:** ~15 seconds (build)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | Status |
|---------|------|------|-------------|-----------|-------------------|--------|
| 3-01-* | 01 | 1 | DATA-01 | build | `dotnet build -c Release` | ⬜ pending |
| 3-02-* | 02 | 1 | DATA-02 | build | `dotnet build -c Release` | ⬜ pending |
| 3-03-* | 03 | 1 | DATA-03/04 | build | `dotnet build -c Release` | ⬜ pending |
| 3-04-* | 04 | 2 | DATA-05 | build | `dotnet build -c Release` | ⬜ pending |
| 3-05-* | 05 | 3 | ALL | manual | in-game verification | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements — no new test files needed. C# build validation is the primary automated gate.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| `get_budget` returns real ECS numbers | DATA-01 | Requires running CS2 with a loaded city | Build → deploy → load city → ask Claude "what are my city finances?" → verify specific numbers appear |
| `get_traffic_summary` returns congestion data | DATA-02 | Requires running CS2 with roads | Load city → ask Claude "how is traffic?" → verify flow/bottleneck data present |
| `get_services_summary` returns coverage data | DATA-03 | Requires CS2 with service buildings | Load city → ask Claude "how are my city services?" → verify health/education/deathcare coverage |
| Tool toggles filter API calls | DATA-05 | Requires settings UI + CS2 + API inspection | Disable a tool in settings → send message → verify disabled tool not in API request |
| Disabled tool name not in tools array | DATA-05 | Requires API log inspection | Check mod log after sending message with tool disabled |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
