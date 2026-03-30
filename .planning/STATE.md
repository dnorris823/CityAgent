---
gsd_state_version: 1.0
milestone: v1.0
milestone_name: milestone
status: executing
stopped_at: Completed 05-02-PLAN.md
last_updated: "2026-03-30T23:10:21.865Z"
last_activity: 2026-03-30
progress:
  total_phases: 6
  completed_phases: 4
  total_plans: 18
  completed_plans: 14
  percent: 11
---

# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-26)

**Core value:** You ask Claude something about your city, it sees the current screenshot and live stats, and responds with narrative commentary that remembers where the city has been — in a polished chat panel that feels like it belongs in the game.
**Current focus:** Phase 05 — memory-file-explorer

## Current Position

Phase: 05 (memory-file-explorer) — EXECUTING
Plan: 3 of 3
Status: Ready to execute
Last activity: 2026-03-30

Progress: [█░░░░░░░░░] 11%

## Performance Metrics

**Velocity:**

- Total plans completed: 0
- Average duration: —
- Total execution time: —

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| - | - | - | - |

**Recent Trend:**

- Last 5 plans: —
- Trend: —

*Updated after each plan completion*
| Phase 01-api-migration-core-stability P01 | 4 | 1 tasks | 2 files |
| Phase 01-api-migration-core-stability P02 | 3min | 2 tasks | 5 files |
| Phase 01 P03 | 3 | 1 tasks | 2 files |
| Phase 01 P04 | 8 | 1 tasks | 3 files |
| Phase 02-chat-ui-polish P03 | 8 | 2 tasks | 2 files |
| Phase 02-chat-ui-polish P02 | 8m | 2 tasks | 2 files |
| Phase 03-extended-city-data-tools P02 | 13min | 2 tasks | 3 files |
| Phase 04-web-search-tool P01 | 2min | 2 tasks | 2 files |
| Phase 04-web-search-tool P02 | 5min | 2 tasks | 2 files |
| Phase 04-web-search-tool P02 | 5min | 2 tasks | 2 files |
| Phase 05 P05-01 | 15min | 2 tasks | 2 files |
| Phase 05 P05-02 | 20min | 2 tasks | 3 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- Claude API as primary (not Ollama): Ollama format was scaffolding; full migration needed before anything else works
- No streaming API: Coherent GT binding layer not designed for incremental updates — use `stream: false` with loading indicator
- Brave Search over Bing: Free tier (2,000/month), single header auth, fits existing async HTTP pattern
- [Phase 01-api-migration-core-stability]: Old OllamaApiKey/OllamaModel/OllamaBaseUrl fields deleted entirely per D-01; new ClaudeApiKey/ClaudeModel primary + OllamaFallback* optional section with claude-sonnet-4-6 default (D-02) and localhost:11434 Ollama default (D-03)
- [Phase 01-api-migration-core-stability]: GetAwaiter().GetResult() used in tool Execute methods — safe on thread pool (no SynchronizationContext in Unity Mono)
- [Phase 01-api-migration-core-stability]: File.Delete wrapped in Task.Run since .NET Standard 2.1 has no DeleteAsync
- [Phase 01-api-migration-core-stability]: NarrativeMemorySystem read methods (ReadFile, ListFiles, GetAlwaysInjectedContext) kept synchronous
- [Phase 01]: Anthropic format uses x-api-key header and top-level system field (not Authorization:Bearer, not in messages array)
- [Phase 01]: HTTP 429 sentinel __429__ for rate-limit fallback routing; 400/401/500 show [Error]: without Ollama fallback
- [Phase 01]: Interlocked.Exchange replaces volatile on PendingResult for stronger thread-safety memory barrier
- [Phase 01]: volatile removed from ClaudeAPISystem.PendingResult — Interlocked.Exchange provides stronger guarantees and CS0420 prevents passing volatile ref
- [Phase 02-01]: Regex inline fully qualified (System.Text.RegularExpressions.Regex) — no new using directive needed
- [Phase 02-01]: D-04 expressed as comment marker only — RunClaudeRequestAsync does not yet iterate m_History so there is nothing to filter at this point
- [Phase 02-chat-ui-polish]: Italic regex: replaced lookbehind with character class boundary groups for Coherent GT V8 compatibility
- [Phase 02-chat-ui-polish]: Nested list: relative baseIndent comparison (itemIndent > baseIndent) handles both 2-space and 4-space Claude indent
- [Phase 02-chat-ui-polish]: Code language label: block-level .ca-code-lang span before <pre> for visual merging via top-rounded CSS corners
- [Phase 02-chat-ui-polish]: Store queued message in useRef not useState to prevent double-send on re-render during isLoading transition
- [Phase 03-extended-city-data-tools]: Toggle filter at serialization boundary only (GetToolsJson/GetToolsJsonOpenAI) — Dispatch() not filtered so disabled tools still execute if called directly
- [Phase 03-extended-city-data-tools]: DefaultSystemPrompt updated in Settings.cs (user-overridable setting) not hardcoded in ClaudeAPISystem; multi-line tool guidance uses C# \n escape sequences
- [Phase 04-web-search-tool]: WebSearchEnabled defaults to false (explicit opt-in — user must configure Brave key before enabling)
- [Phase 04-web-search-tool]: SearchWebTool owns separate s_BraveHttp HttpClient to avoid coupling to ClaudeAPISystem.s_Http
- [Phase 04-web-search-tool]: Registration at OnCreate() time — user must restart game session after adding Brave key (same pattern as Claude API key)
- [Phase 04-web-search-tool]: Web search paragraph appended to DefaultSystemPrompt — not dynamically injected at request time (D-14)
- [Phase 04-web-search-tool]: Registration at OnCreate() time — user must restart game session after adding Brave key (same pattern as Claude API key)
- [Phase 04-web-search-tool]: Web search paragraph appended to DefaultSystemPrompt — not dynamically injected at request time (D-14)
- [Phase 05]: ListFiles() field names changed to name/size_kb/last_modified_unix (from filename/size_bytes/last_modified) to match React binding contract D-06/D-22
- [Phase 05]: awaitingOp state string disambiguates memoryOpResult in React: same binding serves read result OR write/delete status code per D-18
- [Phase 05]: Synchronous WriteFile/DeleteFile added to NarrativeMemorySystem alongside async variants — sync for UI main thread, async for AI tool loop

### Pending Todos

None yet.

### Blockers/Concerns

- [Pre-Phase 1]: ECS component names for budget/traffic are unconfirmed — runtime discovery needed before Phase 3 can begin
- [Pre-Phase 1]: `NarrativeMemorySystem` public API surface is partially inferred — needs direct code read before Phase 5 begins

## Session Continuity

Last session: 2026-03-30T23:10:21.862Z
Stopped at: Completed 05-02-PLAN.md
Resume file: None
