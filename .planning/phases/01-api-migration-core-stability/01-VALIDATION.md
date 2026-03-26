---
phase: 1
slug: api-migration-core-stability
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-26
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | No automated test suite — manual in-game validation only |
| **Config file** | none — no test project exists |
| **Quick run command** | `cd src && dotnet build -c Release` |
| **Full suite command** | Manual in-game: build → deploy → launch CS2 → exercise panel |
| **Estimated runtime** | ~30s build; ~5 min manual in-game |

---

## Sampling Rate

- **After every task commit:** Run `cd src && dotnet build -c Release`
- **After every plan wave:** Build passes + manual spot-check for that wave's behavior
- **Before `/gsd:verify-work`:** Full manual in-game pass against all success criteria
- **Max feedback latency:** 30s (build), 5 min (full manual)

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| Settings refactor | TBD | 1 | API-01, API-02 | Build + manual | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| ClaudeAPISystem rewrite | TBD | 2 | API-01, API-03, API-04 | Build + log inspection | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| Thread safety fixes | TBD | 2 | CORE-02 | Build + manual rapid-send | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| NarrativeMemory async | TBD | 2 | CORE-01 | Build + manual stutter test | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| Screenshot async | TBD | 2 | CORE-01, CORE-03 | Build + manual screenshot | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| End-to-end validation | TBD | 3 | CORE-03, API-01–04 | Manual in-game | — | — | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

No test framework to install. No Wave 0 needed.

*Existing build infrastructure covers automated feedback (dotnet build). All behavioral verification is manual in-game.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| No UI freeze on file write or screenshot encode | CORE-01 | No perf tooling in mod context — requires visual observation | Send message + screenshot; confirm no visible stutter or freeze |
| No duplicate requests on rapid send | CORE-02 | Race condition requires human timing | Click Send twice rapidly; check CS2 log for single outbound HTTP request |
| Claude responds with narrative via /v1/messages | API-01 | Live API call + in-game display | Send a message; confirm HTTP 200 in log and response in panel |
| Screenshot included and Claude describes it | CORE-03 | Vision requires live screenshot + API round-trip | Send with screenshot; ask Claude "what do you see?"; confirm visual description |
| Tool call fires and result in response | CORE-03 | Tool loop requires live game data + live API | Ask "how's my city doing?"; confirm population/demand data in response |
| Memory written on background thread | CORE-01 | File write timing requires no-stutter observation | Post-response: check memory file updated on disk; game didn't freeze |
| Ollama fallback fields in settings | API-02 | UI verification | Open mod settings; confirm "Ollama Fallback (optional)" section visible |
| Model change takes effect without restart | API-04 | Requires settings change + live send | Change Claude model in settings; send message; confirm new model in log |
| 429 → in-panel notice + Ollama retry | API-03 | Hard to trigger live; simulate in code | Temporarily force 429 return; confirm ⚠️ notice in panel + Ollama request in log |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify (build) or explicit manual instructions above
- [ ] Build passes after every wave before proceeding
- [ ] End-to-end in-game pass performed before marking phase complete
- [ ] No regressions in existing panel behavior (toggle, drag, resize, clear chat)
- [ ] `nyquist_compliant: true` set in frontmatter when all manual checks pass

**Approval:** pending
