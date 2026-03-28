---
phase: 01-api-migration-core-stability
plan: 03
subsystem: api
tags: [anthropic, claude-api, ollama, http-client, tool-use, thread-safety, interlocked]

# Dependency graph
requires: []
provides:
  - ClaudeAPISystem sends correct Anthropic /v1/messages format with x-api-key header and top-level system field
  - Image content blocks (type:image, source:{type:base64, media_type:image/png}) for screenshot vision
  - Tool-use loop using stop_reason==tool_use and tool_result with tool_use_id
  - HTTP 429 triggers Ollama /v1/chat/completions fallback with in-panel rate-limit notice
  - HTTP 400/401/500 show [Error]: without fallback
  - PendingResult uses Interlocked.Exchange for thread safety; m_RequestInFlight is volatile
  - Settings.cs restructured: ClaudeApiKey/ClaudeModel primary, OllamaFallback* secondary
affects:
  - 01-04-PLAN.md (UISystem integration reads ClaudeAPISystem.PendingResult)
  - Any future plan that reads Mod.ActiveSetting fields

# Tech tracking
tech-stack:
  added:
    - System.Threading.Interlocked (Interlocked.Exchange for atomic PendingResult read/write)
  patterns:
    - Two-provider routing: RunClaudeRequestAsync (primary) + RunOllamaRequestAsync (429 fallback)
    - Sentinel string "__429__" for cross-method control flow without exceptions
    - Interlocked.Exchange(ref PendingResult, value) for all cross-thread PendingResult writes
    - volatile bool m_RequestInFlight to prevent double-send races

key-files:
  created: []
  modified:
    - src/Systems/ClaudeAPISystem.cs
    - src/Settings.cs

key-decisions:
  - "Anthropic format uses x-api-key header (not Authorization: Bearer) — critical difference from Ollama"
  - "system is a top-level field in Anthropic requests, not in messages array — avoids 400 errors"
  - "tool_result messages use role:user (not role:tool) with tool_use_id — Anthropic-specific requirement"
  - "HTTP 429 sentinel __429__ avoids exceptions for control flow; caller handles fallback routing"
  - "Interlocked.Exchange replaces volatile on PendingResult — provides stronger memory barrier"
  - "Settings.cs updated as Rule 3 deviation: ClaudeApiKey/ClaudeModel/OllamaFallback* fields required for compilation"

patterns-established:
  - "Pattern: Two-provider async routing — RunClaudeRequestAsync returns sentinel on 429, caller routes to fallback"
  - "Pattern: All Interlocked.Exchange(ref PendingResult, ...) in async methods; OnUpdate reads via Interlocked.Exchange on UI thread"

requirements-completed: [API-01, API-03, CORE-02]

# Metrics
duration: 3min
completed: 2026-03-28
---

# Phase 01 Plan 03: ClaudeAPISystem Anthropic Migration Summary

**Anthropic /v1/messages client with x-api-key headers, image content blocks, tool_use/tool_result loop, HTTP 429 Ollama fallback, and Interlocked thread safety — replacing the Ollama-native /api/chat implementation**

## Performance

- **Duration:** 3 min
- **Started:** 2026-03-28T22:48:33Z
- **Completed:** 2026-03-28T22:51:36Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Rewrote ClaudeAPISystem.RunRequestAsync into two private methods: RunClaudeRequestAsync (Anthropic /v1/messages) and RunOllamaRequestAsync (Ollama /v1/chat/completions)
- Implemented correct Anthropic API format: x-api-key header, anthropic-version header, system as top-level field, image content blocks, tool_use loop with tool_use_id in tool_result user messages
- HTTP 429 returns __429__ sentinel → caller shows in-panel rate-limit notice via Interlocked.Exchange then falls back to Ollama; HTTP 400/401/500 → [Error]: message, no fallback (D-07)
- Fixed thread safety: PendingResult now uses Interlocked.Exchange everywhere; m_RequestInFlight declared volatile
- Updated Settings.cs (Rule 3 deviation): replaced OllamaApiKey/OllamaModel/OllamaBaseUrl with ClaudeApiKey, ClaudeModel, OllamaFallbackBaseUrl, OllamaFallbackApiKey, OllamaFallbackModel

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite ClaudeAPISystem with Anthropic format, Ollama fallback, and thread safety** - `897b920` (feat)

**Plan metadata:** _(docs commit pending)_

## Files Created/Modified
- `src/Systems/ClaudeAPISystem.cs` - Rewrote RunRequestAsync into RunClaudeRequestAsync + RunOllamaRequestAsync; fixed thread safety fields
- `src/Settings.cs` - Replaced old Ollama-only fields with Claude primary + Ollama Fallback sections

## Decisions Made
- Used sentinel string `"__429__"` as return value from `RunClaudeRequestAsync` to signal rate-limiting to the caller without exceptions — clean control flow, no exception overhead
- Wrote `system` as a top-level JObject field (not in messages array) per Anthropic spec — the most common source of 400 errors when migrating from Ollama
- All `tool_result` blocks go in a single `role:user` message per turn (Anthropic Pattern 3) — multiple tool calls aggregated into one content array
- In-panel rate-limit notice via `Interlocked.Exchange` before Ollama retry, so UISystem drains it as a visible system message on the next frame

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Updated Settings.cs to add new field names required by ClaudeAPISystem**
- **Found during:** Task 1 (ClaudeAPISystem rewrite)
- **Issue:** ClaudeAPISystem references `setting.ClaudeApiKey`, `setting.ClaudeModel`, `setting.OllamaFallbackBaseUrl`, `setting.OllamaFallbackApiKey`, `setting.OllamaFallbackModel` — none of which existed in Settings.cs (Plans 01-01 and 01-02 had not yet been executed)
- **Fix:** Applied Plan 01-01's full Settings.cs rewrite: removed OllamaApiKey/OllamaModel/OllamaBaseUrl, added two-section layout (Claude API + Ollama Fallback), ActiveProvider read-only property, correct defaults, updated LocaleEN
- **Files modified:** src/Settings.cs
- **Verification:** dotnet build -c Release succeeds with 0 errors, 0 warnings
- **Committed in:** 897b920 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 blocking — missing prerequisite plan execution)
**Impact on plan:** Required to compile. Implements Plan 01-01's full Settings.cs spec, which was a declared dependency of this plan. No scope creep beyond the dependency.

## Issues Encountered
- CS2_INSTALL_PATH environment variable had a trailing newline, breaking the dotnet build. Resolved by setting the variable explicitly in the build command. The .csproj fallback default path also works correctly when the env var is absent.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ClaudeAPISystem now sends correct Anthropic /v1/messages format — the critical path blocker from Phase 1 context is resolved
- Settings.cs has all required fields (ClaudeApiKey, ClaudeModel, OllamaFallback*) for Plan 01-01 and downstream plans
- Plan 01-02 (NarrativeMemorySystem async) can proceed independently
- Plan 01-04 (UISystem integration / CityAgentUISystem wiring to new settings) should read ClaudeApiKey from the new Settings field names
- In-game testing (Plan 01-04/CORE-03) will validate the full Anthropic request round-trip

---
*Phase: 01-api-migration-core-stability*
*Completed: 2026-03-28*
