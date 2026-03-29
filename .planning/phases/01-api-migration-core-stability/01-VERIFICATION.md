---
phase: 01-api-migration-core-stability
verified: 2026-03-28T23:30:00Z
status: passed
score: 6/6 must-haves verified
re_verification: false
human_verification:
  - test: "End-to-end in-game validation (CORE-03)"
    expected: "Claude responds with narrative via /v1/messages, screenshot vision works, tool calls fire and return city data, narrative memory files written, no UI freeze"
    why_human: "Live API call + in-game display + visual stutter detection cannot be verified programmatically"
    resolution: "APPROVED by user at Plan 04 human-verify checkpoint"
---

# Phase 1: API Migration & Core Stability — Verification Report

**Phase Goal:** The Claude API (and Ollama) works correctly end-to-end — tool calls fire, screenshots are sent, memory is written, responses render
**Verified:** 2026-03-28T23:30:00Z
**Status:** passed
**Re-verification:** No — initial verification

---

## Context: Post-Checkpoint Settings Redesign

Plans 01-01 through 01-04 were executed. After the four plans completed, a post-checkpoint fix was applied that changed the Settings architecture. Plan 01-01 specified a "Claude primary + Ollama fallback" two-section layout with `OllamaFallbackBaseUrl / OllamaFallbackApiKey / OllamaFallbackModel` field names. The post-fix implementation added a **Provider toggle** (`ProviderChoice` enum: Claude or Ollama) with a `kProviderGroup` section, retaining the Ollama fields as first-class `OllamaBaseUrl / OllamaApiKey / OllamaModel`. The `ClaudeAPISystem` was updated to route by `setting.Provider` rather than treating Claude as the unconditional primary.

This is a superset of the planned behavior — the goal (user can configure and select between Claude and Ollama) is more fully achieved. The Plan 01 must_haves are evaluated against the goal, not the original field name spec.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Claude API requests use Anthropic /v1/messages format with correct headers and body | VERIFIED | `ClaudeAPISystem.cs` line 181: `const string endpoint = "https://api.anthropic.com/v1/messages"`. Lines 234-235: `x-api-key` and `anthropic-version` headers set. Line 218: `["system"] = sysPrompt` as top-level field. |
| 2 | Screenshots are sent as image content blocks (type:base64, media_type:image/png) | VERIFIED | `ClaudeAPISystem.cs` lines 186-196: `["type"] = "image"`, `["source"] = { ["type"] = "base64", ["media_type"] = "image/png", ["data"] = base64Png }`. Screenshot encoding runs on background `Task.Run` in `CityAgentUISystem.cs` lines 135-152. |
| 3 | Tool calls fire and results return via tool_result user messages with tool_use_id | VERIFIED | `ClaudeAPISystem.cs` lines 277-294: iterates `content` array for `type == "tool_use"`, dispatches via `m_ToolRegistry.Dispatch`, builds `["type"] = "tool_result"` with `["tool_use_id"]`. Loop runs up to 10 iterations. |
| 4 | Narrative memory is written off the main game thread | VERIFIED | `NarrativeMemorySystem.cs` lines 348-368 (`WriteFileAsync`), 371-410 (`AppendToLogAsync`), 539-556 (`SaveChatSessionAsync`): all use `File.WriteAllTextAsync` with `ConfigureAwait(false)`. `CityAgentUISystem.cs` line 261: `_ = m_NarrativeMemory.SaveChatSessionAsync(markdown)` — fire-and-forget. |
| 5 | Response text renders in the chat panel | VERIFIED | `CityAgentUISystem.cs` lines 156-163: `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)` drains result atomically each frame. On non-null: appended to `m_History`, `PushMessagesBinding()` called to update `messagesJson` binding for React. |
| 6 | User can select Claude API or Ollama as primary provider in settings | VERIFIED | `Settings.cs` lines 11-43: `ProviderChoice` enum, `Provider` property with dropdown, `kProviderGroup` section. `ClaudeAPISystem.cs` lines 98-113: routes by `setting.Provider == ProviderChoice.Ollama`. Both providers fully implemented. |

