# Phase 4: Web Search Tool - Context

**Gathered:** 2026-03-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a `search_web(query)` agent tool backed by Brave Search API. C# makes the HTTP call
(synchronous, off the game thread), shapes the result, and returns it to Claude via the existing
tool_use loop. Claude invokes it autonomously based on system prompt guidance. The Brave Search API
key and an enabled/disabled toggle live in a new "Web Search" settings section.

</domain>

<decisions>
## Implementation Decisions

### Tool Execution Model
- **D-01:** `SearchWebTool.Execute()` makes the HTTP call **synchronously** via
  `.GetAwaiter().GetResult()` on the calling thread. Since `RunRequestAsync` already runs on a
  thread pool thread, the game thread is never blocked. No change to `ICityAgentTool` interface —
  all 12 existing tools are untouched.
- **D-02:** On HTTP failure or timeout, `Execute()` returns a JSON error object:
  `{"error": "Search failed: [reason]"}` — same pattern as `CityToolRegistry.Dispatch()` error
  handling. Claude sees the failure and can acknowledge it gracefully in its response.
- **D-03:** Timeout is **hardcoded at ~5 seconds**. No configurable timeout in settings. If Brave
  Search doesn't respond in 5s, it fails and returns D-02's error. The existing `s_Http`
  `HttpClient` instance may need a per-request `CancellationTokenSource` to enforce the timeout.

### Search Result Format
- **D-04:** Tool returns **3 results** per call, fixed. The Brave Search `count` parameter is set
  to 3. Claude cannot request more or fewer via tool input — `InputSchema` has no count parameter.
- **D-05:** Each result includes: `title`, `url`, `description`, and `extra_snippets` (if Brave
  returns them). `extra_snippets` is optional/nullable — include when present, omit when absent.
- **D-06:** 0 results is **not an error**. Return `{"query": "...", "results": []}`. Claude sees
  an empty array and can acknowledge "I couldn't find information on that."
- **D-07:** The **query string is echo'd back** in the result: `{"query": "...", "results": [...]}`.
  Claude can see what it searched, which helps it form better follow-up queries if needed.

### Settings Placement
- **D-08:** New **"Web Search"** settings section — the **5th section**. Updated settings order:
  General → UI → Memory → Data Tools → Web Search. Consistent with Phase 3 D-15.
