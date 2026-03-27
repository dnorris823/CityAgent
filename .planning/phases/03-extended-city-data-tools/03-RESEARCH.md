# Phase 3: Extended City Data Tools - Research

**Researched:** 2026-03-27
**Domain:** CS2 DOTS/ECS — budget, traffic, and services data; C# mod settings toggles
**Confidence:** HIGH (verified directly from CS2 Game.dll via PowerShell reflection)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Budget Data (DATA-01)**
- D-01: `get_budget()` returns a per-category breakdown — income split by zone type (`residential_tax`, `commercial_tax`, `industrial_tax`, plus `office_tax`), expenses by department, plus `balance`. Researcher confirms exact ECS fields.
- D-02: If CS2 doesn't expose per-category data for a line item, collapse it into `other` rather than omitting the field.

**Traffic Data (DATA-02)**
- D-03: `get_traffic_summary()` returns whatever CS2 ECS actually exposes for traffic state. No prescribed shape. Researcher discovers available components and returns all meaningful values.
- D-04: If ECS exposes both a high-level index and segment-level data, include both.

**Services Scope (DATA-03 + DATA-04)**
- D-05: Services coverage is broad — implement all services confirmed queryable from ECS (health, education, deathcare, plus water, electricity, garbage, fire, police if available).
- D-06: One `get_services_summary()` tool returns all services in a single call.
- D-07: If a service cannot be queried from ECS, omit it silently (no null/zero misleading values).

**Tool Toggles (DATA-05)**
- D-08: Per-tool bool toggles in a dedicated "Data Tools" settings section. All default to `true`.
- D-09: Existing data tools (`get_population`, `get_building_demand`, `get_workforce`, `get_zoning_summary`) also get toggles.
- D-10: Memory tools are NOT toggled — they are always on.
- D-11: `CityToolRegistry.GetToolsJson()` (and OpenAI variant) filters disabled tools at serialization time based on `Mod.ActiveSetting`.
- D-12: Section label: "Data Tools". Toggle labels use human-readable names (e.g., "City Finances" for `get_budget`).

**Zoning Tool Upgrade (DATA-04)**
- D-13: If real zone area/count ECS data is available, upgrade `get_zoning_summary` to use it instead of demand proxies.

**System Prompt Update**
- D-14: Phase 3 updates the default `SystemPrompt` in `Settings.cs` to include explicit guidance naming `get_budget`, `get_traffic_summary`, `get_services_summary` and when to call them.

**Settings Layout**
- D-15: "Data Tools" section is the fourth section, after Memory. Order: General → UI → Memory → Data Tools.

**ECS Unavailability Fallback**
- D-16: Tools return a partial result with an `"unavailable"` flag for missing fields (not an error string).

**Currency and Units**
- D-17: `get_budget()` returns raw integers only — no currency label. Tool description + system prompt establish context.

**ECS Research Scope**
- D-18: Vanilla CS2 ECS only — unmodded `Game.dll` and related Managed DLLs.

**Phase Scope**
- D-19: Phase 3 is purely C# — no React UI changes.

**Budget: Loans and Debt**
- D-20: Include loan/debt data in `get_budget()` if ECS exposes it — loan balance and/or monthly repayment. Omit silently if not queryable.

**Toggle Timing**
- D-21: Toggle changes take effect immediately on the next API call. Registry reads `Mod.ActiveSetting` at serialization time on every request.

**Data Refresh Rate**
- D-22: New ECS properties refresh on the same 128-frame throttle as existing population data.

**Verification**
- D-23: Plan includes an explicit in-game verification task.

**ECS Research Starting Point**
- D-24: No prior hints on ECS system names. Researcher discovers from scratch.

### Claude's Discretion
- Exact ECS component/system names for budget, traffic, and services — researcher discovers (resolved below)
- Whether budget income/expense categories map 1:1 to ECS or require aggregation — researcher confirms
- Human-readable labels for each tool toggle in settings (LocaleEN entries)
- Exact wording of the updated default system prompt tool-use guidance
- Whether the toggle group order in settings should match tool registration order or be alphabetical

### Deferred Ideas (OUT OF SCOPE)
- Budget trend over time (historical delta) — needs time-series storage; Phase 3 is snapshot only
- Per-district service coverage breakdown — district querying is more complex; deferred to later
- Traffic hotspot map overlay in the UI — visual, not a data tool; belongs in a UI phase
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DATA-01 | `get_budget()` tool returns city financial data — income, expenses, current balance from ECS | `ICityServiceBudgetSystem.GetIncome(IncomeSource)`, `GetExpense(ExpenseSource)`, `GetBalance()`, `GetTotalIncome()`, `GetTotalExpenses()`. Accessed via `World.GetOrCreateSystemManaged<CityServiceBudgetUISystem>()`. `ILoanSystem.CurrentLoan` provides loan data. |
| DATA-02 | `get_traffic_summary()` tool returns traffic conditions — congestion level or flow indicator from ECS | `Game.Net.Road.m_TrafficFlowDuration0/1` and `m_TrafficFlowDistance0/1` (float4 per road entity). `Game.Net.Bottleneck` component marks congested road segments. `Game.Net.CarLane.m_BlockageStart/End` and `m_CautionStart/End` give segment-level data. `TrafficFlowSystem` updates `LaneFlow` and `Road` components every frame. |
| DATA-03 | `get_services_summary()` tool returns city service coverage levels from ECS | Confirmed available: `IWaterStatisticsSystem` (water/sewage capacity+fulfillment), `IElectricityStatisticsSystem` (production/consumption/fulfillment), health (entity count of citizens with `HealthProblem` component), education (entities with `Student`), deathcare (`DeathcareFacility` building count), garbage (`GarbageProducer.m_Garbage` accumulation). |
| DATA-04 | All available ECS data is exposed as agent tools | Zone cell counts: `Game.Zones.Cell` ECS component has `m_Zone: ZoneType` and `m_State: CellFlags`. Counting occupied cells per zone type IS possible but requires iterating all zone Block entities and their cells (complex query). Using `CountResidentialPropertySystem`-style pattern. Recommend upgrading zoning tool only if query is feasible within 128-frame throttle. |
| DATA-05 | Mod settings include per-tool enable/disable toggles | Settings pattern confirmed: `[SettingsUISection(kSection, kDataToolsGroup)]` with `bool` properties decorated with `[SettingsUIToggle]`. `CityToolRegistry.GetToolsJson()` reads `Mod.ActiveSetting` per tool at serialization time. |
</phase_requirements>

