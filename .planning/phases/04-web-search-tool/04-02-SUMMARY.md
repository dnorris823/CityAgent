---
phase: 04-web-search-tool
plan: "02"
subsystem: api-system, settings
tags: [brave-search, tool-registration, system-prompt, web-search]
dependency_graph:
  requires: [04-01 (SearchWebTool.cs, Settings.BraveSearchApiKey, Settings.WebSearchEnabled)]
  provides: [ClaudeAPISystem conditional SearchWebTool registration, DefaultSystemPrompt web search guidance]
  affects: [in-game tool availability, Claude system prompt]
tech_stack:
  added: []
  patterns: [conditional tool registration at OnCreate, DefaultSystemPrompt append pattern]
key_files:
  created: []
  modified:
    - src/Systems/ClaudeAPISystem.cs
    - src/Settings.cs
decisions:
  - "Registration at OnCreate() time — user must restart the game session after adding a Brave key (same pattern as Claude API key)"
  - "Web search paragraph appended to Phase 3 DefaultSystemPrompt — not dynamically injected at request time (D-14)"
metrics:
  duration: 5min
  completed: 2026-03-30
  tasks_completed: 2
  files_modified: 2
---

# Phase 4 Plan 2: ClaudeAPISystem Registration and System Prompt Extension Summary

**One-liner:** SearchWebTool conditionally registered in ClaudeAPISystem.OnCreate when WebSearchEnabled=true and BraveSearchApiKey is non-empty; DefaultSystemPrompt extended with search_web() directive, trigger conditions, and citation instruction.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Conditional SearchWebTool registration in ClaudeAPISystem.OnCreate | 315a374 | src/Systems/ClaudeAPISystem.cs |
| 2 | Extend DefaultSystemPrompt with web search guidance | 41e7584 | src/Settings.cs |

## What Was Built

### Task 1: ClaudeAPISystem — Conditional SearchWebTool Registration

Added a conditional block after the `ListMemoryFilesTool` registration and before the "Tool registry initialised" log line in `OnCreate()`:

```csharp
var setting = Mod.ActiveSetting;
if (setting != null
    && setting.WebSearchEnabled
    && !string.IsNullOrWhiteSpace(setting.BraveSearchApiKey))
{
    m_ToolRegistry.Register(new SearchWebTool(setting));
    Mod.Log.Info("[ClaudeAPISystem] search_web tool registered.");
}
else
{
    Mod.Log.Info("[ClaudeAPISystem] search_web tool NOT registered (disabled or no API key).");
}
```

Both branches log their outcome. The tool count in the subsequent log line correctly reflects whether the search tool was included.

### Task 2: Settings.cs — DefaultSystemPrompt Web Search Paragraph

Appended a web search guidance paragraph to the existing Phase 3 `DefaultSystemPrompt`. The existing prompt already contained city data tool guidance (from Phase 3); the web search paragraph was added after it with `\n\n` separation:

> "You also have access to web search. Use search_web() to look up real-world urban planning techniques, zoning practices, infrastructure design (roads, transit, utilities), and historical city examples or case studies. When using search results, reference the source title and URL in your response so the player can explore further."

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| OnCreate() registration time | Same pattern as all other tools; user must restart game session after configuring key (consistent UX with Claude API key behavior) |
| Append to DefaultSystemPrompt | D-14 specifies guidance lives in the default prompt, not dynamically injected; keeps the prompt complete and user-overridable as a unit |

## Deviations from Plan

**1. [Rule 3 - Blocking Issue] Resolved STATE.md merge conflict**
- **Found during:** Task 1 commit attempt
- **Issue:** STATE.md had unresolved `<<<<<<< Updated upstream` / `>>>>>>> Stashed changes` conflict markers from a prior stash operation
- **Fix:** Kept the "Updated upstream" content (Phase 04 Plan 01 completion state), discarded stashed version
- **Files modified:** .planning/STATE.md
- **Commit:** 315a374 (included in same commit)

## Verification

- `dotnet build -c Release` exits with 0 errors, 0 warnings (with CS2_INSTALL_PATH set)
- ClaudeAPISystem.cs contains `new SearchWebTool(setting)`, `setting.WebSearchEnabled`, `setting.BraveSearchApiKey`
- Settings.cs DefaultSystemPrompt contains `search_web()`, `urban planning techniques`, `zoning practices`, `infrastructure design`, `reference the source title and URL`, `historical city examples`

## Known Stubs

None.

## Human Verification (Task 3 Checkpoint)

**Status:** Approved (2026-03-29)

Verified in-game:
- Web Search section visible in mod settings with BraveSearchApiKey field and WebSearchEnabled toggle
- Claude invoked search_web autonomously and cited sources in response

## Self-Check: PASSED

- `src/Systems/ClaudeAPISystem.cs` — FOUND (modified)
- `src/Settings.cs` — FOUND (modified)
- Commit 315a374 — verified in git log
- Commit 41e7584 — verified in git log
- `dotnet build -c Release` — 0 errors
