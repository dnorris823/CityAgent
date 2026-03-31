---
phase: 06-proactive-heartbeat
plan: 01
subsystem: settings
tags: [csharp, cs2-modding, settings, heartbeat]

# Dependency graph
requires:
  - phase: 05-agent-tools
    provides: Settings.cs pattern with SettingsUIGroupOrder and locale entries
provides:
  - Heartbeat settings section in CS2 mod options menu (4 configurable fields)
  - DefaultHeartbeatSystemPrompt constant with [silent] sentinel
  - HeartbeatSystem scheduled in GameSimulation phase
affects: [06-02-heartbeat-system, CityAgentUISystem poll loop, HeartbeatSystem implementation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Heartbeat settings follow existing SettingsUISection/SettingsUISlider/SettingsUITextInput pattern"
    - "kHeartbeatGroup constant added as 8th group in SettingsUIGroupOrder/SettingsUIShowGroupName"
    - "DefaultHeartbeatSystemPrompt uses [silent] sentinel for AI silence gating"

key-files:
  created: []
  modified:
    - src/Settings.cs
    - src/Mod.cs

key-decisions:
  - "HeartbeatIncludeScreenshot defaults to false — screenshots add API cost for a background check"
  - "DefaultHeartbeatSystemPrompt instructs AI to respond [silent] when nothing noteworthy"
  - "HeartbeatSystem scheduled in GameSimulation phase, same as ClaudeAPISystem and CityDataSystem"
  - "HeartbeatEnabled defaults to false — no heartbeat fires on fresh install"

patterns-established:
  - "Heartbeat fields follow SettingsUISection(kSection, kHeartbeatGroup) + attribute + property pattern"
  - "All 4 heartbeat fields mirrored in SetDefaults() for reset-to-defaults support"

requirements-completed: [HB-02]

# Metrics
duration: 12min
completed: 2026-03-30
---

# Phase 06 Plan 01: Heartbeat Settings and System Registration Summary

**Heartbeat settings section with 4 configurable fields added to Settings.cs and HeartbeatSystem registered in Mod.cs OnLoad for GameSimulation phase**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-30T00:00:00Z
- **Completed:** 2026-03-30T00:12:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added `kHeartbeatGroup` constant and wired it into `SettingsUIGroupOrder` / `SettingsUIShowGroupName` as the 8th group
- Added `DefaultHeartbeatSystemPrompt` private const with the `[silent]` sentinel for AI silence gating
- Added 4 heartbeat fields with correct defaults: `HeartbeatEnabled` (false), `HeartbeatIntervalMinutes` (5, slider 1-60), `HeartbeatIncludeScreenshot` (false), `HeartbeatSystemPrompt` (string)
- Added all 4 fields to `SetDefaults()` and full locale label/desc entries in `LocaleEN`
- Scheduled `HeartbeatSystem` in `GameSimulation` phase in `Mod.cs` `OnLoad`

## Task Commits

Each task was committed atomically:

1. **Task 1: Add heartbeat settings fields and locale entries** - `142e79e` (feat)
2. **Task 2: Schedule HeartbeatSystem in Mod.cs** - `142e79e` (feat — committed together with Task 1)

## Files Created/Modified
- `src/Settings.cs` - Added kHeartbeatGroup, DefaultHeartbeatSystemPrompt, 4 heartbeat fields, SetDefaults entries, 9 locale entries
- `src/Mod.cs` - Added HeartbeatSystem scheduling in GameSimulation phase

## Decisions Made
- `HeartbeatIncludeScreenshot` defaults to `false` — screenshots add meaningful API cost per background check; player opts in explicitly
- `DefaultHeartbeatSystemPrompt` uses the `[silent]` sentinel pattern, per CONTEXT.md D-05, so `CityAgentUISystem` can suppress non-events without parsing the response
- `HeartbeatEnabled` defaults to `false` per D-09 — no heartbeat fires on fresh install; player must opt in

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
None. `dotnet build` is expected to fail until Plan 02 creates `HeartbeatSystem.cs` — this is explicitly deferred per the plan's verification note.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Settings infrastructure complete; Plan 02 can implement `HeartbeatSystem.cs` and reference `Mod.ActiveSetting.HeartbeatEnabled`, `HeartbeatIntervalMinutes`, `HeartbeatIncludeScreenshot`, and `HeartbeatSystemPrompt` directly
- `CityAgentUISystem` will need to add a `PendingHeartbeatResult` poll alongside the existing `ClaudeAPISystem.PendingResult` poll (per D-02)
- No blockers

## Known Stubs
None - all 4 fields are wired to real `ModSetting` properties with correct defaults and locale entries. No placeholder values flow to UI rendering.

## Self-Check: PASSED
- `src/Settings.cs` exists and contains all 4 heartbeat fields, kHeartbeatGroup, DefaultHeartbeatSystemPrompt
- `src/Mod.cs` exists and contains HeartbeatSystem scheduling line
- Commit `142e79e` exists in git log

---
*Phase: 06-proactive-heartbeat*
*Completed: 2026-03-30*