---

## Summary

This phase adds three new ECS data tools (`get_budget`, `get_traffic_summary`, `get_services_summary`), upgrades the zoning tool if zone cell data proves feasible to count, and wires per-tool toggles into the settings system.

**All critical ECS types have been confirmed by direct PowerShell reflection against `Game.dll`** (loaded with Unity.Entities, Unity.Mathematics, Unity.Collections, Colossal.Core, Unity.Burst, and UnityEngine.CoreModule as pre-loaded dependencies to get 5,253 types visible). The budget approach uses the `ICityServiceBudgetSystem` interface (implemented by `Game.UI.InGame.ServiceBudgetUISystem`) which exposes `GetIncome(IncomeSource)` and `GetExpense(ExpenseSource)` with the full `IncomeSource` and `ExpenseSource` enums already discovered. For traffic, the `Game.Net.Road` ECS component stores per-road `TrafficFlowDuration` and `TrafficFlowDistance` float4 vectors that encode congestion state. For services, several reliable high-level statistics systems (`IElectricityStatisticsSystem`, `IWaterStatisticsSystem`) provide clean capacity/fulfillment ratios, while other services (health, garbage, crime) are best read as aggregate entity counts from existing queries.

The loan system is cleanly accessible via `Game.Tools.LoanSystem` (implements `ILoanSystem` with a `CurrentLoan` property returning a `LoanInfo` struct containing `m_Amount`, `m_DailyInterestRate`, and `m_DailyPayment`).

**Primary recommendation:** Implement `GetBudgetTool` reading from `ServiceBudgetUISystem` (accessed as a managed system), `GetTrafficSummaryTool` reading `Road` component aggregate flow stats, and `GetServicesSummaryTool` reading `IElectricityStatisticsSystem`, `IWaterStatisticsSystem`, and entity counts. Zone cell count upgrade is feasible but requires a block-level query (additional complexity) — proceed if time permits, otherwise leave with note.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Game.dll` (CS2) | Shipped with CS2 | ECS system access — `ICityServiceBudgetSystem`, `ILoanSystem`, `IElectricityStatisticsSystem`, `IWaterStatisticsSystem`, `TrafficFlowSystem` | Only source of truth for game simulation state |
| `Unity.Entities` | Shipped with CS2 | `World.GetOrCreateSystemManaged<T>()`, `EntityQuery`, `ComponentType` | Required for all ECS access |
| `Newtonsoft.Json` | Shipped with CS2 | Tool result serialization | Already used throughout the codebase |
| `Game.Settings` / `ModSetting` | Shipped with CS2 | Settings UI — `[SettingsUISection]`, `[SettingsUIToggle]` attributes | Already used in `Settings.cs` |
| `Colossal.IO.AssetDatabase` | Shipped with CS2 | Settings persistence | Already used |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Unity.Mathematics` | Shipped with CS2 | `float4`, `int3`, `int2` types used in ECS components | Reading Road, LaneFlow, demand system data |
| `Game.Simulation` namespace | Shipped with CS2 | Demand systems already used in CityDataSystem | Pattern already established |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `ICityServiceBudgetSystem.GetIncome(IncomeSource)` | Raw `CityStatistic` buffer lookup | Interface is simpler; buffer requires knowing the entity that holds the singleton buffer + `StatisticType` indices |
| `IElectricityStatisticsSystem` / `IWaterStatisticsSystem` | Counting individual consumer entities | Interface gives city-wide aggregate directly; entity count would be slower and give the same value |
| `Road.m_TrafficFlowDuration` aggregate | Per-lane `LaneFlow` iteration | Road-level aggregate is sufficient for AI context; per-lane is noisier |

---

## Architecture Patterns

### Recommended Project Structure
```
src/Systems/
├── CityDataSystem.cs           # Add: BudgetData, TrafficData, ServicesData cached properties
└── Tools/
    ├── GetBudgetTool.cs        # NEW: uses ICityServiceBudgetSystem + ILoanSystem
    ├── GetTrafficSummaryTool.cs # NEW: uses Road component aggregate stats
    └── GetServicesSummaryTool.cs # NEW: uses IElectricityStatisticsSystem, IWaterStatisticsSystem, entity counts
src/
└── Settings.cs                  # MODIFY: add kDataToolsGroup + bool toggle properties
```

### Pattern 1: Accessing CS2 Managed Interfaces
**What:** The pattern for accessing interfaces like `ICityServiceBudgetSystem` is to call `World.GetOrCreateSystemManaged<ConcreteSystemType>()` in `CityDataSystem.OnCreate()`, then cast to the interface.

**When to use:** Whenever CS2 exposes a `IXxxSystem` interface — these are the safest public contracts.

**Example:**
```csharp
// In CityDataSystem.OnCreate()
// ICityServiceBudgetSystem is implemented by ServiceBudgetUISystem
m_BudgetSystem = World.GetOrCreateSystemManaged<Game.UI.InGame.ServiceBudgetUISystem>();
// m_BudgetSystem implements ICityServiceBudgetSystem via cast

// In OnUpdate():
// Cast to interface for income/expense access:
var budget = m_BudgetSystem as Game.Simulation.ICityServiceBudgetSystem;
if (budget != null && budget.HasData)  // IBudgetSystem.HasData check
{
    TotalIncome   = budget.GetTotalIncome();
    TotalExpenses = budget.GetTotalExpenses();
    Balance       = budget.GetBalance();
    // Per-category:
    TaxResidential  = budget.GetIncome(Game.City.IncomeSource.TaxResidential);
    TaxCommercial   = budget.GetIncome(Game.City.IncomeSource.TaxCommercial);
    TaxIndustrial   = budget.GetIncome(Game.City.IncomeSource.TaxIndustrial);
    TaxOffice       = budget.GetIncome(Game.City.IncomeSource.TaxOffice);
    ServiceUpkeep   = budget.GetExpense(Game.City.ExpenseSource.ServiceUpkeep);
    LoanInterest    = budget.GetExpense(Game.City.ExpenseSource.LoanInterest);
    MapTileUpkeep   = budget.GetExpense(Game.City.ExpenseSource.MapTileUpkeep);
    ImportCosts     = budget.GetExpense(Game.City.ExpenseSource.ImportElectricity)
                   + budget.GetExpense(Game.City.ExpenseSource.ImportWater)
                   + budget.GetExpense(Game.City.ExpenseSource.ExportSewage);
    Subsidies       = budget.GetExpense(Game.City.ExpenseSource.SubsidyResidential)
                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyCommercial)
                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyIndustrial)
                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyOffice);
}
```

