---
phase: 5
slug: memory-file-explorer
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-03-28
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | None — no jest/vitest/xunit; CS2 binding layer requires game runtime |
| **Config file** | none |
| **Quick run command** | `cd UI && npm run build` |
| **Full suite command** | `cd src && dotnet build -c Release` then `cd UI && npm run build` |
| **Estimated runtime** | ~30 seconds |

---

## Sampling Rate

- **After every task commit:** Run `cd UI && npm run build` (TypeScript type-check via ts-loader; catches binding type errors and JSX issues)
- **After every plan wave:** Run both `dotnet build -c Release` (C# compile) and `npm run build` (TS/CSS bundle)
- **Before `/gsd:verify-work`:** Both builds green + all 4 manual in-game tests pass
- **Max feedback latency:** ~30 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 5-01-* | 01 | 1 | MEM-01 | Build | `cd src && dotnet build -c Release` | ✅ | ⬜ pending |
| 5-02-* | 02 | 1 | MEM-01–04 | Build | `cd UI && npm run build` | ✅ | ⬜ pending |
| 5-03-* | 03 | 2 | MEM-01–04 | Manual in-game | See Manual Verifications | N/A | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No test framework setup needed.
`TriggerBinding<string,string>` confirmed present in live game DLL.

*No Wave 0 stubs needed.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Memory tab visible in panel header | MEM-01 | CS2 Coherent GT runtime required | Open panel in-game, verify "Advisor" and "Memory" tab buttons appear in header |
| File list renders on tab open | MEM-01 | C#↔JS binding requires live game | Click Memory tab, verify ≥1 file rows with name, size, relative time |
| Core files show lock icon, sorted first | MEM-01 | React rendering requires game context | Verify `_index.md` at top with lock; user files below alphabetically |
| File click shows content | MEM-02 | readMemoryFile trigger requires game runtime | Click `_index.md`, verify full markdown content renders in view area |
| Sub-header shows ← filename + Edit (no Delete for core) | MEM-02/04 | UI layout requires Coherent GT | Click core file; verify no Delete button; click non-core file; verify Delete present |
| Edit textarea pre-populated, Save persists | MEM-03 | File I/O requires game process | Click non-core file → Edit → modify text → Save; re-open file, verify change |
| Saved edit affects Claude's next response | MEM-03 | Requires full Claude API call chain | After editing `lore.md`, send a message asking about lore; verify Claude references new content |
| Delete core file is prevented | MEM-04 | DeleteFile C# guard + UI hide | Verify Delete button absent for `_index.md`; optionally fire raw trigger, verify "[Error]" |
| Delete non-core file: confirm → success → list refresh | MEM-04 | Full trigger round-trip | Delete `lore.md` (if exists), confirm in sub-header, verify navigates back and file is gone |
| Back button resets Memory tab state | MEM-01 | React state requires rendered component | Navigate into file view → back → re-open Memory tab → verify at file list |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