**Score:** 6/6 truths verified

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Settings.cs` | Two-section Claude + Ollama settings with provider toggle, ClaudeApiKey, ClaudeModel, OllamaBaseUrl, ActiveProvider | VERIFIED | Contains `ProviderChoice` enum, `Provider` dropdown, `ClaudeApiKey`, `ClaudeModel`, `OllamaBaseUrl`, `OllamaApiKey`, `OllamaModel`, `ActiveProvider` getter, `kProviderGroup` + `kClaudeGroup` + `kOllamaGroup` sections. `kGeneralGroup` fully removed. |
| `src/Systems/ClaudeAPISystem.cs` | Anthropic /v1/messages client, Ollama fallback, Interlocked thread safety | VERIFIED | 449 lines. Contains `RunClaudeRequestAsync` (Anthropic format), `RunOllamaRequestAsync` (OpenAI-compat), 16 `Interlocked.Exchange` calls, `volatile bool m_RequestInFlight`. Routes by `setting.Provider`. |
| `src/Systems/NarrativeMemorySystem.cs` | Async write methods (WriteFileAsync, AppendToLogAsync, SaveChatSessionAsync, CreateFileAsync, DeleteFileAsync) | VERIFIED | 782 lines. 7 async Task methods, 12 `ConfigureAwait(false)` usages, all 5 public write methods async. `ReadFile` / `ListFiles` / `GetAlwaysInjectedContext` remain synchronous. |
| `src/Systems/CityAgentUISystem.cs` | Interlocked PendingResult drain, fire-and-forget SaveChatSessionAsync, background screenshot Task.Run | VERIFIED | 276 lines. Line 156: `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)`. Line 261: `_ = m_NarrativeMemory.SaveChatSessionAsync(markdown)`. Lines 135-152: `Task.Run` for screenshot encoding. |
| `src/Systems/Tools/WriteMemoryFileTool.cs` | Calls WriteFileAsync via GetAwaiter().GetResult() | VERIFIED | Line 20: `m_Memory.WriteFileAsync(filename, content).GetAwaiter().GetResult()` |
| `src/Systems/Tools/AppendNarrativeLogTool.cs` | Calls AppendToLogAsync via GetAwaiter().GetResult() | VERIFIED | Line 19: `m_Memory.AppendToLogAsync(entry).GetAwaiter().GetResult()` |
| `src/Systems/Tools/CreateMemoryFileTool.cs` | Calls CreateFileAsync via GetAwaiter().GetResult() | VERIFIED | Line 20: `m_Memory.CreateFileAsync(filename, content).GetAwaiter().GetResult()` |
| `src/Systems/Tools/DeleteMemoryFileTool.cs` | Calls DeleteFileAsync via GetAwaiter().GetResult() | VERIFIED | Line 19: `m_Memory.DeleteFileAsync(filename).GetAwaiter().GetResult()` |
| `src/Systems/Tools/CityToolRegistry.cs` | GetToolsJson() (Anthropic format) and GetToolsJsonOpenAI() (OpenAI-compat format) | VERIFIED | Lines 28-46: `GetToolsJson()` with `input_schema`. Lines 52-69: `GetToolsJsonOpenAI()` with `parameters` under `function` object. Both used in correct branches of `ClaudeAPISystem`. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `ClaudeAPISystem.cs` | `https://api.anthropic.com/v1/messages` | HttpClient POST | WIRED | Line 181: endpoint constant. Lines 229-238: `HttpRequestMessage(HttpMethod.Post, endpoint)`. |
| `ClaudeAPISystem.cs` | `{OllamaBaseUrl}/v1/chat/completions` | HttpClient POST | WIRED | Line 347: `string endpoint = $"{ollamaBase}/v1/chat/completions"`. |
| `ClaudeAPISystem.cs` | `CityToolRegistry.GetToolsJson()` | Anthropic tools array | WIRED | Line 209: `JArray.Parse(m_ToolRegistry.GetToolsJson())` — used in Claude branch. |
| `ClaudeAPISystem.cs` | `CityToolRegistry.GetToolsJsonOpenAI()` | Ollama tools array | WIRED | Line 357: `JArray.Parse(m_ToolRegistry.GetToolsJsonOpenAI())` — used in Ollama branch. |
| `ClaudeAPISystem.cs` | `Settings.cs` | `Mod.ActiveSetting.ClaudeApiKey`, `.ClaudeModel`, `.OllamaBaseUrl`, `.OllamaApiKey`, `.OllamaModel`, `.Provider` | WIRED | Lines 98-152: reads `setting.Provider`, `setting.OllamaBaseUrl`, `setting.OllamaApiKey`, `setting.OllamaModel`, `setting.ClaudeApiKey`, `setting.ClaudeModel` per request. No cached settings. |
| `CityAgentUISystem.cs` | `ClaudeAPISystem.PendingResult` | `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)` | WIRED | Line 156: atomic drain each frame. |
| `CityAgentUISystem.cs` | `NarrativeMemorySystem.SaveChatSessionAsync` | fire-and-forget `_ = ...` | WIRED | Line 261: `_ = m_NarrativeMemory.SaveChatSessionAsync(markdown)`. |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `CityAgentUISystem.cs` — chat panel render | `m_MessagesJson` binding | `m_History` list populated by `Interlocked.Exchange` drain of `ClaudeAPISystem.PendingResult` | Yes — `PendingResult` set by real HTTP response from Anthropic or Ollama | FLOWING |
| `ClaudeAPISystem.cs` — PendingResult | `PendingResult` string | HTTP response from `api.anthropic.com/v1/messages` or Ollama endpoint | Yes — parsed from `responseJson["content"]` text blocks or Ollama `choices[0].message.content` | FLOWING |
| `NarrativeMemorySystem.cs` — memory files | `WriteFileAsync` content | Tool calls dispatch from `CityToolRegistry`, arguments from Claude response | Yes — `AppendToLogAsync` / `WriteFileAsync` write actual AI-generated content | FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — no runnable entry points without CS2 game process. All behaviors require the live game thread and Anthropic HTTP endpoint. End-to-end behavior approved via human-verify checkpoint (CORE-03).

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| CORE-01 | Plan 02, Plan 04 | File writes execute off main game thread — no UI freezes | SATISFIED | `NarrativeMemorySystem` write methods are `async Task` with `File.WriteAllTextAsync`. `CityAgentUISystem` calls `SaveChatSessionAsync` as fire-and-forget. Screenshot encoding in `Task.Run`. |
| CORE-02 | Plan 03, Plan 04 | `PendingResult` uses `Interlocked.Exchange` to prevent race conditions | SATISFIED | `ClaudeAPISystem.PendingResult` is non-volatile; 16 `Interlocked.Exchange` usages in `ClaudeAPISystem.cs`. `CityAgentUISystem.cs` drains with `Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null)`. `m_RequestInFlight` is `volatile bool`. |
| CORE-03 | Plan 04 | End-to-end in-game cycle passes — screenshot, tool calls, memory, response render | SATISFIED | **Human-verified** — approved by user at Plan 04 checkpoint. Claude responds, vision works, tool calls fire, memory files written, response renders. |
| API-01 | Plan 03 | Claude `/v1/messages` format — correct headers, system top-level, image blocks, tool_result with tool_use_id | SATISFIED | `x-api-key` and `anthropic-version` headers set. `["system"]` top-level field. Image content block with `type:base64`. Tool results as `role:user` with `type:tool_result` and `tool_use_id`. |
| API-02 | Plan 01 (post-fix) | Ollama API `/v1/chat/completions` supported — user can select provider in settings | SATISFIED | `ProviderChoice` enum, dropdown in `kProviderGroup`. `RunOllamaRequestAsync` implements full OpenAI-compatible tool loop. |
| API-03 | Plan 03 | Automatic rate-limit fallback — HTTP 429 from Claude retries with Ollama or surfaces clear error | SATISFIED | `ClaudeAPISystem.cs` lines 134-153: `__429__` sentinel triggers Ollama fallback with in-panel rate-limit notice (`⚠️ Rate limited — retrying with {model}...`). If no Ollama configured, surfaces clear error message. |
| API-04 | Plan 01, Plan 04 | User-configurable model name — changes take effect without restarting the game | SATISFIED | `ClaudeModel` and `OllamaModel` properties in Settings. `RunRequestAsync` reads `Mod.ActiveSetting` per-request — no cached model name anywhere in the call chain. |