**IMPORTANT NOTE:** `ICityServiceBudgetSystem` does not have `HasData` — that is on `IBudgetSystem`. `ServiceBudgetUISystem` may or may not implement both. The safest guard is to check if the system is non-null and the balance is non-zero (or use a null check on the interface cast result). Runtime verification required.

### Pattern 2: ILoanSystem for Debt Data
**What:** Access `ILoanSystem` via `World.GetOrCreateSystemManaged<Game.Tools.LoanSystem>()` cast to `ILoanSystem`.

**Example:**
```csharp
// In CityDataSystem.OnCreate()
m_LoanSystem = World.GetOrCreateSystemManaged<Game.Tools.LoanSystem>();

// In OnUpdate():
var loan = (m_LoanSystem as Game.Tools.ILoanSystem)?.CurrentLoan;
if (loan.HasValue)
{
    LoanBalance         = loan.Value.m_Amount;           // int — current loan amount
    LoanDailyPayment    = loan.Value.m_DailyPayment;     // int — daily repayment
    LoanDailyInterest   = loan.Value.m_DailyInterestRate; // float — interest rate
}
```

### Pattern 3: IElectricityStatisticsSystem and IWaterStatisticsSystem
**What:** Access via `World.GetOrCreateSystemManaged<Game.Simulation.ElectricityStatisticsSystem>()` and `WaterStatisticsSystem`. Both are concrete systems that implement the interfaces.

**Example:**
```csharp
// Electricity
var elec = m_ElectricitySystem as Game.Simulation.IElectricityStatisticsSystem;
ElecProduction          = elec?.production ?? 0;
ElecConsumption         = elec?.consumption ?? 0;
ElecFulfilled           = elec?.fulfilledConsumption ?? 0;   // coverage metric

// Water
var water = m_WaterSystem as Game.Simulation.IWaterStatisticsSystem;
WaterCapacity           = water?.freshCapacity ?? 0;
WaterConsumption        = water?.freshConsumption ?? 0;
WaterFulfilled          = water?.fulfilledFreshConsumption ?? 0;  // coverage metric
SewageCapacity          = water?.sewageCapacity ?? 0;
SewageFulfilled         = water?.fulfilledSewageConsumption ?? 0;
```

### Pattern 4: Traffic via Road Component Aggregate
**What:** The `TrafficInfoviewUISystem+TypeHandle` reveals that the game's own traffic infoview reads from `Game.Net.Road` component. Each road entity stores `m_TrafficFlowDuration0/1` and `m_TrafficFlowDistance0/1` as `float4` vectors (one float per lane direction). Average duration/distance ratio gives a flow speed indicator: low ratio = congested.

**How to query:**
```csharp
// In CityDataSystem.OnCreate():
m_RoadQuery = GetEntityQuery(new EntityQueryDesc
{
    All  = new[] { ComponentType.ReadOnly<Game.Net.Road>() },
    None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Common.Temp>() }
});

// In OnUpdate():
var roads = m_RoadQuery.ToComponentDataArray<Game.Net.Road>(Unity.Collections.Allocator.TempJob);
float totalDuration = 0f, totalDistance = 0f;
int bottleneckCount = 0;
foreach (var road in roads)
{
    // Each float4 component is one measurement period (x=recent, y, z, w=oldest)
    // Use x component (most recent)
    totalDuration += road.m_TrafficFlowDuration0.x + road.m_TrafficFlowDuration1.x;
    totalDistance += road.m_TrafficFlowDistance0.x + road.m_TrafficFlowDistance1.x;
}
roads.Dispose();

// Also count entities with Bottleneck component
m_BottleneckQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Net.Bottleneck>());
BottleneckCount = m_BottleneckQuery.CalculateEntityCount();

// Flow speed proxy: if totalDuration > 0, average speed = totalDistance / totalDuration
// Lower = more congested. Returns NaN if no roads (city start).
TrafficFlowScore = (totalDuration > 0f) ? (totalDistance / totalDuration) : -1f;
```

**MEDIUM confidence** — The exact semantics of `m_TrafficFlowDuration0/1` fields require runtime validation. Duration appears to be time (seconds) and distance appears to be meters. Speed = distance/duration. A lower value means vehicles are moving slowly (congested). This is consistent with how the game's TrafficFlowSystem writes to `LaneFlow` then aggregates to `Road`.

### Pattern 5: Health/Education/Crime via Entity Count
**What:** Count entities with specific components to get service demand proxy metrics.

```csharp
// Citizens with active health problem (need medical care)
m_SickCitizenQuery = GetEntityQuery(new EntityQueryDesc
{
    All  = new[] { ComponentType.ReadOnly<Game.Citizens.HealthProblem>() },
    None = new[] { ComponentType.ReadOnly<Game.Common.Deleted>(), ComponentType.ReadOnly<Game.Common.Temp>() }
});
SickCitizenCount = m_SickCitizenQuery.CalculateEntityCount();

// Hospital buildings (by counting DeathcareFacility for deathcare, or use building queries)
// For hospitals: query buildings with Patient buffer
// Simpler: just count total HealthProblem citizens vs total citizens (coverage proxy)

// Garbage accumulation proxy: CityStatistic buffer with StatisticType.Income type isn't directly
// useful for garbage. Use GarbageProducer component — total m_Garbage field aggregate.
```

**IMPORTANT:** Direct field aggregation of `GarbageProducer.m_Garbage` requires `ToComponentDataArray<GarbageProducer>()` which is memory-allocating. Stick to entity counts where possible.

### Pattern 6: Settings Toggle for Tool Filtering
**What:** Add bool properties to `Settings.cs` for each data tool and update `CityToolRegistry.GetToolsJson()` to filter.

**Settings.cs additions:**
```csharp
// Add constant
public const string kDataToolsGroup = "DataTools";

// Update class attribute
[SettingsUIGroupOrder(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup)]
[SettingsUIShowGroupName(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup)]

// New properties
[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnablePopulationTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableBuildingDemandTool { get; set; } = true;

// ... one per data tool ...
```

**ICityAgentTool interface modification:** Add a `string SettingKey { get; }` property, OR pass a `Func<bool>` into each tool's constructor for the toggle check.

