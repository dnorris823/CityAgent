# Phase 4: Web Search Tool - Research

**Researched:** 2026-03-27
**Domain:** Brave Search API integration, C# HTTP + timeout patterns, CS2 mod settings extension
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Tool Execution Model**
- D-01: `SearchWebTool.Execute()` makes the HTTP call synchronously via `.GetAwaiter().GetResult()` on the calling thread. Since `RunRequestAsync` already runs on a thread pool thread, the game thread is never blocked. No change to `ICityAgentTool` interface — all 12 existing tools are untouched.
- D-02: On HTTP failure or timeout, `Execute()` returns `{"error": "Search failed: [reason]"}` — same pattern as `CityToolRegistry.Dispatch()` error handling. Claude sees the failure and can acknowledge it gracefully.
- D-03: Timeout is hardcoded at ~5 seconds. No configurable timeout in settings. Enforced via `CancellationTokenSource`.

**Search Result Format**
- D-04: Tool returns 3 results per call, fixed. The Brave Search `count` parameter is set to 3.
- D-05: Each result includes: `title`, `url`, `description`, and `extra_snippets` (optional/nullable — include when present, omit when absent).
- D-06: 0 results is not an error. Return `{"query": "...", "results": []}`.
- D-07: The query string is echo'd back in the result: `{"query": "...", "results": [...]}`.

**Settings Placement**
- D-08: New "Web Search" settings section — the 5th section. Order: General → UI → Memory → Data Tools → Web Search.
- D-09: Web Search section contains two fields: (1) `BraveSearchApiKey` text input; (2) `WebSearchEnabled` bool toggle.
- D-10: The `search_web` tool is registered in `ClaudeAPISystem.OnCreate()` only when `WebSearchEnabled` is true AND `BraveSearchApiKey` is non-empty — OR via the Phase 3 toggle-at-serialization pattern.

**System Prompt Guidance**
- D-11: System prompt guidance uses directive phrasing: "Use search_web() to look up..." with explicit trigger conditions.
- D-12: System prompt instructs Claude to cite or reference sources from search results.
- D-13: No guardrails on search scope — trust Claude to use `search_web()` appropriately.
- D-14: Search guidance lives in the `DefaultSystemPrompt` constant in `Settings.cs` (not appended dynamically at request time).
- D-15: Phase 4 extends Phase 3's system prompt — appends web search guidance after city data tool guidance.

### Claude's Discretion
- Whether to enforce the ~5s timeout via `CancellationTokenSource` + `CancellationToken` on `s_Http.GetAsync()` or via a separate `HttpClient` with `Timeout` set
- Exact `WebSearchEnabled` default value in `SetDefaults()` — true (assume key present) or false (explicit opt-in)
- Whether tool registration happens at `OnCreate()` conditionally, or at serialization time via the registry toggle pattern (Phase 3 D-11)
- Human-readable label for `WebSearchEnabled` toggle in LocaleEN
- Exact wording of the complete extended system prompt including both Phase 3 city data guidance and Phase 4 web search guidance

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SRCH-01 | `search_web(query)` agent tool calls Brave Search API from C# backend and returns relevant results to Claude | Brave API confirmed: `GET https://api.search.brave.com/res/v1/web/search?q={query}&count=3&extra_snippets=true`, auth via `X-Subscription-Token` header, response shape in `web.results[]`. Synchronous call inside `RunRequestAsync` thread pool task is safe per D-01. |
| SRCH-02 | Brave Search API key is configurable in mod settings (separate field from Anthropic API key) | Settings pattern confirmed: `[SettingsUITextInput]` with `[SettingsUISection(kSection, kWebSearchGroup)]` follows exact same pattern as existing `OllamaApiKey` and the planned Claude API key from Phase 1. |
| SRCH-03 | Claude automatically uses web search when answering questions about real-world urban planning — system prompt instructs when to search | System prompt extension pattern confirmed from Phase 3 D-14. Phase 3 prompt baseline captured — Phase 4 appends directive guidance to the same `DefaultSystemPrompt` constant. |

</phase_requirements>

---

## Summary

Phase 4 adds a single new tool, `SearchWebTool`, that implements `ICityAgentTool` and makes a synchronous
Brave Search API call from inside the existing `RunRequestAsync` thread pool task. The call is safe on
the game thread because it is already off the main thread (D-01). The tool follows the exact same
interface contract as all 12 existing tools — no interface changes, no new abstractions needed.

