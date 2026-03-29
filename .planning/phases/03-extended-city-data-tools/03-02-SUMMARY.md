---
phase: 03-extended-city-data-tools
plan: 02
subsystem: api
tags: [csharp, settings, toggles, tools, system-prompt]

# Dependency graph
requires:
  - phase: 03-extended-city-data-tools plan: 01
    provides: GetBudgetTool, GetTrafficSummaryTool, GetServicesSummaryTool classes
provides:
  - Settings.cs Data Tools section with 7 per-tool bool toggles
  - CityToolRegistry toggle-aware serialization (filters disabled tools per API call)
  - ClaudeAPISystem with 13 registered tools (4 original data + 3 new data + 6 memory)
  - Updated DefaultSystemPrompt with explicit per-tool usage guidance
affects: [ClaudeAPISystem tool array sent to API, mod settings UI Data Tools section]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Settings group constant kDataToolsGroup = "DataTools" — fourth section after Memory (D-15)
    - Toggle filter at serialization boundary only — Dispatch() never filtered, only GetToolsJson/GetToolsJsonOpenAI
    - IsToolEnabled reads Mod.ActiveSetting per call — toggle changes apply immediately on next request (D-21)
    - Memory tools always pass (default switch case returns true) — no toggles for memory tools (D-10)

key-files:
  created: []
  modified:
    - src/Settings.cs
    - src/Systems/Tools/CityToolRegistry.cs
    - src/Systems/ClaudeAPISystem.cs

key-decisions:
  - "Filter applied at GetToolsJson/GetToolsJsonOpenAI serialization boundary only — Dispatch() not filtered (Claude should not call disabled tools since they are absent from schema)"
  - "IsToolEnabled() reads Mod.ActiveSetting dynamically per call — no restart or new chat required to apply toggle changes (D-21)"
  - "DefaultSystemPrompt updated in Settings.cs (not ClaudeAPISystem) — prompt is user-overridable player setting, not hardcoded in the API system"
  - "DefaultSystemPrompt uses \\n escape sequences for newlines — valid C# string escapes, multi-line structure improves readability of per-tool guidance"

patterns-established:
  - "Pattern: Settings toggle section — bool properties with SettingsUISection(kSection, kDataToolsGroup); add corresponding LocaleEN entries for label, description, and group name"

requirements-completed: [DATA-05]

# Metrics
duration: 13min
completed: 2026-03-29
---

# Phase 03 Plan 02: Data Tool Toggles and Full Tool Registration Summary

**Data Tools toggle section wired into Settings with 7 bool properties, CityToolRegistry filters disabled tools at serialization, ClaudeAPISystem registers 13 tools with updated system prompt guidance**

## Performance

- **Duration:** 13 min
- **Started:** 2026-03-29T18:18:01Z
- **Completed:** 2026-03-29T18:31:34Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- `Settings.cs` extended with `kDataToolsGroup = "DataTools"` section (4th group per D-15) containing 7 bool toggle properties (EnablePopulationTool through EnableServicesSummaryTool) all defaulting to `true` — includes existing 4 data tools and the 3 new Phase 3 tools
- `LocaleEN` updated with human-readable toggle labels: "Population", "Building Demand", "Workforce", "Zoning Summary", "City Finances" (for `get_budget` per D-12), "Traffic", "City Services" — plus group label "Data Tools"
- `CityToolRegistry.IsToolEnabled()` added — maps 7 tool names to Settings bool properties via switch expression; memory tools always return `true` (D-10 default case); reads `Mod.ActiveSetting` dynamically so changes take effect immediately (D-21)
- Filter `if (!IsToolEnabled(tool.Name)) continue;` added to both `GetToolsJson()` and `GetToolsJsonOpenAI()` — Dispatch() not filtered per plan intent
- `ClaudeAPISystem.OnCreate()` now registers 13 tools: 4 original data tools + 3 new data tools (GetBudgetTool, GetTrafficSummaryTool, GetServicesSummaryTool) + 6 memory tools
- `DefaultSystemPrompt` updated with explicit per-tool usage guidance naming all 7 data tools and when to call each — ensures Claude calls tools proactively without player prompting (D-14)

## Task Commits

1. **Task 1: Add Data Tools toggle section to Settings and update CityToolRegistry filtering** — `0907fc0` (feat)
2. **Task 2: Register new tools in ClaudeAPISystem and update default system prompt** — `c65ed4a` (feat)

## Files Created/Modified

- `src/Settings.cs` — Added `kDataToolsGroup` constant, 6-group `SettingsUIGroupOrder` attribute, 7 bool Data Tools properties, 7 SetDefaults entries, 16 LocaleEN entries (group + 7x label+desc), updated `DefaultSystemPrompt` with multi-line tool guidance using `\n` escapes
- `src/Systems/Tools/CityToolRegistry.cs` — Added `IsToolEnabled(string toolName)` private static method with switch expression; added `if (!IsToolEnabled(tool.Name)) continue;` filter to both `GetToolsJson()` and `GetToolsJsonOpenAI()` foreach loops
- `src/Systems/ClaudeAPISystem.cs` — Added 3 new `m_ToolRegistry.Register(...)` calls: `GetBudgetTool`, `GetTrafficSummaryTool`, `GetServicesSummaryTool` — registry now initialises with 13 tools

## Decisions Made

- Filter placed at serialization boundary only (GetToolsJson/GetToolsJsonOpenAI), not in Dispatch() — disabled tools are absent from the schema Claude sees, so it won't call them; if a bug caused Claude to call a disabled tool anyway, it would still execute rather than returning a confusing error
- IsToolEnabled() uses a switch expression reading Mod.ActiveSetting dynamically — this means toggle changes take effect on the very next API call, no restart or new chat session required (D-21)
- DefaultSystemPrompt updated in Settings.cs rather than hardcoded in ClaudeAPISystem — the prompt is stored as a user-overridable mod setting, so the default must live there

## Deviations from Plan

None — plan executed exactly as written.

The plan's Task 2 action listed updating `DefaultSystemPrompt` in `Settings.cs`, which was bundled into Task 1 (same file). This is not a deviation — both tasks modify Settings.cs, and the commit captures both changes atomically. ClaudeAPISystem.cs was committed separately as Task 2.

## Known Stubs

None — all toggles read live `Mod.ActiveSetting` properties. No hardcoded or placeholder values in the toggle logic.

## Self-Check: PASSED

All key artifacts verified at 2026-03-29T18:31:34Z:
- `src/Settings.cs` — FOUND: kDataToolsGroup, EnableBudgetTool, City Finances, get_budget in prompt
- `src/Systems/Tools/CityToolRegistry.cs` — FOUND: IsToolEnabled, continue filter, memory tools comment
- `src/Systems/ClaudeAPISystem.cs` — FOUND: Register(new GetBudgetTool), GetTrafficSummaryTool, GetServicesSummaryTool
- Commits: 0907fc0 (task 1), c65ed4a (task 2) — both verified in git log
- Build: `dotnet build -c Release` — 0 errors, 0 warnings