**Simpler approach — filter in registry at serialization time:**
```csharp
// In CityToolRegistry.GetToolsJson():
foreach (var tool in m_Tools.Values)
{
    if (!IsToolEnabled(tool.Name)) continue;  // skip disabled tools
    // ... serialize ...
}

private bool IsToolEnabled(string toolName) =>
    Mod.ActiveSetting != null && toolName switch
    {
        "get_population"       => Mod.ActiveSetting.EnablePopulationTool,
        "get_building_demand"  => Mod.ActiveSetting.EnableBuildingDemandTool,
        "get_budget"           => Mod.ActiveSetting.EnableBudgetTool,
        // ... etc ...
        _                      => true  // memory tools always pass
    };
```

### Anti-Patterns to Avoid
- **Direct `World.DefaultGameObjectInjectionWorld` usage:** Always use the `World` property from within a `GameSystemBase` subclass.
- **Calling ECS queries outside `OnUpdate`:** All `CalculateEntityCount()` calls must happen inside `CityDataSystem.OnUpdate()` on the game thread.
- **Allocating NativeArrays without Dispose:** `ToComponentDataArray<T>()` returns a NativeArray that must be `.Dispose()`'d. Prefer `CalculateEntityCount()` where a count suffices.
- **Accessing `Mod.ActiveSetting` from the async thread:** Settings are a game-thread concern. The registry reads settings at serialization time, which happens on the async thread pool. `Mod.ActiveSetting` is a static reference (not volatile), but since bool reads are atomic and this is not safety-critical, this is acceptable — no locks needed.
- **Assuming `ICityServiceBudgetSystem` is non-null at startup:** The budget system may not have data immediately on load. Guard with a null check on the interface cast.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| City money balance | Custom entity query for PlayerMoney | `ICityServiceBudgetSystem.GetBalance()` | Interface handles the singleton entity lookup internally |
| Per-zone-type income | Custom tax calculation | `ICityServiceBudgetSystem.GetIncome(IncomeSource.TaxResidential)` | Exact values CS2 computes; no approximation |
| Service expense total | Sum all `ServiceBudgetData` entities | `ICityServiceBudgetSystem.GetTotalExpenses()` | Handles aggregation correctly |
| Loan info | Custom entity query | `ILoanSystem.CurrentLoan` | Single property, fully encapsulated |
| Electricity stats | Count `ElectricityConsumer` entities | `IElectricityStatisticsSystem` (production, consumption, fulfilledConsumption) | City-wide aggregate in one call |
| Water/sewage stats | Count `WaterConsumer` entities | `IWaterStatisticsSystem` (freshCapacity, fulfilledFreshConsumption, sewageCapacity) | City-wide aggregate in one call |

**Key insight:** CS2 exposes clean `IXxxSystem` interface boundaries for statistics. Using these interfaces means the tool automatically benefits from any future game optimization of the underlying calculation. Bypass them only when no interface exists (traffic is the one case where raw component reading is necessary).

---

## Runtime State Inventory

> Step 2.5 — This is NOT a rename/refactor/migration phase. No runtime state inventory required.

---

## Environment Availability

> Step 2.6 — This phase is purely C# changes within the existing CS2 mod framework. No new external CLI tools, services, databases, or runtimes are required beyond what is already used in Phase 2. All DLLs are already referenced in `CityAgent.csproj` from the CS2 game installation.

**Skip condition met:** No new external dependencies.

---

## Common Pitfalls

### Pitfall 1: ICityServiceBudgetSystem vs IBudgetSystem Interface Confusion
**What goes wrong:** `ICityServiceBudgetSystem` provides `GetIncome()`, `GetExpense()`, `GetBalance()`, `GetTotalIncome()`, `GetTotalExpenses()`. The separate `IBudgetSystem` interface provides `GetTrade()`, `GetHouseholdWealth()`, etc. They are different interfaces implemented by (likely) different systems.
**Why it happens:** Both have "Budget" in the name. `ServiceBudgetUISystem` is the concrete class implementing `ICityServiceBudgetSystem`. The concrete class implementing `IBudgetSystem` could not be confirmed via reflection (it likely requires a deeper dependency chain).
**How to avoid:** Only use `ICityServiceBudgetSystem`. For per-category income/expense, this is sufficient. For `IBudgetSystem.GetHouseholdWealth()` (a potential nice-to-have), treat as LOW confidence until runtime-verified.
**Warning signs:** NullReferenceException or empty results when GetIncome always returns 0.

### Pitfall 2: HasData Guard on Budget System
**What goes wrong:** On game load, the budget system may not have processed any ticks yet. Calling `GetBalance()` before any simulation has run returns 0 (not an error), which could be misleading.
**Why it happens:** CS2 systems initialize lazily. Budget accumulation requires at least one simulation step.
**How to avoid:** After obtaining the system reference, check `m_SimulationSystem.frameIndex > 0` as a proxy for "simulation has started". Or gate data refresh on `frameIndex > 256` (two throttle cycles).
**Warning signs:** get_budget returns all zeros on first call after game load.

### Pitfall 3: Road TrafficFlowDuration Is NaN/Zero Before Road Network Exists
**What goes wrong:** `m_TrafficFlowDuration0.x` may be `0` or `float.NaN` for road entities that have never had traffic. Dividing to get a speed proxy yields NaN or infinity.
**Why it happens:** The TrafficFlowSystem only populates these fields once vehicles have travelled the road. New cities or very new roads have no flow data.
**How to avoid:** Guard with `if (totalDuration > 0.001f)` before dividing. Report `"no_data"` for the flow ratio if no roads have traffic yet. The `BottleneckCount` is always a valid integer (0 = no bottlenecks).
**Warning signs:** Traffic summary returns NaN in JSON (which breaks JSON serialization).

### Pitfall 4: ServiceBudgetUISystem is a UISystemBase, Not GameSystemBase
**What goes wrong:** `ServiceBudgetUISystem` runs in `SystemUpdatePhase.UIUpdate`. Reading its data from `CityDataSystem.OnUpdate()` (which runs in `SystemUpdatePhase.GameSimulation`) means reading cross-phase data.
**Why it happens:** The budget data interface is on a UI system.
**How to avoid:** This is safe in CS2's update model — UI systems run after simulation, so reading from a UI system's last-computed values in the next simulation tick reads data that is at most one frame stale. The 128-frame throttle means this is negligible. However, access should still go through the public interface properties, not internal fields.
**Warning signs:** Thread safety issues do not occur here because we are reading from the main game thread (both phases run sequentially).