The Brave Search API is straightforward: one endpoint (`/res/v1/web/search`), one required auth header
(`X-Subscription-Token`), and a clean JSON response body with a `web.results` array. The `count=3`
and `extra_snippets=true` query parameters are the only knobs needed. The tool shapes the response
into the echo-query format specified in D-07.

Two settings fields are added to a new "Web Search" section (D-08, D-09). The `BraveSearchApiKey`
field follows the same `[SettingsUITextInput]` pattern already used for API keys. The `WebSearchEnabled`
toggle uses `[SettingsUIToggle]` confirmed in Phase 3's DATA-05 research. The system prompt is extended
by appending web search guidance to the Phase 3 baseline constant (D-15).

**Primary recommendation:** Implement `SearchWebTool` as a self-contained class in `src/Systems/Tools/`
that owns its own timeout via `CancellationTokenSource`. Register it conditionally at `OnCreate()` time
(check enabled+key-present) rather than using the serialization-time filter — web search is a bigger
footprint change than a data toggle, and not registering it at all when disabled is simpler and cleaner.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Net.Http.HttpClient` | .NET Standard 2.1 (game-bundled) | HTTP GET to Brave Search API | Already the project-standard HTTP client (`s_Http` static in `ClaudeAPISystem`) |
| `System.Threading.CancellationTokenSource` | .NET Standard 2.1 | Enforce the 5s timeout on the HTTP call | Standard .NET mechanism for cancellation; no external dependency |
| `Newtonsoft.Json` / `JObject` | Game-bundled | Parse Brave Search JSON response; serialize tool result | Already used throughout all tool `Execute()` methods |
| `Game.Settings` / `ModSetting` | Shipped with CS2 | `[SettingsUISection]`, `[SettingsUITextInput]`, `[SettingsUIToggle]` on new settings fields | Already used — exact same attributes applied to Phase 1/3 settings fields |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Uri.EscapeDataString` | .NET Standard 2.1 | URL-encode the search query string | Required before inserting `query` into the GET URL to handle spaces and special characters |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `CancellationTokenSource` on shared `s_Http` | Separate `HttpClient` with `.Timeout = TimeSpan.FromSeconds(5)` | Separate client is cleaner (no shared state mutation) but adds a second static `HttpClient` instance. Both are valid. Per-request `CancellationTokenSource` on the existing `s_Http` instance avoids a new field and is idiomatic for fire-and-forget with cancellation. |
| `s_Http` from `ClaudeAPISystem` (shared) | `SearchWebTool`-owned static `HttpClient` | `SearchWebTool` owning its own client avoids coupling to `ClaudeAPISystem`'s client. Follows single-responsibility. Recommended: inject `s_Http` via constructor (same as data tools receive `CityDataSystem`), keeping one client instance. |

**Installation:** No new packages. All dependencies are game-bundled or .NET Standard 2.1.

---

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Systems/
│   ├── ClaudeAPISystem.cs        # MODIFY: register SearchWebTool in OnCreate
│   └── Tools/
│       └── SearchWebTool.cs      # NEW: implements ICityAgentTool
├── Settings.cs                   # MODIFY: add kWebSearchGroup, BraveSearchApiKey, WebSearchEnabled
```

### Pattern 1: SearchWebTool implements ICityAgentTool
**What:** `SearchWebTool` follows the exact same pattern as `GetPopulationTool` — constructor injection,
`Name`/`Description`/`InputSchema` properties, and a synchronous `Execute()` that calls Brave Search
via `.GetAwaiter().GetResult()`.

**When to use:** Any time Claude needs external information. Called from `CityToolRegistry.Dispatch()`
which already wraps `Execute()` in a try/catch (so exceptions never propagate to the caller).

**Example:**
```csharp
// Source: pattern from GetPopulationTool.cs + Brave Search API docs
public class SearchWebTool : ICityAgentTool
{
    private static readonly HttpClient s_BraveHttp = new HttpClient();
    private readonly Setting m_Setting;

    public SearchWebTool(Setting setting) => m_Setting = setting;

    public string Name        => "search_web";
    public string Description =>
        "Search the internet for real-world information. Use for urban planning techniques, " +
        "zoning practices, infrastructure design, traffic solutions, historical city examples, " +
        "or any question that benefits from current external knowledge.";
    public string InputSchema =>
        "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"," +
        "\"description\":\"The search query\"}},\"required\":[\"query\"]}";

