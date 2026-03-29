---
phase: 03-extended-city-data-tools
plan: 01
subsystem: api
tags: [ecs, csharp, budget, traffic, services, city-data, tools]

# Dependency graph
requires:
  - phase: 02-chat-ui-polish
    provides: CityDataSystem with population/demand properties; ICityAgentTool pattern
provides:
  - CityDataSystem with 30 new cached properties (budget x14, loans x4, traffic x3, services x9)
  - GetBudgetTool (get_budget) — per-category income/expense breakdown with loan data
  - GetTrafficSummaryTool (get_traffic_summary) — flow score, road count, bottleneck count
  - GetServicesSummaryTool (get_services_summary) — electricity, water, sewage, health metrics
  - Unity.Collections reference in CityAgent.csproj
affects: [03-02, ClaudeAPISystem tool registration, CityToolRegistry tool list]

# Tech tracking
tech-stack:
  added:
    - Unity.Collections (Allocator.TempJob for Road component data array)
    - Game.City.IncomeSource / ExpenseSource enums
    - Game.Simulation.ICityServiceBudgetSystem, ILoanSystem, IElectricityStatisticsSystem, IWaterStatisticsSystem
    - Game.UI.InGame.ServiceBudgetUISystem, Game.Tools.LoanSystem
    - Game.Net.Road component, Game.Net.Bottleneck component
    - Game.Citizens.HealthProblem component
  patterns:
    - System reference stored as GameSystemBase, cast to interface in OnUpdate (avoids hard dependency on concrete type)
    - Unavailability sentinel: BudgetAvailable bool + -1f TrafficFlowScore; tools check these before serializing
    - ToComponentDataArray<T>(Allocator.TempJob) + Dispose() pattern for Road aggregate reads

key-files:
  created:
    - src/Systems/Tools/GetBudgetTool.cs
    - src/Systems/Tools/GetTrafficSummaryTool.cs
    - src/Systems/Tools/GetServicesSummaryTool.cs
  modified:
    - src/Systems/CityDataSystem.cs
    - src/CityAgent.csproj

key-decisions:
  - "Store system refs as GameSystemBase, cast to interface in OnUpdate — safe even if the concrete type is renamed in future CS2 versions"
  - "BudgetAvailable bool guards GetBudgetTool — returns status=unavailable during early city load before ServiceBudgetUISystem has data"
  - "TrafficFlowScore = -1f sentinel when no roads exist (city start); tool checks > -0.5f to detect this"
  - "Unity.Collections.dll added to csproj — required for Allocator.TempJob used in Road ToComponentDataArray; was pre-existing gap"

patterns-established:
  - "Pattern: System unavailability fallback — bool flag (BudgetAvailable) or sentinel value (-1f) in CityDataSystem; tool checks the flag and returns {status: 'unavailable'} object rather than error string (D-16)"
  - "Pattern: NativeArray lifecycle — ToComponentDataArray<T>(Allocator.TempJob) immediately followed by .Dispose() after use to prevent memory leaks (D-22 anti-pattern guard)"

requirements-completed: [DATA-01, DATA-02, DATA-03, DATA-04]

# Metrics
duration: 5min
completed: 2026-03-29
---

# Phase 03 Plan 01: Extended City Data Tools — ECS Queries and Tool Classes Summary

**CityDataSystem extended with 30 new ECS properties (budget, loans, traffic, services) and three new tool classes giving Claude structured access to city finances, road conditions, and service coverage**

## Performance

- **Duration:** 5 min
- **Started:** 2026-03-29T18:09:42Z
- **Completed:** 2026-03-29T18:14:00Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- CityDataSystem extended with 30 new public cached properties covering city budget (14 fields), active loans (4 fields), traffic flow (3 fields), and utility/health services (9 fields) — all refreshed on the existing 128-frame throttle (D-22)
- Three new ICityAgentTool implementations created: GetBudgetTool with per-category income/expense breakdown and loan section (D-01, D-17, D-20), GetTrafficSummaryTool with flow score and bottleneck count (D-03), GetServicesSummaryTool with electricity/water/sewage/health sections (D-05, D-06)
- All tools implement D-16 unavailability fallback: GetBudgetTool returns `{status:"unavailable"}` when BudgetAvailable=false; GetTrafficSummaryTool returns `flow_score:"unavailable"` when no roads (TrafficFlowScore=-1f)
- Unity.Collections reference added to csproj to support Allocator.TempJob in Road component array reads

