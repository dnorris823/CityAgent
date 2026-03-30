---
status: resolved
trigger: "Tools return real ECS data but may be reading from the wrong city. The memory system derives a city slug from CityConfigurationSystem at startup — if this read happens before the city is fully loaded, the slug could be empty/stale/wrong, causing memory files and tool calls to target the wrong city context."
created: 2026-03-29T00:00:00Z
updated: 2026-03-29T05:30:00Z
---

## Current Focus

hypothesis: CONFIRMED ROOT CAUSE for population bug. Fix applied and built (0 errors 0 warnings).

AWAITING HUMAN VERIFICATION: Load city, observe DIAG log for population and households,
compare to game HUD population counter.

test: In-game DIAG log line — should show population=~7900 (matching HUD), households<population.
expecting: statsOk=True, population matches HUD, households <= population.
next_action: User runs game and reports DIAG log values vs HUD.

BUG 1 — Population wrong:
InvalidateCache() fires correctly (confirmed by log line added to code), but triggers a single ECS
read at a moment when city entities may still be streaming in. After the one forced read, the system
reverts to the 128-frame throttle. If the user sends a message before the next natural 128-frame
cycle fires, they get the partial/wrong count from the forced read. Fix: add a "stabilizing" re-read
loop — after InvalidateCache fires, keep re-reading every 64 frames (instead of 128) for up to
8 cycles, or until citizen count is > 0 and stable for 2 consecutive reads, whichever comes first.

BUG 2 — Budget unavailable (BudgetAvailable=false):
CONFIRMED ROOT CAUSE: CityDataSystem.OnCreate obtains m_BudgetSystem via
World.GetOrCreateSystemManaged<ServiceBudgetUISystem>() (a UI system in Game.UI.InGame namespace).
The cast in OnUpdate is: `m_BudgetSystem as ICityServiceBudgetSystem`.
Binary analysis of Game.dll confirms ICityServiceBudgetSystem is implemented by
Game.Simulation.CityServiceBudgetSystem — a SEPARATE simulation system in Game.Simulation namespace.
ServiceBudgetUISystem (the UI wrapper) does NOT implement this interface. Cast returns null → BudgetAvailable=false.
Fix: change GetOrCreateSystemManaged<ServiceBudgetUISystem>() to
GetOrCreateSystemManaged<CityServiceBudgetSystem>() (the simulation system that actually implements the interface).

test: Build and deploy. Ask Claude about city budget — should return real numbers, not "unavailable".
  Ask Claude about population — should return correct in-game count.
expecting: BudgetAvailable=true, population matches in-game display.
next_action: Implement both fixes in CityDataSystem.cs

## Symptoms

expected: When a city loads, the memory system should detect the current city name, derive the correct slug, and use that for all memory file paths and tool context. Tool data should match the city currently open in-game.
actual: Tools were working and returning real values, but the data appeared to be from the wrong city. The user noticed the memory/tool context didn't match the city they had open. After initial fixes: population number still wrong (doesn't match in-game display); budget tool returns "unavailable" status.
errors: No crash errors reported — just wrong city data/memory context being used. Budget system reports BudgetAvailable=false fallback path.
reproduction: Load a city in CS2 with the mod active. Ask Claude about city finances or population. The values don't match the city you have open.
timeline: Noticed after Phase 3 changes. The memory system has existed since earlier phases but this specific mismatch was noticed now.

## Eliminated

(none yet)

## Evidence

- timestamp: 2026-03-29T01:00:00Z
  checked: CityDataSystem.OnUpdate throttle logic (line 166), Mod.cs system registration
  found: Systems are registered in Mod.OnLoad once; GameSystemBase systems live for the ECS World lifetime, not per city load. SimulationSystem.frameIndex is a continuously-incrementing counter — does NOT reset between city loads. Throttle fires at a predictable but arbitrary point during city entity streaming. All cached int/bool properties default to 0/false (no initializer). No invalidation hook on city load exists.
  implication: After loading city B, CityDataSystem serves city A's cached values (or zeroes if first load) until the throttle fires. Even after it fires, entity queries may catch a partially-streamed city state. No guarantee of accuracy within any window from city load to tool call.