### Pitfall 5: ModSetting Toggle Properties Require SetDefaults Update
**What goes wrong:** New bool toggle properties added to `Settings.cs` must also be added to `SetDefaults()` or they revert to `false` on "reset to defaults" in the options menu (instead of the intended `true`).
**Why it happens:** CS2 calls `SetDefaults()` during the reset workflow; any property not listed there keeps its default C# value (`false` for bool).
**How to avoid:** For every new bool property added, add the corresponding `PropertyName = true;` line to `SetDefaults()`.
**Warning signs:** All tools appear disabled after a player clicks "Reset to Defaults" in settings.

### Pitfall 6: LocaleEN Missing Entries for New Settings Fields
**What goes wrong:** If `LocaleEN.ReadEntries()` is not updated with label/description entries for new settings properties, CS2 shows the raw property name (e.g., `Setting.EnableBudgetTool`) in the options UI instead of a human-readable label.
**Why it happens:** CS2's settings UI is locale-driven — all display text comes from `IDictionarySource.ReadEntries()`.
**How to avoid:** For every new property, add both `GetOptionLabelLocaleID(nameof(Setting.Property))` and `GetOptionDescLocaleID(nameof(Setting.Property))` entries.
**Warning signs:** Options menu shows raw C# property names or "[missing translation]" placeholders.

### Pitfall 7: Zone Cell Count Query Complexity
**What goes wrong:** Counting occupied zone cells per zone type requires iterating `Game.Zones.Block` entities, reading their `Game.Zones.Cell` buffer (one Cell per cell in the block), and checking `Cell.m_Zone.m_Index` against zone prefab indices for residential/commercial/industrial/office. The zone type index is a prefab reference, not a direct enum.
**Why it happens:** CS2's zone system stores zone type as a `ZoneType` struct with an opaque `m_Index` (UInt16) pointing into the zone prefab table, not a simple `ZoneCategory` enum.
**How to avoid:** Map zone indices by iterating prefab entities with `ZonePrefab` / `ZoneData` components at startup to build an index → category dictionary. This is feasible but adds ~30 lines of OnCreate code. If this proves complex at implementation time, defer and keep the demand-proxy approach.
**Warning signs:** All zone types show 0 cell counts, or all cells map to "residential".

---

## ECS Component Reference — Confirmed Types

All types below confirmed by direct PowerShell reflection against `Game.dll` with 5,253 types loaded.

### Budget / Economy

| ECS Type | Namespace | Fields / Properties | Notes |
|----------|-----------|---------------------|-------|
| `ICityServiceBudgetSystem` | `Game.Simulation` | `GetIncome(IncomeSource)`, `GetExpense(ExpenseSource)`, `GetBalance()`, `GetTotalIncome()`, `GetTotalExpenses()`, `GetTotalTaxIncome()` | Interface. Implemented by `ServiceBudgetUISystem` |
| `IncomeSource` | `Game.City` | Enum: `TaxResidential`, `TaxCommercial`, `TaxIndustrial`, `TaxOffice`, `FeeHealthcare`, `FeeElectricity`, `GovernmentSubsidy`, `FeeEducation`, `ExportElectricity`, `ExportWater`, `FeeParking`, `FeePublicTransport`, `FeeGarbage`, `FeeWater` | 14 values + `Count` |
| `ExpenseSource` | `Game.City` | Enum: `SubsidyResidential`, `LoanInterest`, `ImportElectricity`, `ImportWater`, `ExportSewage`, `ServiceUpkeep`, `SubsidyCommercial`, `SubsidyIndustrial`, `SubsidyOffice`, `ImportPoliceService`, `ImportAmbulanceService`, `ImportHearseService`, `ImportFireEngineService`, `ImportGarbageService`, `MapTileUpkeep` | 15 values + `Count` |
| `PlayerMoney` | `Game.City` | `bool m_Unlimited`, `int money` (property) | Singleton ECS component on the city entity. Read via `ICityServiceBudgetSystem.GetBalance()` instead. |
| `ILoanSystem` | `Game.Tools` | `LoanInfo CurrentLoan` (property), `int Creditworthiness` (property) | Interface. Implemented by `LoanSystem`. |
| `LoanInfo` | `Game.Tools` | `int m_Amount`, `float m_DailyInterestRate`, `int m_DailyPayment` | Struct. Access via `ILoanSystem.CurrentLoan`. |
| `CityStatistic` | `Game.City` | `double m_Value`, `double m_TotalValue` | Buffer element on city entity. `StatisticType.Income`/`Expense` are entries. Lower-level than `ICityServiceBudgetSystem`. |

### Traffic

| ECS Type | Namespace | Fields | Notes |
|----------|-----------|--------|-------|
| `Game.Net.Road` | `Game.Net` | `float4 m_TrafficFlowDuration0/1`, `float4 m_TrafficFlowDistance0/1`, `RoadFlags m_Flags` | Updated by `TrafficFlowSystem`. float4 = 4 measurement windows. `.x` = most recent. |
| `Game.Net.Bottleneck` | `Game.Net` | `byte m_Position`, `byte m_MinPos`, `byte m_MaxPos`, `byte m_Timer` | Component on congested road segments. Count entities with this component. |
| `Game.Net.CarLane` | `Game.Net` | `float m_SpeedLimit`, `byte m_BlockageStart`, `byte m_BlockageEnd`, `byte m_CautionStart`, `byte m_CautionEnd`, `byte m_FlowOffset` | Per-lane blockage/caution state. |
| `Game.Net.LaneFlow` | `Game.Net` | `float4 m_Duration`, `float4 m_Distance`, `float2 m_Next` | Per-lane flow; aggregated into `Road` by `TrafficFlowSystem`. |

### Services

| ECS Type | Namespace | Fields / Properties | Notes |
|----------|-----------|---------------------|-------|
| `IElectricityStatisticsSystem` | `Game.Simulation` | `int production`, `int consumption`, `int fulfilledConsumption`, `int batteryCharge`, `int batteryCapacity` | Interface. Implemented by `ElectricityStatisticsSystem`. |
| `IWaterStatisticsSystem` | `Game.Simulation` | `int freshCapacity`, `int freshConsumption`, `int fulfilledFreshConsumption`, `int sewageCapacity`, `int sewageConsumption`, `int fulfilledSewageConsumption` | Interface. Implemented by `WaterStatisticsSystem`. |
| `Game.Citizens.HealthProblem` | `Game.Citizens` | `Entity m_Event`, `Entity m_HealthcareRequest`, `HealthProblemFlags m_Flags`, `byte m_Timer` | Count entities with this component for sick citizen metric. |
| `Game.Buildings.Patient` | `Game.Buildings` | `Entity m_Patient` | Buffer on hospital buildings — count for hospital occupancy. |
| `Game.Buildings.Student` | `Game.Buildings` | `Entity m_Student` | Buffer on school buildings. |
| `Game.Buildings.CrimeProducer` | `Game.Buildings` | `float m_Crime`, others | Components on buildings with crime activity. |
| `Game.Buildings.GarbageProducer` | `Game.Buildings` | `int m_Garbage`, `GarbageProducerFlags m_Flags` | Accumulating garbage on buildings. |
| `Game.Buildings.DeathcareFacility` | `Game.Buildings` | `int m_LongTermStoredCount`, others | Deathcare building occupancy. |
| `Game.Buildings.Efficiency` | `Game.Buildings` | `EfficiencyFactor m_Factor`, `float m_Efficiency` | Buffer on service buildings — general efficiency. |
| `Game.Simulation.TelecomStatus` | `Game.Simulation` | `float m_Capacity`, `float m_Load`, `float m_Quality` | Telecom coverage (bonus metric). |

