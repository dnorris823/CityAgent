# Phase 3 Context: Extended City Data Tools

**Phase**: 3 — Extended City Data Tools
**Goal**: The advisor has structured access to budget, traffic, and services data — and the player controls which tools are active
**Session**: 2026-03-27
**Requirements**: DATA-01, DATA-02, DATA-03, DATA-04, DATA-05

---

<decisions>
## Implementation Decisions

### Budget Data (DATA-01)
- **D-01:** `get_budget()` returns a **per-category breakdown**, not just summary totals.
  - Income split by zone type: `residential_tax`, `commercial_tax`, `industrial_tax`
  - Expenses by department: `roads`, `health`, `education`, `other` (catch-all for remaining)
  - Plus `balance` (net total)
  - Researcher confirms exact ECS field names and adds/removes categories based on what CS2 actually exposes.
- **D-02:** If CS2 doesn't expose per-category data for a line item, collapse it into `other` rather than omitting the field. Claude handles a partial breakdown better than no breakdown.

### Traffic Data (DATA-02)
- **D-03:** `get_traffic_summary()` returns **whatever CS2 ECS actually exposes** for traffic state.
  No prescribed shape. The researcher discovers the available traffic ECS components/systems and
  returns all meaningful values. Claude will interpret whatever structured data comes back.
- **D-04:** If ECS exposes both a high-level index and segment-level data, include both. Don't
  filter down to a single number — richer data gives Claude more to work with.

### Services Scope (DATA-03 + DATA-04)
- **D-05:** Services coverage is **broad** — implement all services the researcher confirms are
  queryable from CS2 ECS. This covers the success-criteria minimum (health, education, deathcare)
  **plus** any others confirmed available: water, electricity, garbage, fire, police, etc.
- **D-06:** One `get_services_summary()` tool returns all services in a single call (not one tool
  per service). Structure: an array or map of service → coverage metric.
  Example shape (actual fields confirmed by researcher):
  ```json
  {
    "health": { "coverage": 82, "facilities": 4 },
    "education": { "coverage": 71, "facilities": 6 },
    "deathcare": { "coverage": 55, "facilities": 1 }
  }
  ```
- **D-07:** If a service cannot be queried from ECS (no available component), omit it silently
  rather than returning null/zero values that would mislead Claude.

### Tool Toggles (DATA-05)
- **D-08:** Per-tool bool toggles in a dedicated **"Data Tools"** settings section. One toggle per
  tool. All tools default to **enabled** (`true`).
- **D-09:** Existing data tools also get toggles: `get_population`, `get_building_demand`,
  `get_workforce`, `get_zoning_summary` — not just the new Phase 3 tools. This is the
  comprehensive toggle surface for all city data tools.
- **D-10:** The **memory tools** (`read_memory_file`, `write_memory_file`, etc.) are **not**
  toggled — they are always on. Toggles apply only to city data tools.
- **D-11:** `CityToolRegistry.GetToolsJson()` (and OpenAI variant) must respect toggle state:
  disabled tools are **not included** in the tools array sent to the API. The registry filters
  at serialization time based on `Mod.ActiveSetting`.
- **D-12:** Settings section label: **"Data Tools"**. Toggle labels use the tool's human-readable
  name (e.g., "City Finances" for `get_budget`), not the raw function name.

### Zoning Tool Upgrade (DATA-04)
- **D-13:** If the researcher confirms real zone area/count ECS data is available, **upgrade `get_zoning_summary`** to use it instead of demand indices as proxies. Budget/traffic/services are the primary deliverables; zoning upgrade is in-scope if the data is there — defer only if it requires significant new ECS query work.

### System Prompt Update
- **D-14:** Phase 3 **updates the default `SystemPrompt`** in `Settings.cs` to include explicit tool-use guidance — naming `get_budget`, `get_traffic_summary`, `get_services_summary` and when to call them. The goal is reliable, unprompted tool use when players ask about finances, traffic, or services. Planner writes the updated default prompt text.

### Settings Layout
- **D-15:** "Data Tools" section is the **fourth section**, after Memory. Settings order: General → UI → Memory → Data Tools. Power-user toggle surface at the end.

### ECS Unavailability Fallback
- **D-16:** If an ECS system or component isn't available at runtime, tools return a **partial result with an `"unavailable"` flag** for the missing fields rather than an error string or empty object. Example: `{ "balance": 12000, "breakdown": "unavailable" }`. Claude can use what's present and acknowledge what's missing.

### Currency & Units
- **D-17:** `get_budget()` returns **raw integers only** — no currency label or unit field. The tool description and system prompt establish CS2 city funds context. Claude knows what the numbers mean.

### ECS Research Scope
- **D-18:** Researcher targets **vanilla CS2 ECS only** — unmodded game DLLs under `{CS2_INSTALL_PATH}/Cities2_Data/Managed/`. No mod-patched or Harmony-extended APIs. The mod must work for any CS2 player.

### Claude's Discretion
- Exact ECS component/system names for budget, traffic, and services — researcher discovers
- Whether budget income/expense categories map 1:1 to ECS or require aggregation — researcher confirms
- Human-readable labels for each tool toggle in settings (`LocaleEN` entries) — engineering decision
- Exact wording of the updated default system prompt tool-use guidance — engineering decision
- Whether the toggle group order in settings should match tool registration order or be alphabetical — engineering decision

</decisions>

<canonical_refs>
- `src/Systems/CityDataSystem.cs` — ECS query patterns and cached property structure; new queries extend this file
- `src/Systems/Tools/GetPopulationTool.cs` — canonical tool implementation pattern (constructor, Name, Description, InputSchema, Execute)
- `src/Systems/Tools/CityToolRegistry.cs` — tool registration and JSON serialization; toggle filtering goes here
- `src/Settings.cs` — settings class structure; new "Data Tools" section added here
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — Settings reorganization decisions (D-01 through D-05)
</canonical_refs>

<deferred>
## Deferred Ideas (out of Phase 3 scope)
- Budget trend over time (historical delta) — needs time-series storage; Phase 3 is snapshot only
- Per-district service coverage breakdown — district querying is more complex; deferred to later
- Traffic hotspot map overlay in the UI — visual, not a data tool; belongs in a UI phase
</deferred>