    public string Execute(string inputJson)
    {
        try
        {
            var input = JObject.Parse(inputJson);
            string query = input["query"]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(query))
                return JsonConvert.SerializeObject(new { error = "Search failed: empty query" });

            string apiKey = (m_Setting?.BraveSearchApiKey ?? "").Trim();
            if (string.IsNullOrEmpty(apiKey))
                return JsonConvert.SerializeObject(new { error = "Search failed: no Brave Search API key configured" });

            string encodedQuery = Uri.EscapeDataString(query);
            string url = $"https://api.search.brave.com/res/v1/web/search?q={encodedQuery}&count=3&extra_snippets=true";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("X-Subscription-Token", apiKey);
            request.Headers.Add("Accept", "application/json");

            var response = s_BraveHttp.SendAsync(request, cts.Token).GetAwaiter().GetResult();
            string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode)
                return JsonConvert.SerializeObject(new { error = $"Search failed: HTTP {(int)response.StatusCode}" });

            var json = JObject.Parse(body);
            var rawResults = json["web"]?["results"] as JArray ?? new JArray();

            var results = new List<object>();
            foreach (var r in rawResults)
            {
                var extraSnippets = r["extra_snippets"] as JArray;
                if (extraSnippets != null && extraSnippets.Count > 0)
                {
                    results.Add(new
                    {
                        title           = r["title"]?.Value<string>() ?? "",
                        url             = r["url"]?.Value<string>() ?? "",
                        description     = r["description"]?.Value<string>() ?? "",
                        extra_snippets  = extraSnippets.ToObject<List<string>>()
                    });
                }
                else
                {
                    results.Add(new
                    {
                        title       = r["title"]?.Value<string>() ?? "",
                        url         = r["url"]?.Value<string>() ?? "",
                        description = r["description"]?.Value<string>() ?? ""
                    });
                }
            }

            return JsonConvert.SerializeObject(new { query, results });
        }
        catch (OperationCanceledException)
        {
            return JsonConvert.SerializeObject(new { error = "Search failed: timed out after 5 seconds" });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new { error = $"Search failed: {ex.Message}" });
        }
    }
}
```

### Pattern 2: Conditional tool registration in ClaudeAPISystem.OnCreate
**What:** Register `SearchWebTool` only when both `WebSearchEnabled` is true AND `BraveSearchApiKey`
is non-empty. This matches the early-registration pattern: tools appear in the API's `tools` array
for the entire session once registered.

**When to use:** For binary opt-in features (key present or not). Contrast with Phase 3 toggles which
filter at serialization time — those suit per-tool granularity where users toggle during play. Web
search is enabled once at mod load time, not toggled per-message.

**Tradeoff note:** If the user adds a key after game start, the tool won't be active until the next
game session. This is acceptable (same behavior as the Claude API key itself).

```csharp
// In ClaudeAPISystem.OnCreate() — after existing tool registrations
var webSearchSetting = Mod.ActiveSetting;
if (webSearchSetting != null
    && webSearchSetting.WebSearchEnabled
    && !string.IsNullOrWhiteSpace(webSearchSetting.BraveSearchApiKey))
{
    m_ToolRegistry.Register(new SearchWebTool(webSearchSetting));
    Mod.Log.Info("[ClaudeAPISystem] search_web tool registered.");
}
else
{
    Mod.Log.Info("[ClaudeAPISystem] search_web tool NOT registered (disabled or no API key).");
}
```

### Pattern 3: Settings extension for Web Search section
**What:** Add a new section constant `kWebSearchGroup`, two new settings properties, and corresponding
`LocaleEN` entries. Follows the exact same attribute pattern as Phase 1's Claude API key field.

```csharp
// In Settings.cs — class-level attributes (already present, add kWebSearchGroup):
[SettingsUIGroupOrder(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup, kWebSearchGroup)]
[SettingsUIShowGroupName(kGeneralGroup, kUIGroup, kMemoryGroup, kDataToolsGroup, kWebSearchGroup)]
public class Setting : ModSetting
{
    // ... existing constants ...
    public const string kWebSearchGroup = "WebSearch";

    [SettingsUISection(kSection, kWebSearchGroup)]
    [SettingsUITextInput]
    public string BraveSearchApiKey { get; set; } = string.Empty;