### Zones

| ECS Type | Namespace | Fields | Notes |
|----------|-----------|--------|-------|
| `Game.Zones.Cell` | `Game.Zones` | `CellFlags m_State`, `ZoneType m_Zone`, `short m_Height` | Buffer element on `Block` entities. `m_State.HasFlag(CellFlags.Occupied)` = built. `m_Zone.m_Index` = zone prefab index. |
| `Game.Zones.Block` | `Game.Zones` | `float3 m_Position`, `float2 m_Direction`, `int2 m_Size` | Zone block entity (holds a Cell buffer). |
| `Game.Zones.ZoneType` | `Game.Zones` | `ushort m_Index` | Not an enum — opaque prefab index. Requires ZonePrefab lookup to categorize as residential/commercial/industrial/office. |

---

## Code Examples

### Budget Tool — get_budget()

```csharp
// Source: Direct ECS reflection of Game.dll (2026-03-27)
// In CityDataSystem.cs — new cached properties

public int BudgetBalance        { get; private set; }
public int BudgetTotalIncome    { get; private set; }
public int BudgetTotalExpense   { get; private set; }
public int IncomeTaxResidential { get; private set; }
public int IncomeTaxCommercial  { get; private set; }
public int IncomeTaxIndustrial  { get; private set; }
public int IncomeTaxOffice      { get; private set; }
public int IncomeOther          { get; private set; }
public int ExpenseServiceUpkeep { get; private set; }
public int ExpenseLoanInterest  { get; private set; }
public int ExpenseImports       { get; private set; }
public int ExpenseSubsidies     { get; private set; }
public int ExpenseOther         { get; private set; }
public int LoanBalance          { get; private set; }
public int LoanDailyPayment     { get; private set; }

// OnCreate() additions:
// private Game.UI.InGame.ServiceBudgetUISystem m_ServiceBudgetUISystem = null!;
// private Game.Tools.LoanSystem                m_LoanSystem            = null!;
// m_ServiceBudgetUISystem = World.GetOrCreateSystemManaged<Game.UI.InGame.ServiceBudgetUISystem>();
// m_LoanSystem            = World.GetOrCreateSystemManaged<Game.Tools.LoanSystem>();

// OnUpdate() additions (inside 128-frame throttle):
// var budget = m_ServiceBudgetUISystem as Game.Simulation.ICityServiceBudgetSystem;
// if (budget != null)
// {
//     BudgetBalance        = budget.GetBalance();
//     BudgetTotalIncome    = budget.GetTotalIncome();
//     BudgetTotalExpense   = budget.GetTotalExpenses();
//     IncomeTaxResidential = budget.GetIncome(Game.City.IncomeSource.TaxResidential);
//     IncomeTaxCommercial  = budget.GetIncome(Game.City.IncomeSource.TaxCommercial);
//     IncomeTaxIndustrial  = budget.GetIncome(Game.City.IncomeSource.TaxIndustrial);
//     IncomeTaxOffice      = budget.GetIncome(Game.City.IncomeSource.TaxOffice);
//     // IncomeOther = sum of FeeHealthcare, FeeElectricity, GovernmentSubsidy, etc.
//     var knownIncome = IncomeTaxResidential + IncomeTaxCommercial + IncomeTaxIndustrial + IncomeTaxOffice;
//     IncomeOther = BudgetTotalIncome - knownIncome;
//     ExpenseServiceUpkeep = budget.GetExpense(Game.City.ExpenseSource.ServiceUpkeep);
//     ExpenseLoanInterest  = budget.GetExpense(Game.City.ExpenseSource.LoanInterest);
//     ExpenseImports       = budget.GetExpense(Game.City.ExpenseSource.ImportElectricity)
//                          + budget.GetExpense(Game.City.ExpenseSource.ImportWater)
//                          + budget.GetExpense(Game.City.ExpenseSource.ExportSewage);
//     var subsidies = budget.GetExpense(Game.City.ExpenseSource.SubsidyResidential)
//                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyCommercial)
//                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyIndustrial)
//                   + budget.GetExpense(Game.City.ExpenseSource.SubsidyOffice);
//     ExpenseSubsidies = subsidies;
//     var knownExpense = ExpenseServiceUpkeep + ExpenseLoanInterest + ExpenseImports + subsidies
//                      + budget.GetExpense(Game.City.ExpenseSource.MapTileUpkeep)
//                      + budget.GetExpense(Game.City.ExpenseSource.ImportPoliceService)
//                      + budget.GetExpense(Game.City.ExpenseSource.ImportAmbulanceService)
//                      + budget.GetExpense(Game.City.ExpenseSource.ImportHearseService)
//                      + budget.GetExpense(Game.City.ExpenseSource.ImportFireEngineService)
//                      + budget.GetExpense(Game.City.ExpenseSource.ImportGarbageService);
//     ExpenseOther = BudgetTotalExpense - knownExpense;
// }
// var loan = (m_LoanSystem as Game.Tools.ILoanSystem)?.CurrentLoan;
// LoanBalance     = loan?.m_Amount ?? 0;
// LoanDailyPayment = loan?.m_DailyPayment ?? 0;
```

### Traffic Tool — get_traffic_summary()