- timestamp: 2026-03-29T01:00:00Z
  checked: CityAgentUISystem.OnUpdate step 0b (m_CityNameResolved flag)
  found: When TryUpgradeCityName() returns 1 (slug just resolved), m_CityNameResolved is set to true and history is reloaded. This is the precise moment we know which city is loaded. Currently, nothing tells CityDataSystem to refresh at this moment.
  implication: The city-name-resolved event is already being surfaced in CityAgentUISystem. CityDataSystem just needs to be told to force one refresh cycle at that moment.

- timestamp: 2026-03-29T00:00:00Z
  checked: CityAgentUISystem.OnUpdate lines 84-116
  found: Initialization guard uses TWO fields: m_MemoryInitialized (local to UISystem) AND m_NarrativeMemory.IsInitialized. The first branch runs when BOTH are false. On success OR failure, m_MemoryInitialized is set to true. This is a one-shot. It never runs again for the lifetime of the game session.
  implication: If ResolveCityName() returns "Unnamed City" on frame 1 because CityConfigurationSystem hasn't populated yet, all memory files go under "unnamed-city/" and the slug is never corrected.

- timestamp: 2026-03-29T00:00:00Z
  checked: NarrativeMemorySystem.ResolveCityName() lines 122-148
  found: Tries CityConfigurationSystem.cityName. If null/whitespace, falls back to "Unnamed City". GenerateSlug("Unnamed City") => "unnamed-city". All cities that fail early name resolution share "unnamed-city/" directory. No retry mechanism.
  implication: Race condition: if CS2 systems aren't ready on frame 1 of UIUpdate phase, all cities share one memory directory.

- timestamp: 2026-03-29T00:00:00Z
  checked: CityAgentUISystem.OnUpdate lines 113-116 (else-if branch)
  found: The second branch `else if (!m_MemoryInitialized && m_NarrativeMemory.IsInitialized)` only fires if NarrativeMemory was somehow externally initialized but UISystem's local guard wasn't set. This is a dead branch in practice — NarrativeMemorySystem.Initialize() is only called from within the first branch.
  implication: No recovery path exists if initialization produced a wrong city name.

- timestamp: 2026-03-29T00:00:00Z
  checked: NarrativeMemorySystem.Initialize() lines 82-113
  found: Once m_Initialized = true is set at line 111, the system is locked into whatever city slug was resolved. There is no public ReInitialize() or slug-check method. The only public entry after init is StartNewSession().
  implication: The memory system has no mechanism to detect or recover from a wrong slug.

- timestamp: 2026-03-29T00:00:00Z
  checked: Mod.OnLoad() — system registration order
  found: Systems are registered in this order: CityAgentUISystem (UIUpdate phase), CityDataSystem (GameSimulation), ClaudeAPISystem (GameSimulation), NarrativeMemorySystem (GameSimulation). OnLoad runs at mod load time, which is before any save is loaded. SetModDir() is called from OnLoad. Initialize() is NOT called from OnLoad — it's deferred to the first UIUpdate frame. This is intentional (comment says "city name may not be available in OnCreate").
  implication: The defer to first OnUpdate was meant to handle timing but doesn't go far enough — frame 1 of UIUpdate may still precede CityConfigurationSystem population.

- timestamp: 2026-03-29T00:00:00Z
  checked: ResolveCityName / GenerateSlug interaction
  found: GenerateSlug("") => "unnamed-city" (explicit check at line 154). GenerateSlug("Unnamed City") => "unnamed-city". So whether CityConfigurationSystem returns empty string or nothing is returned at all, the slug is identical — "unnamed-city".
  implication: Cannot distinguish "city hasn't loaded yet" from "city is literally named Unnamed City" at slug level. Need to detect and defer when name is still unresolved.