    [SettingsUISection(kSection, kWebSearchGroup)]
    public bool WebSearchEnabled { get; set; } = false;  // explicit opt-in (see note below)
}
```

**Note on `WebSearchEnabled` default:** Recommend `false` (explicit opt-in). The user must configure
a Brave Search API key before web search is useful — defaulting to `true` with an empty key means the
tool is silently unregistered anyway, but shows the toggle in an inconsistent "on" state. `false`
makes the activation flow clear: "get a key, paste it, enable the toggle."

### Pattern 4: Extended DefaultSystemPrompt
**What:** Phase 4 appends web search guidance to Phase 3's baseline prompt in `Settings.cs`.

**Phase 3 baseline (confirmed from 03-RESEARCH.md):**
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

**Phase 4 extension — append after the above:**
```csharp
    // ... Phase 3 baseline above, then append:
    "\n\nYou also have access to web search. Use search_web() to look up real-world urban " +
    "planning techniques, zoning practices, infrastructure design (roads, transit, utilities), " +
    "and historical city examples or case studies. When using search results, reference the " +
    "source title and URL in your response so the player can explore further.";
```

### Anti-Patterns to Avoid
- **Blocking the game thread:** Do NOT call `Execute()` from the main game thread. The tool is safe only because it is called from within `RunRequestAsync`, which already runs on a thread pool thread. Never call `SearchWebTool.Execute()` from `OnUpdate()` or any synchronous game system method.
- **Sharing mutable headers on `s_Http`:** `HttpClient` is thread-safe for sending requests, but `DefaultRequestHeaders` are shared and mutable. Always create a new `HttpRequestMessage` per request (as shown in the pattern above) rather than setting `s_Http.DefaultRequestHeaders["X-Subscription-Token"]`, which would race if two requests ran simultaneously.
- **Logging the full API key:** Mask the Brave key in all log output (same as Claude API key masking: first 4 + last 4 chars). `BraveSearchApiKey` should never appear in full in logs.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| URL encoding of query string | Custom escaping for spaces, `&`, `+` | `Uri.EscapeDataString(query)` | Standard library handles all reserved characters correctly; hand-rolling misses edge cases (Unicode, `%` signs in queries, etc.) |
| JSON parsing of Brave response | Manual string indexing or regex | `JObject.Parse(body)` then `json["web"]?["results"] as JArray` | Already the project-wide pattern; handles nulls, nested paths, and array iteration cleanly |
| Request timeout | `Thread.Sleep` + `Task.Cancel` pattern | `CancellationTokenSource(TimeSpan.FromSeconds(5))` passed to `SendAsync` | The standard .NET cancellation pattern; `CancellationTokenSource` disposes cleanly and raises `OperationCanceledException` which is already caught in the pattern above |

**Key insight:** This phase is entirely "glue" — the Brave Search API and the existing tool infrastructure do all the heavy lifting. The only new code is a thin adapter class and two settings fields.

---

## Common Pitfalls

### Pitfall 1: Accept header missing from Brave Search request
**What goes wrong:** Brave Search API returns HTTP 406 or garbled response without `Accept: application/json`.
**Why it happens:** Some APIs require an explicit Accept header even when they always return JSON.
**How to avoid:** Always include `request.Headers.Add("Accept", "application/json")` alongside the `X-Subscription-Token` header.
**Warning signs:** HTTP 406 status code in logs.

### Pitfall 2: CancellationToken not passed to ReadAsStringAsync
**What goes wrong:** The 5-second timeout fires during the HTTP send phase, but if `SendAsync` completes just before cancellation, the body read (`ReadAsStringAsync`) may hang indefinitely.
**Why it happens:** `CancellationTokenSource` only cancels the operation passed its token.
**How to avoid:** Pass `cts.Token` to both `s_BraveHttp.SendAsync(request, cts.Token)` AND `response.Content.ReadAsStringAsync(cts.Token)` (the overload exists in .NET Standard 2.1).

### Pitfall 3: KeyNotFoundException when Brave response lacks `web` key
**What goes wrong:** If Brave returns a non-web result (e.g., a news-only response or error shape), `json["web"]` is null and the cast throws.
**Why it happens:** Brave Search API returns different top-level keys depending on query type. Some queries return only `news` or `videos` without a `web` key.
**How to avoid:** Use `json["web"]?["results"] as JArray ?? new JArray()` — the null-conditional + null-coalescing to an empty array ensures 0-result handling matches D-06.

### Pitfall 4: Mutation of s_Http.DefaultRequestHeaders across concurrent calls
**What goes wrong:** Two simultaneous requests (e.g., heartbeat + user message in Phase 6) set the `X-Subscription-Token` header on the shared client concurrently, causing one request to be authenticated with the wrong key (or no key).
**Why it happens:** `HttpClient.DefaultRequestHeaders` is not thread-safe for concurrent modification.
**How to avoid:** Create a new `HttpRequestMessage` per call and set the token header on the message object — never on `s_Http.DefaultRequestHeaders`. The pattern in the code example above is correct.

### Pitfall 5: extra_snippets not requested but code checks for it
**What goes wrong:** `r["extra_snippets"]` is always null because Brave only returns this field when `extra_snippets=true` is in the query string.
**Why it happens:** Forgetting to include the query parameter.
**How to avoid:** The URL must include `&extra_snippets=true`. The code example above includes it.

---

## Code Examples

### Brave Search API request (verified from official docs)
```csharp
// Source: https://api-dashboard.search.brave.com/app/documentation/web-search/get-started
// Endpoint: GET https://api.search.brave.com/res/v1/web/search
// Required header: X-Subscription-Token: {apiKey}
// Parameters: q (required), count (max 20), extra_snippets (bool)