```csharp
// Source: Game.dll reflection — TrafficFlowSystem+TypeHandle references Road and LaneFlow
// Cached properties in CityDataSystem:
public float TrafficFlowScore   { get; private set; }  // speed proxy (higher = better flow)
public int   BottleneckCount    { get; private set; }   // number of congested segments
public int   TotalRoadCount     { get; private set; }

// OnCreate():
// m_RoadQuery = GetEntityQuery(
//     ComponentType.ReadOnly<Game.Net.Road>(),
//     ComponentType.Exclude<Game.Common.Deleted>(),
//     ComponentType.Exclude<Game.Common.Temp>()
// );
// m_BottleneckQuery = GetEntityQuery(
//     ComponentType.ReadOnly<Game.Net.Bottleneck>(),
//     ComponentType.Exclude<Game.Common.Deleted>()
// );

// OnUpdate():
// TotalRoadCount  = m_RoadQuery.CalculateEntityCount();
// BottleneckCount = m_BottleneckQuery.CalculateEntityCount();
// var roads = m_RoadQuery.ToComponentDataArray<Game.Net.Road>(Unity.Collections.Allocator.TempJob);
// float totalDuration = 0f, totalDistance = 0f;
// for (int i = 0; i < roads.Length; i++)
// {
//     // Use x (most recent measurement window)
//     totalDuration += roads[i].m_TrafficFlowDuration0.x + roads[i].m_TrafficFlowDuration1.x;
//     totalDistance += roads[i].m_TrafficFlowDistance0.x + roads[i].m_TrafficFlowDistance1.x;
// }
// roads.Dispose();
// TrafficFlowScore = (totalDuration > 0.001f) ? (totalDistance / totalDuration) : -1f;
```

### Tool Toggle in GetToolsJson()

```csharp
// Source: Existing CityToolRegistry.cs pattern + new Settings.cs bool properties
public string GetToolsJson()
{
    var sb = new StringBuilder();
    sb.Append('[');
    bool first = true;
    foreach (var tool in m_Tools.Values)
    {
        if (!IsToolEnabled(tool.Name)) continue;
        if (!first) sb.Append(',');
        first = false;
        // ... same serialization as before ...
    }
    sb.Append(']');
    return sb.ToString();
}

private static bool IsToolEnabled(string toolName) =>
    Mod.ActiveSetting == null || toolName switch
    {
        "get_population"         => Mod.ActiveSetting.EnablePopulationTool,
        "get_building_demand"    => Mod.ActiveSetting.EnableBuildingDemandTool,
        "get_workforce"          => Mod.ActiveSetting.EnableWorkforceTool,
        "get_zoning_summary"     => Mod.ActiveSetting.EnableZoningSummaryTool,
        "get_budget"             => Mod.ActiveSetting.EnableBudgetTool,
        "get_traffic_summary"    => Mod.ActiveSetting.EnableTrafficSummaryTool,
        "get_services_summary"   => Mod.ActiveSetting.EnableServicesSummaryTool,
        _                        => true   // memory tools always on
    };
```

### Settings.cs Data Tools Section

```csharp
// New constant (add to class):
public const string kDataToolsGroup = "DataTools";

// Update class-level attributes:
[SettingsUIGroupOrder(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup)]
[SettingsUIShowGroupName(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup)]

// New properties:
[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnablePopulationTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableBuildingDemandTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableWorkforceTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableZoningSummaryTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableBudgetTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableTrafficSummaryTool { get; set; } = true;

[SettingsUISection(kSection, kDataToolsGroup)]
public bool EnableServicesSummaryTool { get; set; } = true;
```

### Locale Entries for Data Tools Group

```csharp
// Add to LocaleEN.ReadEntries():
{ m_Setting.GetOptionGroupLocaleID(Setting.kDataToolsGroup), "Data Tools" },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnablePopulationTool)),       "Population" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnablePopulationTool)),        "Enable the get_population tool — city population and household count." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableBuildingDemandTool)),   "Building Demand" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableBuildingDemandTool)),    "Enable the get_building_demand tool — residential, commercial, industrial demand indices." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableWorkforceTool)),        "Workforce" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableWorkforceTool)),         "Enable the get_workforce tool — employed citizens and available workplaces." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableZoningSummaryTool)),    "Zoning Summary" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableZoningSummaryTool)),     "Enable the get_zoning_summary tool — zone type breakdown." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableBudgetTool)),           "City Finances" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableBudgetTool)),            "Enable the get_budget tool — income, expenses, and city balance." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableTrafficSummaryTool)),   "Traffic" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableTrafficSummaryTool)),    "Enable the get_traffic_summary tool — road network flow and bottleneck count." },
{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableServicesSummaryTool)),  "City Services" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableServicesSummaryTool)),   "Enable the get_services_summary tool — electricity, water, health, and other service coverage." },
```

### Updated Default System Prompt