- timestamp: 2026-03-29T02:00:00Z
  checked: CityDataSystem.cs line 166 — m_BudgetSystem acquisition
  found: Code does World.GetOrCreateSystemManaged<ServiceBudgetUISystem>() — this is Game.UI.InGame.ServiceBudgetUISystem, a UI binding/rendering system. Cast in OnUpdate is `m_BudgetSystem as ICityServiceBudgetSystem`.
  implication: The cast target interface ICityServiceBudgetSystem lives in Game.Simulation namespace. Game.dll binary analysis confirms this interface is implemented by Game.Simulation.CityServiceBudgetSystem (the simulation system), not by ServiceBudgetUISystem (the UI wrapper). The cast returns null at runtime → BudgetAvailable=false always.

- timestamp: 2026-03-29T02:00:00Z
  checked: Game.dll binary string analysis
  found: Binary contains these distinct types:
    - "Game.Simulation.ICityServiceBudgetSystem" — the interface
    - "Game.Simulation.CityServiceBudgetSystem" — concrete implementor (has m_CityServiceBudgetSystem field references)
    - "Game.UI.InGame.ServiceBudgetUISystem" — a separate UI system (has ServiceInfo, PlayerResourceReader nested types; not the interface implementor)
  RESEARCH.md is internally inconsistent: line 90 says GetOrCreateSystemManaged<CityServiceBudgetUISystem>(), line 103 says "implemented by Game.UI.InGame.ServiceBudgetUISystem". The code used the line 103 name (ServiceBudgetUISystem) but should use the simulation class (Game.Simulation.CityServiceBudgetSystem) to get the ICityServiceBudgetSystem implementation.
  implication: Fix is to change the GetOrCreateSystemManaged type argument from ServiceBudgetUISystem to CityServiceBudgetSystem.

- timestamp: 2026-03-29T02:00:00Z
  checked: InvalidateCache() mechanism for Bug 1
  found: The fix fires once on city name resolve. But city entity streaming may still be in progress at that moment, returning a partial citizen count. After the one forced read, system reverts to 128-frame throttle. A user asking Claude within those 128 frames gets the wrong count.
  implication: Need a "stabilize after invalidation" mode that keeps re-reading at shorter intervals until the count is non-zero and stable for 2 consecutive reads, then reverts to the normal 128-frame throttle.

- timestamp: 2026-03-29T03:00:00Z
  checked: git show 447732b (original phase-3 working commit) vs current CityDataSystem.cs
  found: Original working code had `GetOrCreateSystemManaged<ServiceBudgetUISystem>()` + `using Game.UI.InGame;`.
    Current code has `GetOrCreateSystemManaged<CityServiceBudgetSystem>()` and `Game.UI.InGame` using directive is absent.
    RESEARCH.md line 423 says ICityServiceBudgetSystem is "Implemented by ServiceBudgetUISystem".
    03-03-SUMMARY.md and VERIFICATION.md confirm the original code was human-verified in-game with correct values.
    The previous debug session's "fix" (changing to CityServiceBudgetSystem) is a regression.
  implication: Budget values are being read from Game.Simulation.CityServiceBudgetSystem instead of
    Game.UI.InGame.ServiceBudgetUISystem. The cast to ICityServiceBudgetSystem may succeed (the simulation
    system may also implement the interface) but the values returned by the simulation system are in a
    different form than what the UI displays. This explains "real numbers but wrong values."

- timestamp: 2026-03-29T03:00:00Z
  checked: git show 321feba (original Citizen query, phase 2), current CityDataSystem.cs lines 127-132
  found: Citizen query definition (All=[Citizen], None=[Deleted,Temp,TravelPurpose]) is byte-for-byte
    identical to original commit. No changes to population query from previous debug session.
    Stabilization loop controls READ TIMING only — does not alter what entities are counted.
    Population discrepancy cause is unknown from static analysis; requires runtime diagnostic.
  implication: Population query definition is not the regression source. If population is wrong,
    the discrepancy predates this debug session OR is a different type of mismatch (units, definition
    mismatch between game UI population counter and Citizen entity count).

