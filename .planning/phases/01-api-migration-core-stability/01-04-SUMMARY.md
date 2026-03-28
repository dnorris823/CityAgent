---
phase: 01-api-migration-core-stability
plan: "04"
subsystem: api
tags: [threading, interlocked, task-run, async, screenshot, narrative-memory, fire-and-forget]

# Dependency graph
requires:
  - phase: 01-api-migration-core-stability/01-02
    provides: NarrativeMemorySystem async API (SaveChatSessionAsync)
  - phase: 01-api-migration-core-stability/01-03
    provides: ClaudeAPISystem with PendingResult as non-volatile string
provides:
  - CityAgentUISystem with atomic PendingResult drain via Interlocked.Exchange
  - Chat session persistence via fire-and-forget async SaveChatSessionAsync
  - Screenshot base64 encoding on background Task.Run thread
  - SaveChatSessionAsync method on NarrativeMemorySystem (stub async wrapper)
  - Settings hot-reload confirmed: Mod.ActiveSetting read per-request in RunRequestAsync
affects:
  - 01-VALIDATION
  - in-game testing

# Tech tracking
tech-stack:
  added:
    - System.Threading (Interlocked.Exchange for atomic field access)
    - System.Threading.Tasks (Task.Run for background thread work)
  patterns:
    - "Atomic result drain: Interlocked.Exchange(ref field, null) — reads and clears in one operation, no race"
    - "Fire-and-forget async: _ = someAsync() — fire off Task, swallow result, log errors inside"
    - "Background encode: Task.Run(() => { File.ReadAllBytes; Convert.ToBase64String; update binding })"
    - "Main-thread pure functions + background I/O split: ChatHistoryToMarkdown stays sync, SaveChatSession goes async"

key-files:
  created: []
  modified:
    - src/Systems/CityAgentUISystem.cs
    - src/Systems/ClaudeAPISystem.cs
    - src/Systems/NarrativeMemorySystem.cs

key-decisions:
  - "Interlocked.Exchange replaces volatile-only PendingResult drain — eliminates read-then-clear race between async thread and main thread"
  - "volatile removed from ClaudeAPISystem.PendingResult — required for Interlocked.Exchange ref parameter (CS0420 compiler restriction)"
  - "SaveChatSessionAsync added as Task.Run wrapper over existing SaveChatSession — Plan 02 not yet executed, wrapper provides CORE-01 compliance now"
  - "ChatHistoryToMarkdown kept synchronous on main thread — pure function, no I/O, safe to call from OnUpdate"
  - "m_ScreenshotWaitFrames set to -1 before Task.Run — prevents double-processing if next OnUpdate fires before background completes"

patterns-established:
  - "Async fire-and-forget: _ = asyncMethod() with internal try/catch logging"
  - "Background I/O: capture path as local variable before Task.Run lambda to avoid closure capture of mutable field"

requirements-completed: [CORE-01, CORE-03, API-04]

# Metrics
duration: 8min
completed: 2026-03-28
---

# Phase 1 Plan 04: UI System Async Wiring Summary

**CityAgentUISystem now drains PendingResult atomically via Interlocked.Exchange, persists chat sessions fire-and-forget off the main thread, and encodes screenshots in a Task.Run background worker**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-28T18:36:53Z
- **Completed:** 2026-03-28T18:44:00Z
- **Tasks:** 1 of 2 (Task 2 is checkpoint:human-verify — awaiting in-game sign-off)
- **Files modified:** 3

## Accomplishments
- Replaced non-atomic `m_ClaudeAPI.PendingResult = null` drain with `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)` — eliminates the read-then-clear race window between the async thread pool and main game thread (CORE-02, D-14)
- Moved screenshot file read + base64 encoding into `Task.Run` background thread — `File.ReadAllBytes` + `Convert.ToBase64String` no longer block the main game thread (CORE-01, D-13)
- Converted `PersistChatSession` to fire-and-forget async via `_ = m_NarrativeMemory.SaveChatSessionAsync(markdown)` — `ChatHistoryToMarkdown` (pure function) stays synchronous; only the file write goes async (CORE-01, D-11, D-12)
- Confirmed settings hot-reload path: `OnSendMessage` → `BeginRequest` → `RunRequestAsync` reads `Mod.ActiveSetting` per-request; no cached settings anywhere in the chain (API-04)

