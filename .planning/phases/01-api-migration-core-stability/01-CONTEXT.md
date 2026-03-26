# Phase 1: API Migration & Core Stability - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Rewrite ClaudeAPISystem from Ollama `/api/chat` format to Anthropic `/v1/messages` format; fix thread
safety issues (PendingResult, m_RequestInFlight); add Ollama as a user-configurable optional fallback
with 429 auto-retry; make NarrativeMemorySystem fully async; validate the full end-to-end pipeline
in-game.

</domain>

<decisions>
## Implementation Decisions

### Provider Settings Structure
- **D-01:** Settings reorganized into two separate sections: **"Claude API"** (API key, model) and
  **"Ollama Fallback (optional)"** (base URL, optional API key, model). Clean break — old
  `OllamaApiKey` / `OllamaModel` / `OllamaBaseUrl` fields deleted entirely (not marked `[Obsolete]`).
- **D-02:** Default Claude model: `claude-sonnet-4-6`
- **D-03:** Default Ollama base URL: `http://localhost:11434`
- **D-04:** A read-only **"active provider" status label** is shown in settings (e.g., "Currently
  using: Claude API"). Not duplicated in the chat panel.
- **D-05:** Ollama Fallback section header is explicitly labeled "(optional)" so it is clear it is
  not required to use the mod.

### Rate-Limit Fallback
- **D-06:** When Claude returns HTTP 429, show an **in-panel system notice**:
  `⚠️ Rate limited — retrying with [ollama-model-name]...` then send the request to Ollama. The
  Ollama response renders with normal styling — no footer, no provider label on the response itself.
- **D-07:** **Only HTTP 429 triggers fallback.** Other Claude errors (400, 401, 500) show
  `[Error]: ...` in chat without falling back to Ollama — those indicate config problems, not
  transient rate limits.
- **D-08:** If Claude rate-limits and **no Ollama fallback is configured**, show a clear error in
  chat: `⚠️ Rate limited by Claude. No Ollama fallback configured — set one up in mod settings.`
- **D-09:** No Ollama connectivity validation in Phase 1. The player discovers Ollama isn't working
  when a 429 actually fires, not at settings-save time.

### Tool Format
- **D-10:** Tool call format is **auto-selected per active provider**: `GetToolsJson()` (Anthropic
  format) for Claude; `GetToolsJsonOpenAI()` (OpenAI-compatible) for Ollama. Both methods already
  exist in `CityToolRegistry.cs` — they just need to be wired to the right branch.

### Threading & Race Conditions
- **D-11:** **Full async refactor of NarrativeMemorySystem** — `WriteFile`, `AppendToLog`,
  `SaveChatSession`, and related methods become async throughout (not just wrapped at call sites).
- **D-12:** Memory file writes are **fire-and-forget** on the calling side. Write failures are
  logged via `Mod.Log.Error` and swallowed — non-fatal, consistent with existing error strategy.
- **D-13:** **Screenshot base64 encoding** (`File.ReadAllBytes` + `Convert.ToBase64String`) moves
  to a background thread in Phase 1 (currently runs in `CityAgentUISystem.OnUpdate` on the main
  thread).
- **D-14:** `PendingResult` uses **`Interlocked.Exchange`** to replace the volatile-only pattern.
  `m_RequestInFlight` gets the **`volatile` keyword** to prevent double-send races. Both fixed.

### Claude's Discretion
- Exact async method signatures and Task patterns in NarrativeMemorySystem — engineering decision
- How the "active provider" label reads current state (polling vs. event-driven) — engineering decision
- Whether to introduce a `Provider` enum or string-based routing — engineering decision
- System notice message styling (role type for in-panel notices) — engineering decision

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — CORE-01, CORE-02, CORE-03 (threading/stability); API-01–API-04
  (Claude + Ollama provider integration)
- `.planning/ROADMAP.md` — Phase 1 goal and 5 success criteria

### Codebase Analysis
- `.planning/codebase/ARCHITECTURE.md` — Thread model, data flow, tool registry, API system design,
  existing binding contract
- `.planning/codebase/CONCERNS.md` — Specific file/line references for race conditions (volatile
  field, m_RequestInFlight), synchronous I/O on main thread, Ollama/Claude naming mismatch,
  dead `GetToolsJson()` code

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/Systems/CityToolRegistry.cs` — Both `GetToolsJson()` (Anthropic format, currently dead code)
  and `GetToolsJsonOpenAI()` (currently used) exist. D-10 wires them to the correct provider branch.
- `src/Systems/ClaudeAPISystem.cs` — `RunRequestAsync` needs a full rewrite: Ollama `/api/chat` →
  Claude `/v1/messages`, plus a fallback branch for Ollama on 429. `s_Http` (static HttpClient)
  reused as-is.
- `src/Settings.cs` — `ModSetting` subclass pattern stays. Fields renamed per D-01. Two new
  setting group sections needed.

### Established Patterns
- Private instance fields: `m_` prefix; static: `s_` prefix
- Logging: `Mod.Log.Error(...)`, `Mod.Log.Info(...)`, `Mod.Log.Warn(...)`
- Async work: fire `Task` from game thread → write result to `volatile` field → drain in `OnUpdate`
- Error presentation: `[Error]: ...` string written to `PendingResult` → surfaces as chat message
- In-panel system notices: append to `m_History` with a distinguishable role (e.g., `"system"`)

### Integration Points
- `CityAgentUISystem.cs` → `ClaudeAPISystem.BeginRequest()`: provider routing starts here
- `NarrativeMemorySystem` call sites in `CityAgentUISystem.OnUpdate`: these are the main-thread
  synchronous I/O paths that must become fire-and-forget async calls per D-11/D-12

</code_context>

<specifics>
## Specific Ideas

- In-panel rate-limit notice format: `⚠️ Rate limited — retrying with [model-name]...`
- Settings sections: "Claude API" first, "Ollama Fallback (optional)" second
- Active provider indicator: read-only status line inside settings panel, not in chat

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-api-migration-core-stability*
*Context gathered: 2026-03-26*
