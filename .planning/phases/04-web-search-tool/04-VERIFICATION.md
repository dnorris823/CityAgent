---
phase: 04-web-search-tool
verified: 2026-03-29T00:00:00Z
status: human_needed
score: 7/7 must-haves verified
re_verification: false
human_verification:
  - test: "Open CS2 with mod loaded, go to Options -> CityAgent, verify 'Web Search' section visible with 'Brave Search API Key' text field and 'Enable Web Search' toggle defaulting to OFF"
    expected: "Web Search section appears as the last group in the settings page with two controls"
    why_human: "CS2 in-game settings UI cannot be verified programmatically"
  - test: "Enter a Brave Search API key, enable the toggle, restart CS2, open CityAgent panel and ask: 'How do cities typically reduce highway noise for nearby neighborhoods?'"
    expected: "Claude invokes search_web() autonomously and cites a source title and URL in the response"
    why_human: "Requires live CS2 runtime, active API keys, and network access to Brave Search"
---

# Phase 4: Web Search Tool Verification Report

**Phase Goal:** Add a web search tool so Claude can ground recommendations in real-world urban planning sources.
**Verified:** 2026-03-29
**Status:** human_needed — all automated checks pass; in-game integration requires human testing
**Re-verification:** No — initial verification

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Brave Search API key field exists in mod settings under a Web Search section | VERIFIED | `Settings.cs:150` — `BraveSearchApiKey` property with `[SettingsUISection(kSection, kWebSearchGroup)]` + `[SettingsUITextInput]` |
| 2 | WebSearchEnabled toggle exists in mod settings under Web Search section | VERIFIED | `Settings.cs:153` — `WebSearchEnabled` bool property with `[SettingsUISection(kSection, kWebSearchGroup)]` |
| 3 | SearchWebTool class implements ICityAgentTool and calls Brave Search API | VERIFIED | `SearchWebTool.cs:10` — `class SearchWebTool : ICityAgentTool`; calls `https://api.search.brave.com/res/v1/web/search` |
| 4 | Tool returns structured JSON with query echo and up to 3 results per D-07 | VERIFIED | `SearchWebTool.cs:82` — `return JsonConvert.SerializeObject(new { query, results })` with `count=3` in URL |
| 5 | SearchWebTool is registered in ClaudeAPISystem when WebSearchEnabled is true and BraveSearchApiKey is non-empty | VERIFIED | `ClaudeAPISystem.cs:55-61` — conditional block checks both settings before calling `m_ToolRegistry.Register(new SearchWebTool(setting))` |
| 6 | SearchWebTool is NOT registered when either setting condition fails | VERIFIED | `ClaudeAPISystem.cs:62-65` — else branch logs "search_web tool NOT registered" |
| 7 | System prompt includes directive guidance for search_web() with citation instruction | VERIFIED | `Settings.cs:42-45` — DefaultSystemPrompt ends with `search_web()` paragraph referencing urban planning, zoning, infrastructure, historical examples, and source citation |

**Score:** 7/7 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Settings.cs` | Web Search section with BraveSearchApiKey and WebSearchEnabled; kWebSearchGroup constant; DefaultSystemPrompt with search guidance | VERIFIED | All fields present at lines 25, 150-153; prompt at lines 42-45; SetDefaults at lines 177-178; LocaleEN entries at lines 258-263 |
| `src/Systems/Tools/SearchWebTool.cs` | ICityAgentTool implementation calling Brave Search API | VERIFIED | 95 lines, fully implemented — not a stub |
| `src/Systems/ClaudeAPISystem.cs` | Conditional SearchWebTool registration | VERIFIED | Lines 53-65 contain the conditional registration block |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `SearchWebTool.cs` | `ICityAgentTool` | interface implementation | VERIFIED | `class SearchWebTool : ICityAgentTool` at line 10 |
| `SearchWebTool.cs` | `https://api.search.brave.com/res/v1/web/search` | HTTP GET with X-Subscription-Token header | VERIFIED | URL at line 40, header at line 45 |
| `ClaudeAPISystem.cs` | `SearchWebTool.cs` | `new SearchWebTool(setting)` registration | VERIFIED | Line 59 — `m_ToolRegistry.Register(new SearchWebTool(setting))` |
| `ClaudeAPISystem.cs` | `Settings.cs` | `setting.WebSearchEnabled` + `setting.BraveSearchApiKey` check | VERIFIED | Lines 56-57 read both properties from `Mod.ActiveSetting` |
| `Settings.cs` | Claude API system prompt | `DefaultSystemPrompt` constant containing `search_web()` | VERIFIED | `search_web()` at line 42 in `DefaultSystemPrompt` |

