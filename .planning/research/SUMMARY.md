# Research Summary — CityAgent Next Milestone
**Synthesized:** 2026-03-26
**Research files:** STACK.md, FEATURES.md, ARCHITECTURE.md, PITFALLS.md
**Scope:** Claude API migration, web search tool, proactive heartbeat system, memory file explorer

---

## Executive Summary

CityAgent occupies a unique niche — there is no direct comparator mod in the CS2 ecosystem. All four features in scope (Claude API migration, web search, heartbeat, memory explorer) are buildable with zero new external dependencies: `System.Net.Http.HttpClient` and `Newtonsoft.Json` cover all HTTP and JSON needs, and the React UI requires no additional npm packages. The constraint-heavy environment (Mono/.NET Standard 2.1, Coherent GT embedded browser, CS2 DLL locking) means every pattern must work inside limits set by the game runtime — not around them.

The single biggest blocker is the Claude API format migration. Every feature depends on a working API call: tool calls fail with wrong format, screenshots are never sent, and the entire agentic loop is broken until this is done. The Ollama `/api/chat` wire format and the Claude `/v1/messages` format differ in five distinct ways (system prompt placement, image content blocks, tool schema key names, tool result role and structure, auth header), and the existing `ClaudeAPISystem.cs` is entirely in Ollama format. This is the highest-risk change and must come first.

Once the API migration is validated end-to-end, the three remaining features are relatively isolated. Web search is a single new tool class and a settings field. Memory explorer is new bindings in the existing UISystem and a React tab. Heartbeat is a frame counter and a conditional call — low risk once the API works correctly. The main design risks after migration are heartbeat cost runaway, context window bloat, and threading races that become more likely when background requests are added.

---

## Key Stack Decisions

- **No new dependencies.** Reuse the existing static `HttpClient` (`s_Http` in `ClaudeAPISystem`) for both Anthropic and Brave Search calls. Newtonsoft.Json handles all serialization. No NuGet, no bundled npm packages.
- **Brave Search API over Bing.** Free tier of 2,000 queries/month, single `X-Subscription-Token` header, GET request returning clean JSON — fits the existing async HTTP pattern exactly. Bing requires Azure subscription.
- **No streaming API.** Coherent GT's C#-to-JS binding layer is not designed for incremental updates. Keep `stream: false` (or omit the field). Use a well-designed loading indicator for the wait.

---

## Table Stakes Features

| Feature | Status | Notes |
|---------|--------|-------|
| Claude API format (correct endpoint, headers, format) | NOT WORKING | Primary blocker — currently sending Ollama format |
| Chat panel opens/closes reliably | Working | Needs end-to-end re-validation after API migration |
| Loading/thinking indicator | Incomplete | Exists but needs review; critical for non-streaming calls |
| Visually distinct message bubbles | Incomplete | No distinct bubble styles currently |
| Working markdown rendering | Partial | Hand-rolled renderer has known edge cases |
| Configurable API key | Partial | Field named `OllamaApiKey` — needs rename and correct default URL |
| Screenshot capture in messages | Implemented but broken | Image format is Ollama-style `images[]`; must change to Claude content block |
| ECS city data in tool calls | Partial | Population/employment tools exist; budget and traffic tools are missing |
| Narrative memory across sessions | Implemented | Needs end-to-end validation after API migration |

---

## Differentiating Features (Ship After Table Stakes)

| Feature | Value | Complexity | Dependency |
|---------|-------|------------|------------|
| Web search (Brave API) | Real-world urban planning grounding | Low-Medium | API migration done |
| Memory file explorer | Player trust and control over what Claude remembers | Medium-High | API migration + memory system stable |
| Proactive heartbeat | Advisor notices problems without user prompting | High | API migration + ECS tools + chat UI stable |

---

## Critical Pitfalls / Watch-Outs

**1. Wrong API wire format — everything fails with HTTP 400**
The Ollama and Claude formats differ in five specific ways. A "just change the URL" migration will not work. Key differences: system prompt moves to a top-level field (not a `messages` entry); images become content blocks with `type: "image"` and a `source` object (not an `images[]` array); tool results go back as `role: "user"` with `type: "tool_result"` content blocks (not `role: "tool"`); auth uses `x-api-key` header (not `Authorization: Bearer`); `anthropic-version: 2023-06-01` is required on every request.

**2. Race condition on `PendingResult` — silent dropped responses**
The current `volatile` field pattern is not atomic for a read-then-null sequence. Must replace with `Interlocked.Exchange(ref _pendingResult, null)`. This becomes a crash risk when heartbeat adds concurrent requests.