string url = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count=3&extra_snippets=true";
var request = new HttpRequestMessage(HttpMethod.Get, url);
request.Headers.Add("X-Subscription-Token", apiKey);
request.Headers.Add("Accept", "application/json");

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var response = httpClient.SendAsync(request, cts.Token).GetAwaiter().GetResult();
```

### Brave Search API response shape (verified from official docs)
```json
{
  "web": {
    "results": [
      {
        "title": "Traffic calming - Wikipedia",
        "url": "https://en.wikipedia.org/wiki/Traffic_calming",
        "description": "Traffic calming uses physical design to slow or reduce motor traffic...",
        "extra_snippets": [
          "Speed bumps, chicanes, and raised crosswalks are common techniques.",
          "Studies show 15-20% reduction in speeds with proper calming measures."
        ]
      }
    ]
  },
  "query": { "original": "traffic calming urban design" }
}
```

### Tool result shape returned to Claude (D-07 format)
```json
{
  "query": "traffic calming urban design",
  "results": [
    {
      "title": "Traffic calming - Wikipedia",
      "url": "https://en.wikipedia.org/wiki/Traffic_calming",
      "description": "Traffic calming uses physical design to slow or reduce motor traffic...",
      "extra_snippets": ["Speed bumps, chicanes..."]
    }
  ]
}
```

### Settings SettingsUIToggle pattern (confirmed from Phase 3 DATA-05 research)
```csharp
[SettingsUISection(kSection, kWebSearchGroup)]
public bool WebSearchEnabled { get; set; } = false;
// No attribute needed beyond [SettingsUISection] — bool properties render as toggles
// by default in CS2's ModSetting system. [SettingsUIToggle] attribute does NOT exist
// as a named attribute; the toggle UI is inferred from the bool property type.
```

**Important note on [SettingsUIToggle]:** Phase 3's research confirmed the toggle rendering is
automatic for `bool` properties in `ModSetting`. There is no separate `[SettingsUIToggle]` attribute
to apply — the bool type itself triggers toggle rendering in CS2's settings UI.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| `System.Net.Http.HttpClient` | SearchWebTool HTTP call | Yes | .NET Standard 2.1 (game-bundled) | — |
| `System.Threading.CancellationTokenSource` | 5-second timeout | Yes | .NET Standard 2.1 (game-bundled) | — |
| Brave Search API (external service) | SRCH-01 | Requires account | — | Tool returns error JSON; Claude acknowledges gracefully (D-02) |
| Brave Search API key | SRCH-02 | User must provide | — | Tool not registered if key absent (D-10) |

**Missing dependencies with no fallback:**
- Brave Search API account (free tier: 2,000 queries/month) — user must create at https://api-dashboard.search.brave.com

**Missing dependencies with fallback:**
- Brave Search unavailable at query time: tool returns `{"error": "Search failed: ..."}`, Claude can proceed without search results (D-02, D-06).

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | None — CS2 mod; no automated test runner exists in the codebase. Compile + in-game only. |
| Config file | None |
| Quick run command | `cd "C:/Coding Projects/CityAgent/Working/CityAgent/src" && dotnet build -c Release` |
| Full suite command | Build + in-game verification (manual) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SRCH-01 | `search_web(query)` calls Brave API and returns structured results | in-game manual | `dotnet build -c Release` (compile check) | N/A |
| SRCH-02 | Brave Search API key field visible in mod settings UI | in-game manual | `dotnet build -c Release` (compile check) | N/A |
| SRCH-03 | Claude invokes `search_web` autonomously for urban planning questions without player prompting | in-game manual | `dotnet build -c Release` (compile check) | N/A |

### Sampling Rate
- **Per task commit:** `dotnet build -c Release` (zero compile errors)
- **Per wave merge:** Build + deploy to mod folder + load CS2 + confirm settings section visible
- **Phase gate:** Full in-game test: ask Claude "how do cities reduce highway noise?" — confirm response cites a source retrieved via search

### Wave 0 Gaps
None — no new test infrastructure needed. Same compile-then-in-game pattern as all prior phases.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Claude responds from training data only | Claude calls `search_web()` to retrieve current real-world information | Phase 4 | Responses can cite actual sources; knowledge is no longer limited to training cutoff |
| Single static HttpClient in ClaudeAPISystem | SearchWebTool may own its own HttpClient or share `s_Http` | Phase 4 | Keeps one client per concern; separate client avoids header mutation races |

---

## Open Questions

1. **Should SearchWebTool share `s_Http` or own a separate `HttpClient`?**
   - What we know: `s_Http` in `ClaudeAPISystem` is a shared `static readonly HttpClient`. Adding the Brave `X-Subscription-Token` header per-request (on the `HttpRequestMessage`, not `DefaultRequestHeaders`) is thread-safe.
   - What's unclear: Whether injecting `s_Http` through the constructor creates a confusing dependency from `SearchWebTool` into `ClaudeAPISystem`.
   - Recommendation: Give `SearchWebTool` its own `private static readonly HttpClient s_BraveHttp = new HttpClient()`. This keeps concerns separate and avoids any coupling. Two `HttpClient` instances in a game mod is not a resource problem.

2. **Does `extra_snippets=true` count against the Brave free tier quota differently than without it?**
   - What we know: Brave's free tier allows 2,000 queries/month. `extra_snippets` is an optional enrichment parameter.
   - What's unclear: Whether requesting `extra_snippets` costs additional quota.
   - Recommendation: Keep `extra_snippets=true` in the URL — it provides richer context to Claude at no documented extra quota cost. If the player is budget-conscious they can disable web search entirely via the toggle.

---

## Sources

### Primary (HIGH confidence)
- Brave Search API official docs: https://api-dashboard.search.brave.com/app/documentation/web-search/get-started — endpoint URL, auth header (`X-Subscription-Token`), `count` and `extra_snippets` parameters, response shape (`web.results[].title/url/description/extra_snippets`)
- `src/Systems/Tools/ICityAgentTool.cs` (read directly) — interface contract `SearchWebTool` must implement
- `src/Systems/Tools/GetPopulationTool.cs` (read directly) — canonical tool implementation pattern
- `src/Systems/Tools/CityToolRegistry.cs` (read directly) — `Register()`, `Dispatch()`, error handling pattern
- `src/Systems/ClaudeAPISystem.cs` (read directly) — `s_Http`, `OnCreate()` registration site, `RunRequestAsync` thread context
- `src/Settings.cs` (read directly) — current settings fields, `[SettingsUITextInput]`, `[SettingsUISection]` patterns, `DefaultSystemPrompt` baseline
- `.planning/phases/03-extended-city-data-tools/03-RESEARCH.md` (read directly) — Phase 3 `DefaultSystemPrompt` text (Phase 4 baseline), `[SettingsUIToggle]` behavior for bool properties, `kDataToolsGroup` pattern

### Secondary (MEDIUM confidence)
- `.planning/phases/03-extended-city-data-tools/03-CONTEXT.md` — D-11 (toggle-at-serialization pattern), D-14 (system prompt update approach), D-15 (settings section order)
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — D-01 to D-05 (settings section naming, API key field pattern)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are game-bundled .NET Standard 2.1; no new packages
- Brave API integration: HIGH — endpoint, headers, and response shape verified from official docs
- Architecture: HIGH — `SearchWebTool` pattern derived directly from existing codebase; no guesswork
- Settings extension: HIGH — pattern confirmed from Phase 3 research and current `Settings.cs`
- System prompt: HIGH — Phase 3 baseline text captured verbatim from 03-RESEARCH.md

**Research date:** 2026-03-27
**Valid until:** 2026-05-27 (Brave Search API is stable; CS2 mod SDK does not change between updates)