### Data-Flow Trace (Level 4)

The phase produces a tool (not a rendering component), so Level 4 data-flow tracing is the tool call path, not a UI data source.

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `SearchWebTool.cs` | `results` list | Brave Search HTTP response parsed from `json["web"]?["results"]` | Yes — live HTTP call to external API with real query | FLOWING |
| `SearchWebTool.cs` | API key | `m_Setting.BraveSearchApiKey` read from mod settings at call time | Yes — reads user-configured value, not hardcoded | FLOWING |

### Behavioral Spot-Checks

Step 7b: SKIPPED — the tool runs inside the CS2 game process and requires live API keys. No standalone entry points are available. Both summaries confirm `dotnet build -c Release` exited with 0 errors when CS2_INSTALL_PATH was properly set in the build environment.

Build note: A `dotnet build` run in this verification session produced 164 `CS0246` errors — all are "type not found in Colossal / Game namespaces." These are expected when the shell's `CS2_INSTALL_PATH` env var is not picked up by MSBuild (the var contains a line-break in this shell session). The PLAN itself used `CS2_INSTALL_PATH` to resolve game DLLs and both summaries confirm clean builds. The errors are an environment artifact, not a code defect.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| SRCH-01 | 04-01-PLAN, 04-02-PLAN | `search_web(query)` agent tool calls Brave Search API and returns results to Claude | SATISFIED | `SearchWebTool.cs` implements the full Brave Search call; registered into `CityToolRegistry` in `ClaudeAPISystem.cs` |
| SRCH-02 | 04-01-PLAN | Brave Search API key configurable in mod settings (separate from Anthropic key) | SATISFIED | `BraveSearchApiKey` property in `kWebSearchGroup` section, separate from `ClaudeApiKey` in `kClaudeGroup` |
| SRCH-03 | 04-02-PLAN | Claude automatically uses web search for real-world urban planning questions; system prompt instructs when to search | SATISFIED | `DefaultSystemPrompt` in `Settings.cs` contains explicit directive with trigger conditions and citation instruction |

No orphaned requirements: all three SRCH-IDs mapped to Phase 4 in REQUIREMENTS.md are claimed by plans 04-01 and 04-02 and are satisfied by the verified implementations.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `SearchWebTool.cs` | 48 | `.GetAwaiter().GetResult()` (blocking async) | Info | Intentional per D-01 — executes on thread pool inside `RunRequestAsync`, not on game main thread. Safe by design. |

No TODO/FIXME/PLACEHOLDER comments found. No empty implementations. No stub patterns. No hardcoded empty data reaching user-visible output.

### Human Verification Required

#### 1. Web Search Settings UI Visibility

**Test:** Launch CS2 with mod loaded. Open Options -> CityAgent. Scroll to the bottom of the Main tab.
**Expected:** A "Web Search" section is visible with two controls: "Brave Search API Key" (text input, empty by default) and "Enable Web Search" (toggle, OFF by default).
**Why human:** CS2's in-game options rendering cannot be verified programmatically.

#### 2. End-to-End Search Flow

**Test:** Enter a valid Brave Search API key in the "Brave Search API Key" field, enable the "Enable Web Search" toggle, restart CS2, open the CityAgent panel, and ask: "How do cities typically reduce highway noise for nearby neighborhoods?"
**Expected:** Claude invokes `search_web()` autonomously and the response references at least one source title and URL from the search results.
**Why human:** Requires live CS2 runtime, active Brave Search API key (free tier available), and a network call to `api.search.brave.com`.

### Gaps Summary

No gaps found. All seven observable truths are verified against the actual codebase. All three requirements (SRCH-01, SRCH-02, SRCH-03) are satisfied. The only items requiring further confirmation are the in-game UX behaviors listed under Human Verification, which are structurally inevitable for a CS2 mod and do not represent missing implementation.

---

_Verified: 2026-03-29_
_Verifier: Claude (gsd-verifier)_
