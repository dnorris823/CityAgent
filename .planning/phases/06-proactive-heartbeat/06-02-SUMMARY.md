---
phase: 06-proactive-heartbeat
plan: 02
subsystem: heartbeat
tags: [csharp, cs2-modding, heartbeat, async, gameSystemBase, timer]

# Dependency graph
requires:
  - phase: 06-01
    provides: HeartbeatEnabled, HeartbeatIntervalMinutes, HeartbeatIncludeScreenshot, HeartbeatSystemPrompt settings fields
  - phase: 05-agent-tools
    provides: CityToolRegistry, all tool classes, NarrativeMemorySystem, ClaudeAPISystem pattern
provides:
  - HeartbeatSystem with timer + async dispatch + backoff + full tool registry
  - IsScreenshotCapturePending property on CityAgentUISystem
  - PendingHeartbeatResult volatile field ready for CityAgentUISystem.OnUpdate drain (Plan 03)
affects: [06-03-ui-integration, CityAgentUISystem.OnUpdate poll loop]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "HeartbeatSystem mirrors ClaudeAPISystem's RunOllamaRequestAsync pattern with OpenAI-compatible endpoint"
    - "Wall-clock timer via DateTime.UtcNow comparison in OnUpdate (immune to game speed/pause)"
    - "m_BackoffCycles = 3 on error, decremented each would-be fire cycle (Pitfall 7: timer reset during backoff)"
    - "Separate screenshot path (cityagent_heartbeat_screenshot.png) avoids conflict with chat capture"
    - "One-cycle screenshot delay: read prior file, queue new capture — ScreenCapture writes at end of frame"
    - "IsScreenshotCapturePending as expression-body property: m_ScreenshotWaitFrames >= 0"

key-files:
  created:
    - src/Systems/HeartbeatSystem.cs
  modified:
    - src/Systems/CityAgentUISystem.cs

key-decisions:
  - "HeartbeatSystem uses Ollama /v1/chat/completions endpoint (OpenAI-compatible) — mirrors ClaudeAPISystem.RunOllamaRequestAsync, not the native /api/chat endpoint"
  - "Own static HttpClient s_Http per D-01 — independent pipeline, no sharing with ClaudeAPISystem"
  - "10-tool registry: GetPopulation, GetBuildingDemand, GetWorkforce, GetZoningSummary + 6 memory tools — excludes GetBudget, GetTrafficSummary, GetServicesSummary, SearchWeb"
  - "PendingHeartbeatResult is a field (not property) to support Interlocked.Exchange(ref ...) — D-14"
  - "IsScreenshotCapturePending reads m_ScreenshotWaitFrames on the game thread — no cross-thread concern since both systems run on main thread"

patterns-established:
  - "Backoff timer reset during decrement: m_LastFireTime = DateTime.UtcNow inside the m_BackoffCycles > 0 branch"
  - "Screenshot: read prior cycle's file first, then queue new capture — avoids same-frame read-after-write race"

requirements-completed: [HB-01, HB-03]

# Metrics
duration: 18min
completed: 2026-03-31
---

# Phase 06 Plan 02: HeartbeatSystem Implementation Summary

**HeartbeatSystem.cs created — background periodic advisor with wall-clock timer, Ollama async request loop, 3-cycle error backoff, full 10-tool registry, and one-cycle screenshot capture pattern**

## Performance

- **Duration:** ~18 min
- **Started:** 2026-03-31T04:02:00Z
- **Completed:** 2026-03-31T04:20:17Z
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments

- Created `src/Systems/HeartbeatSystem.cs` (325 lines) implementing HB-01 and HB-03:
  - `GameSystemBase` with `OnCreate` / `OnUpdate` / `OnDestroy` lifecycle
  - Wall-clock timer (`DateTime.UtcNow`) that fires every N minutes regardless of game speed
  - `m_BackoffCycles` counter: set to 3 on API error, timer reset during each decrement (Pitfall 7)
  - `m_HeartbeatInFlight` volatile guard preventing request stacking (D-11)
  - `RunHeartbeatAsync`: async Ollama /v1/chat/completions request with OpenAI-compatible tool-use loop (max 10 iterations), mirrors `ClaudeAPISystem.RunOllamaRequestAsync`
  - Full 10-tool registry: 4 city-data tools + 6 memory tools
  - Screenshot: reads prior cycle's capture file, queues new capture for next cycle
  - `PendingHeartbeatResult` as public volatile field for `Interlocked.Exchange` drain in Plan 03

- Added `IsScreenshotCapturePending` property to `CityAgentUISystem.cs`:
  - `public bool IsScreenshotCapturePending => m_ScreenshotWaitFrames >= 0;`
  - Enables D-07: heartbeat skips screenshot inclusion when a user-triggered capture is in progress

## Task Commits

1. **Task 1 + Task 2: HeartbeatSystem.cs + IsScreenshotCapturePending** — `8dabc76` (feat — committed together since Task 2 is a prerequisite for Task 1 to compile)

## Files Created/Modified

- `src/Systems/HeartbeatSystem.cs` — Full heartbeat system: timer, async dispatch, backoff, tool registry
- `src/Systems/CityAgentUISystem.cs` — Added `IsScreenshotCapturePending` property (7 lines added)

## Decisions Made

- **Ollama endpoint**: Used `/v1/chat/completions` (OpenAI-compatible) matching `ClaudeAPISystem.RunOllamaRequestAsync`, rather than the native `/api/chat` shown in the plan template. The OpenAI-compatible format is already proven in the codebase and uses `choices[0].finish_reason` / `choices[0].message` parsing.
- **10-tool registry**: Included city-data tools (GetPopulation, GetBuildingDemand, GetWorkforce, GetZoningSummary) plus all 6 memory tools. Excluded GetBudget, GetTrafficSummary, GetServicesSummary, SearchWeb — the plan specified exactly these 10. The heartbeat is background observation; budget/traffic/services/web-search are available in chat.
- **Tasks committed together**: Task 2 (IsScreenshotCapturePending) is a compile-time prerequisite for Task 1 (HeartbeatSystem references it). Committed atomically in one commit.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Used OpenAI-compatible endpoint format instead of native Ollama /api/chat**
- **Found during:** Task 1, implementation
- **Issue:** Plan template showed `/api/chat` with `message.tool_calls` response parsing (native Ollama format). ClaudeAPISystem.RunOllamaRequestAsync uses `/v1/chat/completions` with OpenAI-compatible `choices[0]` response format instead.
- **Fix:** Used `/v1/chat/completions` endpoint with `choices[0].finish_reason` / `choices[0].message` / `choices[0].message.tool_calls` parsing — consistent with the existing Ollama integration pattern in `ClaudeAPISystem`.
- **Files modified:** `src/Systems/HeartbeatSystem.cs`
- **Commit:** `8dabc76`

## Known Stubs

None — `HeartbeatSystem` is fully wired. `PendingHeartbeatResult` is populated by `RunHeartbeatAsync` and ready for `CityAgentUISystem.OnUpdate` to drain in Plan 03.

## Self-Check: PASSED

- `src/Systems/HeartbeatSystem.cs` exists: FOUND
- `src/Systems/CityAgentUISystem.cs` contains `IsScreenshotCapturePending`: FOUND
- Commit `8dabc76` exists in git log: FOUND
- Build with correct CS2_INSTALL_PATH: 0 errors, 0 warnings

---
*Phase: 06-proactive-heartbeat*
*Completed: 2026-03-31*