## Task Commits

Each task was committed atomically:

1. **Task 1: Wire Interlocked.Exchange, async persistence, and background screenshot encoding** - `67d8496` (feat)

Task 2 is `type="checkpoint:human-verify"` — requires in-game manual validation. No commit for Task 2.

## Files Created/Modified
- `src/Systems/CityAgentUISystem.cs` - Added System.Threading/Tasks usings; atomic PendingResult drain; background screenshot Task.Run; fire-and-forget SaveChatSessionAsync call
- `src/Systems/ClaudeAPISystem.cs` - Removed `volatile` from PendingResult (required for Interlocked.Exchange ref parameter)
- `src/Systems/NarrativeMemorySystem.cs` - Added System.Threading.Tasks using; added SaveChatSessionAsync as Task.Run wrapper over SaveChatSession

## Decisions Made
- Removed `volatile` from `ClaudeAPISystem.PendingResult`: the C# compiler raises CS0420 when passing a `volatile` field by `ref` to `Interlocked.Exchange`. Since `Interlocked.Exchange` provides stronger guarantees than `volatile` alone, this is the correct resolution.
- Added `SaveChatSessionAsync` directly to `NarrativeMemorySystem` as a `Task.Run` wrapper rather than inlining `Task.Run(() => SaveChatSession(...))` in `CityAgentUISystem`. Keeps the async wrapping encapsulated in the right system.
- Captured `m_ScreenshotWaitFrames = -1` before launching `Task.Run` (not inside it) to stop the polling loop immediately and prevent double-triggering.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Removed volatile from ClaudeAPISystem.PendingResult**
- **Found during:** Task 1 (PendingResult drain with Interlocked.Exchange)
- **Issue:** C# compiler raises CS0420 ("reference to a volatile field will not be treated as volatile") when passing a volatile field by `ref` to `Interlocked.Exchange`. Build would fail. Plan 03 was supposed to fix this first, but Plan 03 has not been executed.
- **Fix:** Removed `volatile` keyword from `PendingResult`; added comment documenting the Interlocked.Exchange access pattern
- **Files modified:** `src/Systems/ClaudeAPISystem.cs`
- **Verification:** Build succeeds with 0 errors
- **Committed in:** 67d8496 (Task 1 commit)

**2. [Rule 3 - Blocking] Added SaveChatSessionAsync to NarrativeMemorySystem**
- **Found during:** Task 1 (PersistChatSession fire-and-forget conversion)
- **Issue:** Plan 04 calls `m_NarrativeMemory.SaveChatSessionAsync(markdown)`, but this method does not exist — Plan 02 (which was supposed to add it) has not been executed. Build would fail with CS0117 (method not found).
- **Fix:** Added `SaveChatSessionAsync(string transcriptMarkdown)` to `NarrativeMemorySystem` as a `public Task` that wraps `SaveChatSession` in `Task.Run` with internal error logging. Matches the interface contract specified in Plan 04's context section.
- **Files modified:** `src/Systems/NarrativeMemorySystem.cs`
- **Verification:** Build succeeds; grep confirms SaveChatSessionAsync exists and is called correctly
- **Committed in:** 67d8496 (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 3 — blocking issues from unexecuted prior plans)
**Impact on plan:** Both fixes were strictly necessary to compile. No scope creep — changes are the minimum needed to make the plan's intended code work correctly. When Plans 01-02 and 01-03 are eventually executed, the `volatile` removal and `SaveChatSessionAsync` will be superseded or confirmed by those plans.

## Issues Encountered
- Build failed in worktree context without explicit `CS2_INSTALL_PATH` env var — resolved by passing it explicitly on the dotnet build command line. The `CS2_INSTALL_PATH` user environment variable is not inherited in the worktree bash context. Build succeeded with 0 errors when env var was provided.

## User Setup Required
None — no external service configuration required.

## Next Phase Readiness
- All four code-level requirements of Plan 04 are met: atomic drain, fire-and-forget persistence, background screenshot encoding, per-request settings reads
- Ready for in-game validation (Task 2 checkpoint) — requires full build+deploy+launch cycle
- After Task 2 sign-off, Phase 1 is complete and Phase 2 (UI polish) can begin

---
*Phase: 01-api-migration-core-stability*
*Completed: 2026-03-28 (Task 1 only — awaiting Task 2 human-verify)*