**3. Synchronous file I/O on the game thread — visible stutters**
`NarrativeMemorySystem` writes (including the O(n) `AppendToLog`) run on the game thread inside `OnUpdate`. Move all file writes to `Task.Run`. Fix before adding heartbeat, which multiplies write frequency.

**4. Heartbeat cost runaway — unbounded API spend**
At $0.02/call and a 5-minute interval over 8 hours: ~$2/session. Must ship with: configurable interval (default 10 minutes), per-session call counter visible in UI, exponential backoff on failure, pause toggle, and low-resolution or no screenshot for background checks.

**5. Context window bloat — requests fail silently after long sessions**
Each tool call adds 200–500 tokens to history. A heartbeat sharing the chat history will accumulate to the 200k token limit. Use a separate short-lived message history for each heartbeat invocation. Track `usage` field from every API response.

**6. CS2 DLL locked by running game — testing against stale code**
Always close CS2 completely before `dotnet build`. Log a version string from `Mod.cs` on every startup to confirm which build loaded.

---

## Recommended Build Order

The dependency chain is clear. Research across all four files converges on the same sequence:

### Phase 1: Claude API Migration (Unblocks Everything)
**Rationale:** The entire mod is broken until this is done. Tool calls, image input, and the tool loop all fail with the Ollama format. This is the highest-risk change (most code touched) and must be validated before anything else is layered on.

**Deliverables:**
- Rewrite `ClaudeAPISystem.RunRequestAsync`: correct endpoint, headers (`x-api-key`, `anthropic-version`), system as top-level field, image content blocks, tool result as `role:"user"` with `tool_use_id`
- Rename settings fields (`OllamaApiKey` → `ApiKey`, etc.)
- Fix `Interlocked.Exchange` for `PendingResult`
- Add `CancellationTokenSource` for dispose safety
- Increase tool loop cap from 10 to 20
- Move `NarrativeMemorySystem` file writes off game thread

**Pitfalls to avoid:** Pitfalls 1, 2, 3, 10, 16

**End-to-end validation:** Text message → API response → tool call fires → memory write → screenshot included. All in-game.

---

### Phase 2: Chat UI Polish (Reduces Rework Before Features)
**Rationale:** The memory explorer and heartbeat both surface content in the chat panel. Polish the panel once before adding new content types, not three times after.

**Deliverables:**
- Distinct user vs. assistant message bubbles
- Loading/thinking indicator (animated, clearly visible during API wait)
- Markdown rendering edge case fixes (nested lists, escaped characters)
- Scrollable chat history

**Pitfalls to avoid:** Pitfall 7 (XSS via `dangerouslySetInnerHTML` — sanitize before injecting)

---

### Phase 3: Web Search Tool (Isolated, Low Risk)
**Rationale:** No dependencies on heartbeat or explorer. Single new file (`SearchWebTool.cs`) plus a settings field. Extends advisor quality significantly for minimal implementation risk.

**Deliverables:**
- `SearchWebTool : ICityAgentTool` — Brave Search API GET, `X-Subscription-Token` auth, 5 results max
- `SearchApiKey` in `Settings.cs`
- Registered in `ClaudeAPISystem.OnCreate`
- Prompt injection mitigation in system prompt and result length cap

**Pitfalls to avoid:** Pitfalls 7, 15

---

### Phase 4: Missing ECS Tools (Raises Advisor Intelligence Floor)
**Rationale:** Budget and traffic are the two most common CS2 advisor topics. The advisor cannot give useful advice without them. Heartbeat also needs richer data to surface meaningful alerts.

**Deliverables:**
- `get_budget()` — income, expenses, debt, tax rates from `EconomySystem`
- `get_traffic_summary()` — congestion index or average speed (start simple; full network analysis is v2)
- `get_services_summary()` — healthcare, education, deathcare coverage gaps

**Pitfalls to avoid:** Pitfall 8 (CS2 API breakage — wrap ECS queries in try-catch, log meaningful errors)

---

### Phase 5: Memory File Explorer (Trust and Control Layer)
**Rationale:** Memory must be solid before the explorer is worth building. This phase requires stable bindings, working file operations, and a polished panel to host the new tab.

**Deliverables:**
- New bindings in `CityAgentUISystem`: `memoryFilesJson`, `memoryFileContentJson`, `listMemoryFiles`, `readMemoryFile`, `writeMemoryFile`, `deleteMemoryFile`
- New public methods on `NarrativeMemorySystem`: `GetFileList()`, `ReadFile()`, `IsProtected()`
- React: `MemoryExplorerPanel.tsx` tab inside `CityAgentPanel` — flexbox tree, no CSS Grid, no external libraries
- Protected file enforcement (core files cannot be deleted)