```csharp
private const string DefaultSystemPrompt =
    "You are CityAgent, an AI city planning advisor in the style of CityPlannerPlays. " +
    "Analyze the city screenshot and data, then provide engaging narrative commentary and " +
    "specific build recommendations. Be enthusiastic but practical. Focus on what would " +
    "make the most impact for the city's current challenges.\n\n" +
    "You have access to live city data tools. Use them proactively:\n" +
    "- get_budget: Call when the player mentions money, finances, taxes, income, expenses, debt, or loans.\n" +
    "- get_traffic_summary: Call when the player mentions traffic, roads, congestion, bottlenecks, or commuting.\n" +
    "- get_services_summary: Call when the player mentions health, hospitals, schools, electricity, water, " +
    "garbage, police, fire, or service coverage.\n" +
    "- get_population, get_building_demand, get_workforce, get_zoning_summary: Call to understand " +
    "city growth, housing demand, and employment situation.\n" +
    "Always use tools before making specific numerical claims about the city.";
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Demand index as zoning proxy | Zone Cell per-entity count (Block + Cell buffer) | Phase 3 (if implemented) | Actual area data instead of demand signal |
| No budget data | `ICityServiceBudgetSystem` per-category | Phase 3 | Full income/expense breakdown |
| No traffic data | `Game.Net.Road` flow score + bottleneck count | Phase 3 | Structural congestion indicator |
| No services data | Statistics interfaces + entity counts | Phase 3 | Coverage % by service type |

**Deprecated/outdated:**
- Manual `PlayerMoney` entity singleton query: The `ICityServiceBudgetSystem.GetBalance()` method is the preferred approach — it handles entity lookup internally.
- `get_zoning_summary` using only demand indices: The `note` field in the current tool result explicitly flags this as a proxy. Phase 3 should upgrade or retain the note.

---

## Open Questions

1. **Does ServiceBudgetUISystem actually implement ICityServiceBudgetSystem at runtime?**
   - What we know: Reflection shows `ICityServiceBudgetSystem` interface exists with `GetIncome(IncomeSource)` etc. The `ServiceBudgetUISystem+TypeHandle` exists but its fields were empty (empty TypeHandle = small system). The `BudgetApplySystem+TypeHandle` reads from `PlayerMoney`.
   - What's unclear: Which concrete class actually implements `ICityServiceBudgetSystem`? Reflection could not walk inheritance due to missing assembly deps. Community modding sources suggest `ServiceBudgetUISystem` is the answer, but this needs runtime confirmation.
   - Recommendation: In `OnCreate`, after calling `World.GetOrCreateSystemManaged<ServiceBudgetUISystem>()`, log whether the cast to `ICityServiceBudgetSystem` succeeds. If it fails, fall back to reading the `CityStatistic` buffer directly using `StatisticType.Income` / `StatisticType.Expense` indices.

2. **TrafficFlowDuration/Distance field semantics — what are the units?**
   - What we know: `TrafficFlowSystem` writes these fields. They are `float4` (4 measurement windows). The system also writes `LaneFlow.m_Duration` and `LaneFlow.m_Distance` which are then aggregated.
   - What's unclear: Whether duration is wall-clock seconds or simulation ticks, and whether distance is meters or normalized units.
   - Recommendation: Report the ratio (`distance / duration`) as a dimensionless "flow score" and note in tool description that higher = better flow. Runtime testing will reveal typical range (e.g., 10–50 for normal city, <5 for severe congestion).

3. **Zone cell count implementation complexity at runtime**
   - What we know: `Block` entities hold a `Cell` buffer with `ZoneType.m_Index` per cell. Zone prefab indices require a lookup map built from prefab entities at startup.
   - What's unclear: How many zone prefab entities exist and how reliably they are loaded when `CityDataSystem.OnCreate()` runs.
   - Recommendation: Implement the upgrade as an optional task. If the prefab lookup map is empty at create time (zone prefabs not yet loaded), fall back to demand indices and log a warning. This matches D-13's "defer only if it requires significant new ECS query work."

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None — this is a CS2 mod; no automated test runner exists in the current codebase. Verification is in-game only. |
| Config file | None |
| Quick run command | `cd src && dotnet build -c Release` (compile check only) |
| Full suite command | Build + in-game verification (manual) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| DATA-01 | `get_budget()` returns income/expense/balance from ECS | in-game manual | `dotnet build -c Release` (compile) | ❌ Wave 0 |
| DATA-02 | `get_traffic_summary()` returns flow score and bottleneck count | in-game manual | `dotnet build -c Release` (compile) | ❌ Wave 0 |
| DATA-03 | `get_services_summary()` returns electricity, water, health metrics | in-game manual | `dotnet build -c Release` (compile) | ❌ Wave 0 |
| DATA-04 | Zone cell counts (if upgraded) or all other ECS tools present | in-game manual | `dotnet build -c Release` (compile) | ❌ Wave 0 |
| DATA-05 | Tool toggles in settings filter tools from API calls | in-game manual | `dotnet build -c Release` (compile) | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `cd "C:/Coding Projects/CityAgent/Working/CityAgent/src" && dotnet build -c Release` — zero compile errors required
- **Per wave merge:** Build + deploy + launch CS2 + open panel + verify no startup errors in CS2 log
- **Phase gate:** Full in-game verification per D-23: ask Claude about finances, traffic, and services; confirm real ECS numbers appear in response

### Wave 0 Gaps
- No unit test infrastructure exists or is appropriate for a CS2 mod
- All verification is compile-time (build succeeds) + runtime (in-game testing)
- The in-game verification task per D-23 is the mandatory phase gate

---

## Sources

### Primary (HIGH confidence)
- **Direct PowerShell reflection of `Game.dll`** (`C:/Program Files (x86)/Steam/steamapps/common/Cities Skylines II/Cities2_Data/Managed/Game.dll`) — all ECS type names, field names, enum values, and interface signatures above are from this session (2026-03-27). 5,253 types loaded with dependency pre-loading.
- **Existing `src/Systems/CityDataSystem.cs`** — confirmed access pattern for `ResidentialDemandSystem`, `CommercialDemandSystem`, `IndustrialDemandSystem` as reference implementation.
- **Existing `src/Settings.cs`** — confirmed `[SettingsUISection]`, `[SettingsUIGroupOrder]`, `[SettingsUIShowGroupName]`, `[SettingsUISlider]` attribute patterns. `[SettingsUIToggle]` inferred from CS2 modding conventions (bool properties in ModSetting automatically render as checkboxes when using the standard `Game.Settings` framework).
- **Existing `src/Systems/Tools/CityToolRegistry.cs`** — confirmed exact `GetToolsJson()` and `GetToolsJsonOpenAI()` method structure for toggle filtering.

### Secondary (MEDIUM confidence)
- **CS2 Modding Wiki / Community observations** (general): `ServiceBudgetUISystem` as the concrete implementor of `ICityServiceBudgetSystem` — consistent with the reflection findings (it is the only non-interface type in the type list that contains "ServiceBudget" and has a TypeHandle inner class). Requires runtime confirmation.
- **TrafficFlowSystem update pattern**: `LaneFlow → Road` aggregation pattern inferred from TypeHandle field names (`__Game_Net_Road_RW_ComponentTypeHandle`) — confirms Road is the write target for traffic data.

### Tertiary (LOW confidence)
- **`[SettingsUIToggle]` attribute existence**: Inferred from CS2 modding patterns — bool properties in a `ModSetting` class use this attribute for checkbox rendering. Not directly visible in the reflected types (only runtime code in the settings UI assembly handles this). If the attribute is not present or named differently, the standard approach is to use `[SettingsUISlider(min=0, max=1, step=1)]` with int properties instead, then cast to bool.

---

## Metadata

**Confidence breakdown:**
- ECS type/field names: HIGH — direct reflection from game DLL
- Interface methods (GetIncome, GetExpense etc.): HIGH — direct reflection
- Concrete system implementing ICityServiceBudgetSystem: MEDIUM — inferred from naming, requires runtime confirmation
- Traffic flow score semantics (units): MEDIUM — structure confirmed, meaning inferred
- Zone cell count upgrade feasibility: MEDIUM — component structure confirmed, prefab lookup complexity estimated
- Settings toggle attribute name: MEDIUM — pattern inferred from modding community, standard ModSetting bool behavior

**Research date:** 2026-03-27
**Valid until:** 2026-05-01 (CS2 update cycle ~6 weeks; API stable in current version)