All 7 phase requirements are satisfied. No orphaned requirements found — REQUIREMENTS.md traceability table maps exactly CORE-01, CORE-02, CORE-03, API-01, API-02, API-03, API-04 to Phase 1.

---

### Plan 01 Must-Haves vs Post-Fix Implementation

Plan 01-01 specified `OllamaFallbackBaseUrl`, `OllamaFallbackApiKey`, `OllamaFallbackModel` field names and a "Ollama Fallback (optional)" section header. The post-checkpoint Provider toggle redesign replaced these with `OllamaBaseUrl`, `OllamaApiKey`, `OllamaModel` (first-class fields, not "fallback" naming) and a separate `kProviderGroup` with a `Provider` dropdown.

**Assessment:** The plan-level field name deviations are intentional design improvements that exceed the plan's goal. The phase goal ("Claude API and Ollama work correctly end-to-end") is fully achieved. The post-fix adds a cleaner UX (explicit provider selection vs implicit fallback) and is confirmed working by the human-verify checkpoint.

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `src/Systems/NarrativeMemorySystem.cs` | 183-187 | `File.WriteAllText` (synchronous) in `EnsureDirectoryStructure` — writes template files at startup | Info | One-time initialization path called from `Initialize()`, not from game loop or message handler. Templates written synchronously at mod load before gameplay begins. Not a runtime stutter risk. |

No blockers found. The synchronous template write is in the initialization path (called once at startup from `OnUpdate` before gameplay, before any user interaction). It is not on the message-send hot path and does not block the API or screenshot flow.

---

### Human Verification Required

#### 1. End-to-End In-Game Validation (CORE-03)

**Test:** Build (`dotnet build -c Release` + `npm run build`), deploy, launch CS2, open CityAgent panel, send a message with screenshot, verify tool calls in log, check memory folder on disk.
**Expected:** Claude responds with narrative, describes the screenshot, city data appears in response via tool calls, chat session file created in memory folder, no UI freeze.
**Why human:** Live API call + in-game display + visual stutter detection cannot be verified programmatically without the CS2 process running.
**Resolution:** APPROVED by user at Plan 04 human-verify checkpoint.

---

## Gaps Summary

No gaps. All 6 observable truths are verified. All 7 requirements are satisfied (CORE-03 via approved human checkpoint). The post-checkpoint Provider toggle is a planned design improvement, not a gap — it fully satisfies API-02 and extends the Ollama support beyond what the original plans specified.

---

_Verified: 2026-03-28T23:30:00Z_
_Verifier: Claude (gsd-verifier)_