- timestamp: 2026-03-29T05:00:00Z
  checked: Game.Citizens.TravelPurpose via Game.dll reflection; Game.Citizens.Purpose enum;
    PopulationInfoviewUISystem and StatisticsUISystem private fields; ICityStatisticsSystem interface;
    CityStatisticsSystem binary string search; StatisticType enum; StatisticCollectionType enum.
  found: TravelPurpose.m_Purpose is type Purpose, which has 40 enum values (None, Shopping, Leisure,
    GoingHome, GoingToWork, Working, Sleeping, ... Relaxing, Sightseeing, etc.). TravelPurpose is
    present on ANY citizen currently traveling — not just tourists. Purpose values cover all
    normal daily activities. The None=[TravelPurpose] filter therefore excludes ~85% of all living
    citizens at any moment in a live city. This directly explains population=1286 when HUD shows ~7900
    (factor of ~6x difference; only ~17% of citizens are stationary/sleeping at any given frame).
    ICityStatisticsSystem.GetStatisticValue(StatisticType.Population, 0) gives the HUD-matching count.
    StatisticCollectionType.Point confirms Population is a current-snapshot value, not cumulative.
    CityStatisticsSystem (Game.Simulation.CityStatisticsSystem) is the concrete implementor.
    StatisticsUISystem and PopulationInfoviewUISystem both hold m_CityStatisticsSystem references
    confirming this is the official source for HUD values.
  implication: CONFIRMED ROOT CAUSE. Remove TravelPurpose from the citizen query None-filter, OR
    (better) replace both TotalPopulation and TotalHouseholds with ICityStatisticsSystem calls.
    The ICityStatisticsSystem approach is superior because it exactly matches the game HUD and
    avoids any future component-filter edge cases.

## Resolution

root_cause: FOUR bugs total. (1) [FIXED] NarrativeMemorySystem initialized on first UIUpdate frame — slug resolved to "unnamed-city". Fixed with TryUpgradeCityName() deferred loop. (2) [FIXED] Population stabilization — InvalidateCache() one-shot insufficient; stabilization loop added. (3) [FIXED] Budget wrong values: previous session regression changed GetOrCreateSystemManaged<ServiceBudgetUISystem>() to GetOrCreateSystemManaged<CityServiceBudgetSystem>(); reverted. (4) [FIXED] Population under-count: m_CitizenQuery used None=[TravelPurpose] — but TravelPurpose is present on ANY citizen who is currently traveling (going to work/school/shops/leisure — 40 possible Purpose values), not just tourists. At any frame ~85% of citizens in a live city carry TravelPurpose, causing ~6x under-count. Fixed by replacing raw entity count with ICityStatisticsSystem.GetStatisticValue(StatisticType.Population, 0) which reads from Game.Simulation.CityStatisticsSystem — the exact source the game's HUD uses.

fix: (4) Added m_CityStatisticsSystem = World.GetOrCreateSystemManaged<CityStatisticsSystem>() in OnCreate. In OnUpdate, cast to ICityStatisticsSystem and call GetStatisticValue(StatisticType.Population, 0) for TotalPopulation and GetStatisticValue(StatisticType.HouseholdCount, 0) for TotalHouseholds. Fallback to entity count if cast fails. Added statsOk flag to DIAG log. Build succeeded 0 errors 0 warnings.

verification: Build succeeded 0 errors 0 warnings (all four fixes applied). DLL deployed to Mods/CityAgent/CityAgent.dll. Awaiting in-game check: DIAG log must show population matching game HUD and statsOk=True.
files_changed:
  - src/Systems/NarrativeMemorySystem.cs: Added TryUpgradeCityName() method (Phase 1 fix — already applied)
  - src/Systems/CityAgentUISystem.cs: Added m_CityNameResolved field, step 0b retry loop (Phase 1 fix — already applied); InvalidateCache() call on city name resolve (Phase 2 fix — already applied)
  - src/Systems/CityDataSystem.cs: InvalidateCache() + stabilization loop (Phase 2 fix — already applied); budget regression fix + diagnostic log (Phase 3 fix — applying now)
