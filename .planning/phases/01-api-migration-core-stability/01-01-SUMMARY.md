---
phase: 01-api-migration-core-stability
plan: 01
subsystem: api
tags: [csharp, settings, claude-api, ollama, mod-settings, cs2]

# Dependency graph
requires: []
provides:
  - "Restructured Settings.cs with ClaudeApiKey/ClaudeModel (primary) and OllamaFallbackBaseUrl/OllamaFallbackApiKey/OllamaFallbackModel (optional fallback)"
  - "Read-only ActiveProvider status label property"
  - "kClaudeGroup and kOllamaGroup setting group constants replacing kGeneralGroup"
  - "Updated LocaleEN with all new field labels and '(optional)' Ollama section header"
affects:
  - "02-api-migration-core-stability (ClaudeAPISystem rewrite reads ClaudeApiKey, ClaudeModel, OllamaFallback* fields)"
  - "all subsequent plans that access Mod.ActiveSetting"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Two-section mod settings layout: primary provider first, fallback second with (optional) label"
    - "Read-only settings label via getter-only property (no set accessor)"

key-files:
  created: []
  modified:
    - src/Settings.cs
    - src/Systems/ClaudeAPISystem.cs

key-decisions:
  - "Old OllamaApiKey/OllamaModel/OllamaBaseUrl fields deleted entirely (not deprecated) per D-01"
  - "Default Claude model: claude-sonnet-4-6 per D-02"
  - "Default Ollama base URL: http://localhost:11434 per D-03"
  - "ActiveProvider is a getter-only property — CS2 renders as read-only label per D-04"
  - "Ollama Fallback section header labeled '(optional)' per D-05"

patterns-established:
  - "Two-section provider settings: kClaudeGroup (primary) + kOllamaGroup (optional fallback)"
  - "Read-only status display: getter-only string property with SettingsUISection attribute"

requirements-completed: [API-02, API-04]

# Metrics
duration: 4min
completed: 2026-03-28
---

# Phase 1 Plan 01: Settings Restructure Summary

**Settings.cs rewritten with Claude API as primary provider (ClaudeApiKey, ClaudeModel) and Ollama as optional fallback (OllamaFallbackBaseUrl, OllamaFallbackApiKey, OllamaFallbackModel), plus a read-only ActiveProvider status label**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-28T22:40:28Z
- **Completed:** 2026-03-28T22:44:05Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments

- Replaced all three Ollama-only fields (`OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl`) with the new two-section layout per decisions D-01 through D-05
- Added `ClaudeApiKey` and `ClaudeModel` as primary Claude API section with default `claude-sonnet-4-6`
- Added `OllamaFallbackBaseUrl`, `OllamaFallbackApiKey`, `OllamaFallbackModel` with default base URL `http://localhost:11434`
- Added read-only `ActiveProvider` getter-only property showing "Currently using: Claude API" or "Currently using: No API key configured"
- Updated `LocaleEN` with all new label/desc entries including "Ollama Fallback (optional)" group header
- Fixed downstream `ClaudeAPISystem.cs` compilation by updating three broken field references to new fallback names

## Task Commits

Each task was committed atomically:

1. **Task 1: Restructure Settings.cs with Claude API and Ollama Fallback sections** - `eb559d3` (feat)

**Plan metadata:** *(to follow)*

## Files Created/Modified

- `src/Settings.cs` - Fully restructured with two provider sections, new field names, updated SetDefaults and LocaleEN
- `src/Systems/ClaudeAPISystem.cs` - Updated three references from old field names to new OllamaFallback* names (compile fix)

## Decisions Made

- Old fields deleted entirely (not marked `[Obsolete]`) per D-01 — clean break required for migration
- `ActiveProvider` implemented as getter-only computed property rather than a stored field — reads current state dynamically
- `SystemPrompt` and `ScreenshotKeybind` moved from the removed `kGeneralGroup` to `kClaudeGroup` — they are Claude-specific settings that belong in the Claude section

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed broken references in ClaudeAPISystem.cs**
- **Found during:** Task 1 (Settings.cs restructure)
- **Issue:** Deleting the three old fields caused `ClaudeAPISystem.cs` lines 88-90 to fail with CS1061 (field not found), breaking the entire build
- **Fix:** Updated the three broken property reads: `OllamaBaseUrl` → `OllamaFallbackBaseUrl`, `OllamaApiKey` → `OllamaFallbackApiKey`, `OllamaModel` → `OllamaFallbackModel`
- **Files modified:** `src/Systems/ClaudeAPISystem.cs`
- **Verification:** `dotnet build -c Release` succeeded with 0 errors, 0 warnings
- **Committed in:** `eb559d3` (part of Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 1 - bug: broken field references)
**Impact on plan:** Necessary fix — deleting the old fields broke the build in ClaudeAPISystem.cs. The fix is a minimal rename of three field reads; the full ClaudeAPISystem rewrite (to Claude API format) is scoped to plan 01-02.

## Issues Encountered

- `dotnet build` required the `CS2_INSTALL_PATH` env var to resolve game DLLs. The user-level PowerShell env var was not inherited by the bash subprocess, so the first build attempt showed 121 errors (all DLL-not-found). Fixed by passing `CS2_INSTALL_PATH` as an environment variable inline (`CS2_INSTALL_PATH="..." dotnet build`). This is a known environment limitation of the worktree bash context, not a code issue.

## User Setup Required

None — no external service configuration required.

## Known Stubs

None — all new settings fields have concrete defaults and functional implementations.

## Next Phase Readiness

- `src/Settings.cs` is now the authoritative field contract for all downstream plans
- Plan 01-02 (ClaudeAPISystem rewrite) can now read `Mod.ActiveSetting.ClaudeApiKey`, `Mod.ActiveSetting.ClaudeModel`, and `Mod.ActiveSetting.OllamaFallback*` — the field names are stable
- The Ollama references remaining in `ClaudeAPISystem.cs` are temporary placeholders; plan 01-02 will replace that entire method with Claude API format

---
*Phase: 01-api-migration-core-stability*
*Completed: 2026-03-28*
