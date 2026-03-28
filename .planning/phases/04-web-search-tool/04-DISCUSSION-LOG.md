# Phase 4: Web Search Tool - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-27
**Phase:** 04-web-search-tool
**Areas discussed:** Tool execution model, Search result format, Settings placement, System prompt guidance

---

## Tool Execution Model

| Option | Description | Selected |
|--------|-------------|----------|
| Sync block on thread pool | Execute() calls Brave Search via .GetAwaiter().GetResult() — safe since already off game thread; no interface change | ✓ |
| Async interface for web tool only | Add IAsyncCityAgentTool with Task<string> ExecuteAsync(); registry dispatches differently | |
| Change ICityAgentTool to async | Upgrade shared interface; all 12 existing tools get Task.FromResult() wrapper | |

**User's choice:** Sync block on thread pool — no interface change, all 12 existing tools untouched.

**Follow-up: Failure handling**

| Option | Description | Selected |
|--------|-------------|----------|
| Return JSON error to Claude | `{"error": "Search failed: [reason]"}` — same error pattern as CityToolRegistry | ✓ |
| Return empty results to Claude | `{"results": [], "note": "Search unavailable"}` — valid shape but misleading | |
| Re-throw exception | Let CityToolRegistry.Dispatch() catch it | |

**Follow-up: Timeout**

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed short timeout | Hardcode ~5 seconds — fail fast, no settings surface | ✓ |
| Configurable in settings | Timeout slider in mod settings | |
| No timeout / default HttpClient | Default ~100s timeout — hangs stall entire tool loop | |

---

## Search Result Format

| Option | Description | Selected |
|--------|-------------|----------|
| 3 results | Enough to synthesize, minimal token overhead | ✓ |
| 5 results | Broader coverage, ~67% more tokens | |
| 1 result | Minimal tokens, risk of one bad result | |

**Fields per result:**

| Option | Description | Selected |
|--------|-------------|----------|
| Title + URL + description | Standard three fields | |
| Title + URL + description + extra_snippets | Include extended snippets when available | ✓ |
| Title + description only | No URL, avoids citation issues | |

**Zero results handling:**

| Option | Description | Selected |
|--------|-------------|----------|
| Return `{"results": [], "query": "..."}` | Empty array, not an error — Claude responds gracefully | ✓ |
| Return a JSON error | Treat 0 results as failure | |

**Query echo:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, include query in result | `{"query": "...", "results": [...]}` — Claude can reference what it searched | ✓ |
| No, results only | Simpler shape | |

**Count request strategy:**

| Option | Description | Selected |
|--------|-------------|----------|
| Request exactly what we return | Request 3 from Brave, return 3 | ✓ |
| Request more, return top N | Over-fetch and filter | |
| Configurable in settings | Player sets result count | |

**Count parameter in InputSchema:**

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed count, no parameter | Tool always returns 3; Claude doesn't micromanage count | ✓ |
| Optional count parameter | Claude can request more/fewer per query | |

---

## Settings Placement

| Option | Description | Selected |
|--------|-------------|----------|
| New 'Web Search' section (5th) | Dedicated section: Brave key + enabled toggle | ✓ |
| Under 'General' | Brave key alongside Claude API key | |
| Under 'Data Tools' | Mixes API key with bool toggles | |

**Explicit toggle:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, explicit toggle | Player can disable without clearing key — follows Phase 3 pattern | ✓ |
| No toggle — blank key = disabled | One fewer setting, less explicit | |

---

## System Prompt Guidance

**Directiveness:**

| Option | Description | Selected |
|--------|-------------|----------|
| Directive: specific trigger conditions | Lists explicit scenarios: urban planning, zoning, infrastructure, history | ✓ |
| Light hint: tool description only | Trust tool description to guide Claude | |
| Aggressive: always search | Maximizes grounding, increases cost | |

**Source citation:**

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, encourage citing sources | "Reference the source in your response" | ✓ |
| No explicit citation guidance | Claude decides whether to cite | |
| Discourage citing — paraphrase only | Synthesis without attribution | |

**Guardrails:**

| Option | Description | Selected |
|--------|-------------|----------|
| No guardrails — trust Claude | Directive trigger conditions provide sufficient focus | ✓ |
| Yes, scope to city planning topics | Explicit restriction on off-topic searches | |

**Prompt placement:**

| Option | Description | Selected |
|--------|-------------|----------|
| In DefaultSystemPrompt | Consistent with Phase 3 D-14 pattern | ✓ |
| Appended separately at request time | Dynamic injection only when web search enabled | |

**Phase relationship:**

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 4 extends Phase 3's prompt | Appends web search guidance after city data guidance | ✓ |
| Phase 4 writes complete replacement | Full DefaultSystemPrompt rewrite | |

**Phrasing tone:**

| Option | Description | Selected |
|--------|-------------|----------|
| Directive: 'Use search_web() to look up' | Imperative — strongly cues Claude to invoke | ✓ |
| Permissive: 'You may use search_web()' | Softer, less consistent usage | |
| Claude's discretion | Planner writes whatever phrasing works | |

---

## Claude's Discretion

- Exact timeout implementation approach (CancellationTokenSource vs. HttpClient.Timeout)
- `WebSearchEnabled` default value (true/false)
- Whether tool registration is conditional at OnCreate() or via registry toggle pattern
- Human-readable label for `WebSearchEnabled` in LocaleEN
- Complete extended system prompt wording for both Phase 3 + Phase 4 guidance

## Deferred Ideas

None