## Task Commits

1. **Task 1: Extend CityDataSystem with budget, traffic, and services ECS queries** — `447732b` (feat)
2. **Task 2: Create GetBudgetTool, GetTrafficSummaryTool, and GetServicesSummaryTool** — `83497c1` (feat)

## Files Created/Modified

- `src/Systems/CityDataSystem.cs` — Added 30 new public properties, 4 new system refs (m_BudgetSystem, m_LoanSystem, m_ElectricitySystem, m_WaterSystem), 3 new entity queries (m_RoadQuery, m_BottleneckQuery, m_SickCitizenQuery), 4 new using directives (Game.City, Game.Net, Game.UI.InGame, Unity.Collections), full budget/loan/traffic/services read blocks inside the existing 128-frame throttle
- `src/CityAgent.csproj` — Added Unity.Collections.dll reference (required for Allocator.TempJob)
- `src/Systems/Tools/GetBudgetTool.cs` — New: `get_budget` tool returning balance, income breakdown by zone type, expense breakdown by category, and loan info; returns unavailable status when BudgetAvailable=false
- `src/Systems/Tools/GetTrafficSummaryTool.cs` — New: `get_traffic_summary` tool returning flow_score (or "unavailable"), total_roads, bottleneck_count
- `src/Systems/Tools/GetServicesSummaryTool.cs` — New: `get_services_summary` tool returning electricity, water, sewage, and health sub-objects

## Decisions Made

- System refs stored as `GameSystemBase`, cast to interface (`ICityServiceBudgetSystem`, `ILoanSystem`, etc.) in OnUpdate — decouples from concrete type names and is safe if CS2 renames the implementing class
- `BudgetAvailable` bool guards the budget tool rather than null-checking the balance — explicit intent, not coincidental zero balance
- `TrafficFlowScore = -1f` sentinel for no-roads state (city start); tool uses `> -0.5f` check to detect; -0.5 threshold avoids false positive from very low legitimate scores
- Added `Unity.Collections.dll` to csproj — it was a pre-existing gap that only became visible when CityDataSystem used `Allocator.TempJob`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Unity.Collections reference to CityAgent.csproj**
- **Found during:** Task 1 (CityDataSystem verification build)
- **Issue:** `ToComponentDataArray<Road>(Allocator.TempJob)` uses `AllocatorManager.AllocatorHandle` from `Unity.Collections.dll`, which was not referenced in the project file — build error CS0012
- **Fix:** Added `<Reference Include="Unity.Collections"><HintPath>$(ManagedDir)\Unity.Collections.dll</HintPath><Private>False</Private></Reference>` to CityAgent.csproj
- **Files modified:** `src/CityAgent.csproj`
- **Verification:** `dotnet build -c Release` exits 0 with 0 warnings
- **Committed in:** `447732b` (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking build issue)
**Impact on plan:** Required missing DLL reference; no scope creep or architecture change.

## Issues Encountered

- Build environment in worktree requires `CS2_INSTALL_PATH` to be set as an exported env var for dotnet CLI to locate game DLLs — without it, 130+ "namespace not found" errors. Used `export CS2_INSTALL_PATH=...` prefix on dotnet commands throughout. This is a pre-existing environment requirement, not a new issue.

## Known Stubs

None — all three tools read from live CityDataSystem properties. No hardcoded or placeholder data in the tool outputs.

## User Setup Required

None — no external service configuration required. The new tools will become available to Claude as soon as they are registered in CityToolRegistry (Phase 3 Plan 02).

## Next Phase Readiness

- All three tool classes implement ICityAgentTool and are ready to be registered in `CityToolRegistry.OnCreate()`
- CityDataSystem properties are named consistently with what the tool files reference
- Phase 3 Plan 02 (tool registration, settings toggles, system prompt update) can proceed immediately

---
*Phase: 03-extended-city-data-tools*
*Completed: 2026-03-29*

## Self-Check: PASSED

- FOUND: src/Systems/CityDataSystem.cs
- FOUND: src/Systems/Tools/GetBudgetTool.cs
- FOUND: src/Systems/Tools/GetTrafficSummaryTool.cs
- FOUND: src/Systems/Tools/GetServicesSummaryTool.cs
- FOUND: .planning/phases/03-extended-city-data-tools/03-01-SUMMARY.md
- FOUND commit: 447732b (Task 1 — CityDataSystem extensions)
- FOUND commit: 83497c1 (Task 2 — three tool files)
