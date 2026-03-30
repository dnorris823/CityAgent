---
phase: 04-web-search-tool
plan: "01"
subsystem: settings, tools
tags: [brave-search, settings, tool-implementation, web-search]
dependency_graph:
  requires: []
  provides: [Settings.BraveSearchApiKey, Settings.WebSearchEnabled, SearchWebTool]
  affects: [ClaudeAPISystem (tool registration in 04-02)]
tech_stack:
  added: []
  patterns: [ICityAgentTool implementation, SettingsUISection pattern, CancellationTokenSource timeout]
key_files:
  created:
    - src/Systems/Tools/SearchWebTool.cs
  modified:
    - src/Settings.cs
decisions:
  - "WebSearchEnabled defaults to false (explicit opt-in) — user must get a key first"
  - "SearchWebTool owns its own HttpClient (s_BraveHttp) to avoid coupling to ClaudeAPISystem.s_Http"
  - "Headers set per-request on HttpRequestMessage, not DefaultRequestHeaders (thread-safety)"
  - "using (var cts = ...) C# 7.3 syntax used (compatible with Unity Mono .NET Standard 2.1)"
metrics:
  duration: 2min
  completed: 2026-03-30
  tasks_completed: 2
  files_modified: 2
---

# Phase 4 Plan 1: Web Search Settings and Tool Implementation Summary

**One-liner:** Web Search settings section (BraveSearchApiKey + WebSearchEnabled toggle) added to Settings.cs and SearchWebTool implemented as a full ICityAgentTool calling Brave Search API with 5s timeout and 3-result structured JSON output.

## Tasks Completed

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Add Web Search settings section to Settings.cs | c08f82c | src/Settings.cs |
| 2 | Implement SearchWebTool class | 9ed28ac | src/Systems/Tools/SearchWebTool.cs |

## What Was Built

### Task 1: Settings.cs — Web Search Section

Added a new `kWebSearchGroup = "WebSearch"` section as the 7th settings group (after DataTools), following the established `kProviderGroup, kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup, kDataToolsGroup` order. The existing settings already had `kDataToolsGroup` from Phase 3, so `kWebSearchGroup` was placed after it.

Two new properties added:
- `BraveSearchApiKey` — `[SettingsUITextInput]` string, default `string.Empty`
- `WebSearchEnabled` — bool toggle (no additional attribute needed — CS2 ModSetting renders bool as toggle automatically), default `false`

`SetDefaults()` and `LocaleEN.ReadEntries()` updated with all required entries.

### Task 2: SearchWebTool.cs — Brave Search API Tool

New file `src/Systems/Tools/SearchWebTool.cs` implementing `ICityAgentTool`:
- `Name = "search_web"`
- Constructor injection: `SearchWebTool(Setting setting)`
- Owns `private static readonly HttpClient s_BraveHttp` — separate from `ClaudeAPISystem.s_Http`
- Calls `https://api.search.brave.com/res/v1/web/search?q={query}&count=3&extra_snippets=true`
- `X-Subscription-Token` and `Accept: application/json` headers set per-request on `HttpRequestMessage`
- 5-second timeout via `CancellationTokenSource(TimeSpan.FromSeconds(5))` inside `using` block
- Returns `{query, results[{title, url, description, extra_snippets?}]}` per D-07
- All error paths return `{error: "Search failed: [reason]"}` per D-02
- `OperationCanceledException` caught separately for clean timeout message

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| `WebSearchEnabled = false` | Explicit opt-in UX — user must get a Brave key before enabling; defaulting true with empty key shows an inconsistent "on" state |
| Separate `s_BraveHttp` static client | Avoids coupling to `ClaudeAPISystem.s_Http`; single-responsibility; thread-safe header isolation |
| `using (var cts = ...)` instead of `using var cts = ...` | C# 7.3 compatibility for Unity Mono .NET Standard 2.1 runtime |
| `kWebSearchGroup` placed after `kDataToolsGroup` | Phase 3 already added `kDataToolsGroup`; plan's NOTE covered this case — placed after it |

## Verification

- `dotnet build -c Release` exits with 0 errors, 0 warnings after both tasks
- Settings.cs contains all required constants, properties, defaults, and locale entries
- SearchWebTool.cs exists and implements all acceptance criteria

## Deviations from Plan

None — plan executed exactly as written.

The build environment has a newline in the `CS2_INSTALL_PATH` environment variable within the bash session, requiring the explicit path to be passed. This is a dev environment quirk, not a code issue.

## Self-Check: PASSED

- `src/Systems/Tools/SearchWebTool.cs` — FOUND
- `src/Settings.cs` (modified) — FOUND
- Commit c08f82c — verified in git log
- Commit 9ed28ac — verified in git log
- `dotnet build -c Release` — 0 errors
