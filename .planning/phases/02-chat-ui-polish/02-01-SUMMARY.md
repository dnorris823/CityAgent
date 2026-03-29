---
phase: 02-chat-ui-polish
plan: "01"
subsystem: C# data layer
tags: [chat, error-handling, api-filtering, thinking-blocks]
dependency_graph:
  requires: []
  provides: [role=system error messages, thinking-block stripping, D-04 filter marker]
  affects: [CityAgentUISystem, ClaudeAPISystem, React chat render pipeline]
tech_stack:
  added: []
  patterns: [System.Text.RegularExpressions inline (no new using), Interlocked.Exchange drain]
key_files:
  created: []
  modified:
    - src/Systems/CityAgentUISystem.cs
    - src/Systems/ClaudeAPISystem.cs
decisions:
  - "Use fully qualified System.Text.RegularExpressions.Regex inline — no new using directive needed since System is already imported"
  - "D-04 expressed as a comment marker rather than dead code — history iteration does not yet exist in RunClaudeRequestAsync"
metrics:
  duration_seconds: 78
  tasks_completed: 2
  tasks_total: 2
  files_modified: 2
  completed_date: "2026-03-28"
---

# Phase 02 Plan 01: Data-Layer Foundation for UI Polish Summary

**One-liner:** Errors from ClaudeAPISystem are now promoted to `role="system"` in chat history, and `<thinking>` blocks are stripped before display; the D-04 filter marker documents where the API payload must exclude system-role messages when history is wired in.

## What Was Done

### Task 1 — CityAgentUISystem: strip `<thinking>` and promote errors to system role

The PendingResult drain block in `OnUpdate` was updated to:

1. Strip `<thinking>...</thinking>` blocks from the result using a case-insensitive regex before anything is stored. This handles extended-thinking model output (D-18) — the raw XML never reaches the React panel.
2. Promote `[Error]:` prefixed strings to `role = "system"` instead of `role = "assistant"`. This ensures API errors appear as center-pill notices (rendered by Plan 02) rather than assistant chat bubbles (D-05).

The `Interlocked.Exchange` pattern already in place was preserved unchanged.

**Commit:** `67789b1` — `feat(02-01): strip <thinking> blocks and promote errors to system role`

### Task 2 — ClaudeAPISystem: document D-04 system-role filter marker

A comment was added immediately before the `messages` list construction in `RunClaudeRequestAsync` documenting the D-04 requirement: when Phase 2 wires `m_History` into the API payload, any entries with `role == "system"` must be excluded (those are UI-only notice pills, not conversation turns).

The current file does not iterate `m_History` at all — the messages list starts fresh with only the current user message. The comment serves as a definitive insertion-point marker for the Phase 2 implementor, with an example filter pattern.

**Commit:** `115f7c4` — `chore(02-01): document D-04 system-role filter marker in ClaudeAPISystem`

## Deviations from Plan

### Plan interface reference vs. actual code

The plan's `<interfaces>` block showed the old drain pattern (direct assignment `m_ClaudeAPI.PendingResult = null`), but the actual file already used `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)` from a prior D-14 fix. The new code was inserted into the correct Interlocked.Exchange pattern that was already in place — no issue, same logical block.

This is informational only — the change applied cleanly and correctly.

## Build Verification

- `dotnet build -c Release` passed with 0 errors, 0 warnings after both tasks.
- Note: `CS2_INSTALL_PATH` must be passed explicitly in git-bash (the Windows user-level env var has a line-break artifact in the bash environment); the project's `.csproj` fallback default path also resolves correctly.

## Known Stubs

None. This plan makes no UI-visible data changes — it only shapes the data that flows to React. The `role="system"` messages will remain invisible until Plan 02 adds the center-pill renderer.

## Self-Check: PASSED

- `src/Systems/CityAgentUISystem.cs` contains `result.StartsWith("[Error]:")` — confirmed
- `src/Systems/CityAgentUISystem.cs` contains `thinking` regex pattern — confirmed
- `src/Systems/CityAgentUISystem.cs` contains `string role =` in drain block — confirmed
- Original verbatim `role = "assistant", content = result` line no longer exists — confirmed
- `src/Systems/ClaudeAPISystem.cs` contains `D-04` comment — confirmed
- Commits `67789b1` and `115f7c4` exist in git log — confirmed