**Pitfalls to avoid:** Pitfalls 9, 12 (z-index conflicts, Coherent GT flexbox-only constraint)

---

### Phase 6: Proactive Heartbeat System (Highest Complexity, Needs Stable Foundation)
**Rationale:** Heartbeat depends on a working API, richer ECS tools, a polished chat panel, and reliable memory. Build last to avoid compounding instability across all those layers.

**Deliverables:**
- Frame counter in `CityAgentUISystem.OnUpdate` (not a new system)
- `BeginHeartbeatRequest()` in `ClaudeAPISystem` — synthetic prompt, no screenshot or low-res, separate message history
- `HeartbeatEnabled` (default: false) and `HeartbeatIntervalMinutes` (default: 10) in `Settings.cs`
- Per-session call counter visible in UI
- Exponential backoff on API failure
- Pause toggle in panel

**Pitfalls to avoid:** Pitfalls 2, 4, 5, 14

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Claude API wire format | HIGH | Verified against official Anthropic docs; format is precise and documented |
| Brave Search API | HIGH | Official docs confirm endpoint, headers, response shape |
| CS2 DOTS threading patterns | HIGH | Confirmed by existing `CityDataSystem` frame-counter pattern in codebase |
| `ValueBinding`/`TriggerBinding` pattern | HIGH | Existing system has 7 bindings and 5 triggers — pattern is proven |
| Heartbeat frame math | MEDIUM | Assumes ~60fps; actual frame rate varies; real-time accumulator is more robust |
| NarrativeMemorySystem public API surface | MEDIUM | Systems not fully read; method names inferred from tool implementations |
| Coherent GT CSS compatibility | MEDIUM | Flexbox-only confirmed by CS2 wiki; specific `var()` and other feature support unverified |
| ECS budget/traffic query shape | MEDIUM | CS2 ECS schema is undocumented; exact component names require runtime discovery |

---

## Open Questions

1. **Does `CityToolRegistry.GetToolsJson()` (Claude format) match the current Anthropic spec, or has it drifted?** It is dead code — called nowhere. Audit against docs before enabling it.

2. **What is the exact ECS component name and shape for CS2's budget system?** The `EconomySystem` name is inferred; needs runtime verification via game DLL inspection before `get_budget()` can be implemented.

3. **Does Coherent GT block inline scripts via CSP, or is XSS via `dangerouslySetInnerHTML` a real runtime risk?** The answer changes how aggressively to sanitize AI-generated content.

4. **What is the current `NarrativeMemorySystem` public method surface?** The architecture assumes `ReadFile`, `WriteFile`, `GetFileList`, and `IsProtected` are either present or thin wrappers. This needs a direct code read before the memory explorer phase begins.

5. **Is `moduleRegistry.append("Game.MainScreen", ...)` still broken in the current CS2 version?** If it has been restored, the `document.body.appendChild` workaround and associated z-index issues go away.

---

## Sources (Aggregated)

- [Anthropic Claude Messages API](https://docs.anthropic.com/en/api/messages) — wire format, headers, tool use, image content blocks
- [Anthropic Tool Use — Implement Tool Use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use) — tool_use/tool_result format, stop_reason, tool_use_id
- [Anthropic Vision Guide](https://platform.claude.com/docs/en/build-with-claude/vision) — base64 image content blocks
- [Brave Search API](https://api-dashboard.search.brave.com/app/documentation/web-search/get-started) — endpoint, auth header, response structure
- [Microsoft Semantic Kernel BraveConnector](https://github.com/microsoft/semantic-kernel/pull/11308) — C#/.NET Brave integration pattern
- [Unity DOTS Time.DeltaTime](https://discussions.unity.com/t/time-deltatime-in-dots/830729) — `World.Time.DeltaTime` in SystemBase
- [CS2 UI Modding wiki](https://cs2.paradoxwikis.com/UI_Modding) — Coherent GT CSS constraints, binding patterns
- [Smashing Magazine — Designing for Agentic AI](https://www.smashingmagazine.com/2026/02/designing-agentic-ai-practical-ux-patterns/) — interrupt and notification design
- [Smashing Magazine — Notification UX](https://www.smashingmagazine.com/2025/07/design-guidelines-better-notifications-ux/) — alert fatigue
- [Info Loom mod](https://thunderstore.io/c/cities-skylines-ii/p/Infixo/Info_Loom/) — CS2 data panel UX reference
- Existing codebase — `CityDataSystem.cs`, `ClaudeAPISystem.cs`, `CityAgentUISystem.cs`, `CityToolRegistry.cs`, `NarrativeMemorySystem.cs`