- **D-09:** Web Search section contains two fields:
  1. `BraveSearchApiKey` — text input, separate from the Anthropic/Ollama API keys
  2. `WebSearchEnabled` — bool toggle (default: **true** when key is present, but the planner
     should decide the right default — Claude's discretion on exact default value)
- **D-10:** The `search_web` tool is registered in `ClaudeAPISystem.OnCreate()` only when
  `WebSearchEnabled` is true AND `BraveSearchApiKey` is non-empty. If either condition fails,
  the tool is not registered. Alternatively, the existing toggle-at-serialization pattern from
  Phase 3 D-11 applies — Claude's discretion on which approach fits the registry pattern better.

### System Prompt Guidance
- **D-11:** System prompt guidance uses **directive phrasing**: `"Use search_web() to look up..."`.
  Specific trigger conditions are listed explicitly — at minimum:
  - Real-world urban planning techniques
  - Zoning practices and land use
  - Infrastructure design (roads, transit, utilities)
  - Historical city examples or case studies
- **D-12:** System prompt instructs Claude to **cite or reference sources** from search results:
  e.g., "When using search results, reference the source in your response."
- **D-13:** **No guardrails** on search scope — trust Claude to use search_web() appropriately.
  The directive trigger conditions in D-11 provide sufficient focus without explicit restrictions.
- **D-14:** Search guidance lives in the **`DefaultSystemPrompt` constant** in `Settings.cs`
  (not appended dynamically at request time). Consistent with Phase 3 D-14 pattern.
- **D-15:** Phase 4 **extends Phase 3's system prompt** — appends web search guidance after the
  city data tool guidance written in Phase 3. The Phase 3 prompt is the baseline; Phase 4 adds
  to it. Planner must read Phase 3's prompt text before writing Phase 4 additions.

### Claude's Discretion
- Whether to enforce the ~5s timeout via `CancellationTokenSource` + `CancellationToken` on
  `s_Http.GetAsync()` or via a separate `HttpClient` with `Timeout` set — engineering decision
- Exact `WebSearchEnabled` default value in `SetDefaults()` — true (assume key present) or false
  (explicit opt-in) — planner decides based on UX intent
- Whether tool registration happens at `OnCreate()` conditionally, or at serialization time via
  the registry toggle pattern (Phase 3 D-11) — planner picks the cleaner approach given both
  patterns exist
- Human-readable label for `WebSearchEnabled` toggle in LocaleEN (`"Web Search"` vs.
  `"Enable Web Search"`) — engineering decision
- Exact wording of the complete extended system prompt including both Phase 3 city data guidance
  and Phase 4 web search guidance — planner writes this

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — SRCH-01 (search_web tool), SRCH-02 (Brave key in settings),
  SRCH-03 (Claude invokes autonomously)
- `.planning/ROADMAP.md` — Phase 4 goal and 3 success criteria

### Prior Phase Context
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — Settings structure pattern
  (D-01 through D-05): section naming, API key field patterns, how settings sections are declared
- `.planning/phases/03-extended-city-data-tools/03-CONTEXT.md` — D-08 to D-12 (tool toggles,
  toggle coverage, registry filtering), D-14 (system prompt update approach), D-15 (settings
  section order: General → UI → Memory → Data Tools)

### Codebase
- `src/Systems/Tools/ICityAgentTool.cs` — Interface that `SearchWebTool` must implement (Name,
  Description, InputSchema, Execute)
- `src/Systems/Tools/GetPopulationTool.cs` — Canonical tool implementation pattern
- `src/Systems/Tools/CityToolRegistry.cs` — Tool registration (`Register()`), serialization
  (`GetToolsJson()`, `GetToolsJsonOpenAI()`), dispatch (`Dispatch()`)
- `src/Systems/ClaudeAPISystem.cs` — `OnCreate()` tool registration site; `s_Http` static
  `HttpClient` that `SearchWebTool` may use or reference
- `src/Settings.cs` — Settings class structure, `[SettingsUISection]` attribute pattern,
  `kSection`/group constants, `SetDefaults()` pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ICityAgentTool` interface (Name, Description, InputSchema, Execute) — `SearchWebTool`
  implements this exactly; no interface modification needed (D-01)
- `CityToolRegistry.Register()` — tool injection point in `ClaudeAPISystem.OnCreate()`
- `s_Http` (`static readonly HttpClient`) in `ClaudeAPISystem` — may be usable for Brave Search
  HTTP call, or `SearchWebTool` may own its own `HttpClient` instance; planner decides
- `[SettingsUISection(kSection, kGroup)]` + `[SettingsUITextInput]` pattern (Settings.cs) —
  exact pattern for `BraveSearchApiKey` field
- `[SettingsUIToggle]` pattern (if it exists in the CS2 SDK for bool properties) —
  planner confirms the correct attribute for `WebSearchEnabled`

### Established Patterns
- Tool Execute() returns a JSON string — `JsonConvert.SerializeObject(new { ... })`
- Tool errors: `JsonConvert.SerializeObject(new { error = "..." })` — no exceptions thrown to caller
- Settings: `m_` prefix fields don't apply (settings are auto-properties); group constants are
  `const string k*Group`
- Async HTTP pattern: fire Task from game thread → write to `volatile PendingResult` → drain in
  OnUpdate. Web search happens *inside* that task, so it's synchronous relative to the task thread.

### Integration Points
- `ClaudeAPISystem.OnCreate()`: add `m_ToolRegistry.Register(new SearchWebTool(...))` — needs
  access to settings to check if enabled, and to the `HttpClient`
- `Settings.cs`: add `BraveSearchApiKey` + `WebSearchEnabled` under a new `kWebSearchGroup`
  constant; add to `[SettingsUIGroupOrder]` and `[SettingsUIShowGroupName]` class attributes
- `DefaultSystemPrompt` constant in `Settings.cs`: Phase 4 appends web search guidance here

</code_context>

<specifics>
## Specific Ideas

- Tool input schema: `{"type": "object", "properties": {"query": {"type": "string", "description": "The search query"}}, "required": ["query"]}` — single required string parameter
- Result shape: `{"query": "...", "results": [{"title": "...", "url": "...", "description": "...", "extra_snippets": ["..."]}]}`
- Brave Search API endpoint: `https://api.search.brave.com/res/v1/web/search?q={query}&count=3`
- Auth header: `X-Subscription-Token: {BraveSearchApiKey}` (single header, no Bearer prefix)
- System prompt addition format: "Use search_web() to look up real-world urban planning
  techniques, zoning practices, infrastructure design, and historical city examples. When using
  search results, reference the source in your response."

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 04-web-search-tool*
*Context gathered: 2026-03-27*
