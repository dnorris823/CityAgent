# Architecture Patterns

**Project:** CityAgent — CS2 Mod + Claude AI Advisor
**Researched:** 2026-03-26
**Scope:** Heartbeat system, web search tool, memory file explorer, Claude API migration

---

## Existing System Map (Baseline)

Before documenting new component architecture, the established system has these layers and boundaries:

```
CS2 Game Thread
  CityAgentUISystem (UISystemBase, UIUpdate phase)
    - Owns all ValueBinding / TriggerBinding objects
    - Polls ClaudeAPISystem.PendingResult each frame
    - Manages screenshot capture (file poll over ~10 frames)
    - Serialises chat history to messagesJson (JSON string over bridge)
    - Delegates persistence to NarrativeMemorySystem

  CityDataSystem (GameSystemBase, GameSimulation phase)
    - ECS queries, refreshed every 128 frames (~4 s)
    - Exposes public properties (no direct ECS access elsewhere)

  ClaudeAPISystem (GameSystemBase, OnUpdate is no-op)
    - Owns static HttpClient
    - BeginRequest() fires async Task; writes to volatile PendingResult
    - RunRequestAsync: Ollama /api/chat format (TO BE MIGRATED)
    - CityToolRegistry dispatches tool calls synchronously within async task

  NarrativeMemorySystem (GameSystemBase, OnUpdate is no-op)
    - Per-city markdown file I/O
    - Called imperatively by UISystem and ClaudeAPISystem

.NET Thread Pool
  ClaudeAPISystem.RunRequestAsync
    - All HTTP I/O, all tool execution
    - Writes only to volatile PendingResult; no other cross-thread writes

React UI (Coherent GT, browser thread)
  CityAgentPanel.tsx
    - Consumes 7 useValue() subscriptions
    - Triggers: togglePanel, sendMessage, clearChat, removeScreenshot, captureScreenshot
```

---

## Component 1: Claude API Format Migration

### What Changes

`ClaudeAPISystem.RunRequestAsync` currently constructs Ollama-native `/api/chat` requests. It must be replaced with Anthropic Messages API format against `https://api.anthropic.com/v1/messages`.

### Endpoint and Headers

```
POST https://api.anthropic.com/v1/messages
x-api-key: {ApiKey}
anthropic-version: 2023-06-01
content-type: application/json
```

No `Authorization: Bearer` header. The Anthropic API uses `x-api-key` only.

### Request Body Differences

**Ollama (current):**
```json
{
  "model": "...",
  "messages": [{"role": "system", "content": "..."}, ...],
  "tools": [...],
  "stream": false
}
```
Images attached as: `{"role": "user", "content": "...", "images": ["base64..."]}`

**Claude API (target):**
```json
{
  "model": "claude-sonnet-4-6",
  "max_tokens": 4096,
  "system": "...",
  "messages": [...],
  "tools": [...]
}
```

Key structural differences:
- `system` is a top-level field, NOT a message with `role: "system"`
- Images embedded as content blocks in the user message, not an `images` array
- `max_tokens` is required
- No `stream` field (streaming is a separate endpoint pattern; non-streaming is default)

### Image Content Block (Claude API format)

```json
{
  "role": "user",
  "content": [
    {
      "type": "image",
      "source": {
        "type": "base64",
        "media_type": "image/png",
        "data": "<base64string>"
      }
    },
    {
      "type": "text",
      "text": "User message text"
    }
  ]
}
```

When there is no image, `content` can be a plain string instead of an array:
```json
{"role": "user", "content": "User message text"}
```

### Response Structure Differences

**Ollama (current):**
```json
{
  "message": {
    "role": "assistant",
    "content": "...",
    "tool_calls": [{"function": {"name": "...", "arguments": {...}}}]
  },
  "done": true
}
```

**Claude API (target):**
```json
{
  "id": "msg_...",
  "role": "assistant",
  "stop_reason": "tool_use",
  "content": [
    {"type": "text", "text": "..."},
    {
      "type": "tool_use",
      "id": "toolu_...",
      "name": "get_population",
      "input": {"param": "value"}
    }
  ]
}
```

Detection of tool calls: `stop_reason == "tool_use"` (not presence of `tool_calls` array).
Plain text response: `stop_reason == "end_turn"`.

### Tool Result Message Format

**Ollama (current):**
```json
{"role": "tool", "content": "result string"}
```

**Claude API (target):**
```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_...",
      "content": "result string"
    }
  ]
}
```

Critical differences:
- Tool results go back as `role: "user"`, not `role: "tool"`
- Each result must include `tool_use_id` matching the `id` from the `tool_use` block
- Multiple tool results for parallel calls go in the same user message content array
- `tool_result` blocks must come before any `text` blocks in the content array

### Tool Schema Format

The existing `CityToolRegistry.GetToolsJsonOpenAI()` method already emits OpenAI-style tool schema. The Claude API tools array format is compatible:
```json
[{
  "name": "get_population",
  "description": "...",
  "input_schema": {
    "type": "object",
    "properties": {...},
    "required": [...]
  }
}]
```
No changes needed to `ICityAgentTool` interface or individual tool implementations. Only the registry's serialisation method name may need updating for clarity.

### Settings Changes

Current setting fields (`OllamaBaseUrl`, `OllamaApiKey`, `OllamaModel`) are semantically wrong. Rename or add:
- `ApiKey` — Anthropic API key
- `ModelName` — defaults to `claude-sonnet-4-6`
- Remove or repurpose `OllamaBaseUrl` (hardcode `https://api.anthropic.com` or allow override for proxy use)

**Confidence:** HIGH — Verified against official Anthropic documentation.

---

## Component 2: Heartbeat System

### Architecture Decision

Do NOT create a new CS2 System for heartbeat. Use the existing `CityAgentUISystem.OnUpdate` which already runs every frame in `UIUpdate` phase. Add a frame counter there.

**Rationale:** Adding another `GameSystemBase` for heartbeat means scheduling it, managing its lifecycle, and adding cross-system communication. `CityAgentUISystem` already has the full context: it owns `m_ClaudeAPI`, tracks `m_RequestInFlight` state via `m_IsLoading`, and manages the chat history. The heartbeat check is a single counter increment and a conditional call — it belongs in the system that already orchestrates all of this.

### Update Loop Integration

```
CityAgentUISystem.OnUpdate (every frame, game thread):
  1. Existing: drain PendingResult from ClaudeAPISystem
  2. Existing: poll screenshot file
  3. Existing: poll settings every ~60 frames
  4. NEW: increment heartbeat counter
     if counter >= HeartbeatIntervalFrames AND not loading AND panel exists:
       reset counter
       m_ClaudeAPI.BeginHeartbeatRequest()
```

`HeartbeatIntervalFrames` derived from settings: e.g. 60 fps * 60 seconds * N minutes. A 5-minute interval = 18,000 frames. The counter approach avoids `System.Timers.Timer` (which creates a background thread) and keeps all state on the game thread.

### Heartbeat Request Path

```
CityAgentUISystem
  → ClaudeAPISystem.BeginHeartbeatRequest()
      - Generates a synthetic prompt: "Proactive city check. No user question."
      - Passes no screenshot (heartbeat is stat-only)
      - Uses same RunRequestAsync path
      - PendingResult written as normal

  → CityAgentUISystem.OnUpdate drains PendingResult
      - Appends heartbeat response as assistant message with a flag:
        {role: "assistant", content: "...", isHeartbeat: true}
      - React can style heartbeat messages differently (subtle, dimmed)
```

### New Bindings Required

```
C# → JS: heartbeatEnabled (bool)   — whether heartbeat is on
C# → JS: heartbeatInterval (int)   — minutes between checks (display only)
JS → C#: setHeartbeatEnabled (trigger, bool arg) — toggle
```

Alternatively, expose these through the existing Settings panel (mod options) rather than in-panel controls, deferring new bindings until UI polish phase.

### Guard Conditions

Heartbeat must not fire when:
- Another request is in flight (`m_RequestInFlight`)
- Panel is not yet initialised / memory not loaded
- API key is empty
- Heartbeat is disabled in settings

### Build Order Implication

Heartbeat depends on a working `ClaudeAPISystem` (Claude API format migration must come first). Heartbeat is purely additive — it adds a counter and a conditional call. Build after API migration.

**Confidence:** HIGH for the counter-in-UISystem approach. MEDIUM for the exact binding surface (may simplify to settings-only in v1).

---

## Component 3: Web Search Tool

### Architecture

Web search follows the existing `ICityAgentTool` pattern exactly. A new `SearchWebTool` class:

```
SearchWebTool : ICityAgentTool
  Name: "search_web"
  Description: "Search the web for real-world urban planning information..."
  InputSchema: { query: string, count?: number }
  Execute(inputJson):
    → Reads search API key from Mod.ActiveSetting.SearchApiKey
    → Makes SYNCHRONOUS HttpWebRequest (or HttpClient) to search API
    → Returns formatted results as JSON string
```

The tool executes synchronously within `ClaudeAPISystem.RunRequestAsync` (which is already on the .NET thread pool — synchronous I/O is fine there). No new async pattern needed.

### Search API: Brave Search

Endpoint: `GET https://api.search.brave.com/res/v1/web/search?q={query}&count={count}`

Required header: `X-Subscription-Token: {SearchApiKey}`
Response: JSON with `web.results[]` containing `title`, `url`, `description`.

Do NOT use `Authorization: Bearer`. Brave uses a proprietary header.

### Result Formatting

Return to Claude as a compact JSON string:
```json
{
  "query": "pedestrian zone urban planning benefits",
  "results": [
    {"title": "...", "url": "https://...", "snippet": "..."},
    {"title": "...", "url": "https://...", "snippet": "..."}
  ]
}
```

Limit to 5 results by default (configurable). Keep snippets to 200 characters. This keeps tool result tokens reasonable.

### API Key Storage

`SearchApiKey` added as a new field on `Settings.cs` (extends `ModSetting`). This surfaces in CS2's built-in mod options UI alongside the existing Anthropic API key. Never passed to the search API unless non-empty; tool returns an error message if key is missing.

### Registration

Register in `ClaudeAPISystem.OnCreate`:
```csharp
m_ToolRegistry.Register(new SearchWebTool());
```

`SearchWebTool` reads the key from `Mod.ActiveSetting` at `Execute()` time (not at construction time), so settings changes take effect without restart.

### HttpClient Note

`ClaudeAPISystem` already owns a static `HttpClient`. The `SearchWebTool` can share it (pass it in via constructor) or use its own. Given the tool executes within the same async task and C#'s `HttpClient` is thread-safe, sharing is preferred. Pass the `HttpClient` reference to `SearchWebTool` at registration time.

### Component Boundaries

```
ClaudeAPISystem (owns HttpClient, owns ToolRegistry)
  → SearchWebTool.Execute()
       reads: Mod.ActiveSetting.SearchApiKey
       uses: shared HttpClient (passed at construction)
       calls: Brave Search API
       returns: JSON string to ToolRegistry.Dispatch
  → Result appended to messages as tool_result block (same as all other tools)
```

No new systems. No new bindings. No UI changes. Pure C# addition.

### Build Order Implication

Web search tool can be built in parallel with heartbeat, after API migration. It only touches `SearchWebTool.cs`, `Settings.cs`, and one line in `ClaudeAPISystem.OnCreate`.

**Confidence:** HIGH for tool pattern and Brave API format. MEDIUM for shared HttpClient (verify thread-safety of concurrent calls; if issues arise, SearchWebTool gets its own static instance).

---

## Component 4: Memory File Explorer

### Architecture Decision

The memory explorer is a UI feature backed by existing C# capabilities. The right pattern is new `ValueBinding` / `TriggerBinding` pairs in `CityAgentUISystem`, consistent with the existing bridge contract. Do NOT create a separate system.

### Why Not a Separate System

`NarrativeMemorySystem` already exposes file operations imperatively. `CityAgentUISystem` already calls it. The explorer is just a new surface for the same operations — adding bindings to the existing bridge owner (`CityAgentUISystem`) is the correct pattern.

### New Bindings

```
C# → JS: memoryFilesJson (string)
  — JSON array of file metadata:
    [{"path": "characters.md", "size": 1234, "isProtected": true}, ...]
  — Refreshed on demand (not every frame)

C# → JS: memoryFileContentJson (string)
  — JSON object: {"path": "...", "content": "...", "error": null}
  — Populated after readMemoryFile trigger

JS → C#: listMemoryFiles (trigger)
  — Calls NarrativeMemorySystem.ListFiles()
  — Updates memoryFilesJson binding

JS → C#: readMemoryFile (trigger, string path)
  — Calls NarrativeMemorySystem.ReadFile(path)
  — Updates memoryFileContentJson binding

JS → C#: writeMemoryFile (trigger, string argsJson)
  — argsJson: {"path": "...", "content": "..."}
  — Calls NarrativeMemorySystem.WriteFile(path, content)
  — Triggers listMemoryFiles refresh

JS → C#: deleteMemoryFile (trigger, string path)
  — Only allowed for non-protected files
  — NarrativeMemorySystem enforces protection list
  — Triggers listMemoryFiles refresh
```

### Data Flow

```
React (user opens explorer tab)
  → trigger("cityAgent", "listMemoryFiles")
  → CityAgentUISystem.OnListMemoryFiles()
       calls NarrativeMemorySystem.GetFileList()
       serialises to JSON
       m_MemoryFilesJson.Update(json)
  → React re-renders file tree

React (user clicks a file)
  → trigger("cityAgent", "readMemoryFile", "characters.md")
  → CityAgentUISystem.OnReadMemoryFile(path)
       calls NarrativeMemorySystem.ReadFile(path)
       serialises to JSON: {path, content}
       m_MemoryFileContentJson.Update(json)
  → React renders content in editor pane

React (user saves edits)
  → trigger("cityAgent", "writeMemoryFile", "{\"path\":\"...\",\"content\":\"...\"}")
  → CityAgentUISystem.OnWriteMemoryFile(argsJson)
       deserialises, calls NarrativeMemorySystem.WriteFile(path, content)
       calls NarrativeMemorySystem.GetFileList() to refresh
       m_MemoryFilesJson.Update(refreshed list)
```

### File I/O Thread Consideration

`NarrativeMemorySystem` file I/O is synchronous. In `CityAgentUISystem.OnUpdate` (game thread), synchronous file reads for small markdown files (typically < 50 KB) complete in under 1 ms — acceptable for interactive operations triggered by user action (not every frame). If a file is unexpectedly large, consider offloading to a `Task` and draining the result in subsequent frames, but this is unlikely to be needed for the use case.

### React UI Structure

The explorer is a tab or section within `CityAgentPanel`. It does not require a separate component registration (no new `coui://` entry point). It consumes:
- `memoryFilesJson` via `useValue(bindValue<string>("cityAgent", "memoryFilesJson"))`
- `memoryFileContentJson` via `useValue(bindValue<string>("cityAgent", "memoryFileContentJson"))`

### NarrativeMemorySystem New Methods Required

The existing public API may not expose all needed primitives. Likely additions:
- `GetFileList()` → returns list of `{relativePath, sizeBytes, isProtected}` objects
- `ReadFile(relativePath)` → returns string content (already exists as memory tool logic, needs a direct public method)
- `WriteFile(relativePath, content)` → already exists (maps to WriteMemoryFileTool logic)
- `IsProtected(relativePath)` → needed for UI to show lock icons

These are thin wrappers over existing file system calls — no new I/O patterns.

### Build Order Implication

Memory explorer is independent of API migration and heartbeat. It only touches:
- `CityAgentUISystem.cs` (new bindings + handlers)
- `NarrativeMemorySystem.cs` (new public methods)
- `CityAgentPanel.tsx` (new UI tab)

Can be built in any order relative to other components. Recommended: after API migration (to reduce total in-flight risk), before heartbeat.

**Confidence:** HIGH for binding pattern (matches existing system). MEDIUM for exact NarrativeMemorySystem method surface (depends on what's already public).

---

## Updated Component Map

```
CS2 Game Thread
  CityAgentUISystem (UISystemBase, UIUpdate phase)
    Existing bindings (7 values, 5 triggers)
    NEW bindings:
      memoryFilesJson (string)         — explorer file list
      memoryFileContentJson (string)   — explorer file content
      heartbeatEnabled (bool)          — heartbeat toggle [optional v1]
    NEW triggers:
      listMemoryFiles                  — refresh file list
      readMemoryFile (string path)     — load file content
      writeMemoryFile (string args)    — save file
      deleteMemoryFile (string path)   — delete non-protected file
      setHeartbeatEnabled (bool)       — [optional v1]
    NEW internal state:
      m_HeartbeatCounter (int)         — frame counter for heartbeat

  CityDataSystem (unchanged)

  ClaudeAPISystem
    CHANGED: RunRequestAsync → Claude API format (v1/messages)
    CHANGED: Settings field names (OllamaApiKey → ApiKey, etc.)
    CHANGED: Message construction (system top-level, content blocks, tool_result)
    CHANGED: Response parsing (stop_reason, content array, tool_use id tracking)
    NEW: BeginHeartbeatRequest() — synthetic prompt, no screenshot
    NEW: SearchWebTool registered at OnCreate

  NarrativeMemorySystem
    NEW public methods: GetFileList(), ReadFile(), IsProtected()
    (existing WriteFile-equivalent logic extracted from tool classes)

  Tools/
    Existing tools: unchanged
    NEW: SearchWebTool
      reads: Mod.ActiveSetting.SearchApiKey
      uses: shared HttpClient from ClaudeAPISystem
      calls: Brave Search API (GET /res/v1/web/search)

.NET Thread Pool
  ClaudeAPISystem.RunRequestAsync (unchanged thread model)
  SearchWebTool.Execute() runs here (within RunRequestAsync task)

Settings.cs
  NEW fields: SearchApiKey, HeartbeatEnabled, HeartbeatIntervalMinutes
  RENAMED: OllamaApiKey → ApiKey, OllamaModel → ModelName, OllamaBaseUrl → (removed or ApiBaseUrl)

React UI
  CityAgentPanel.tsx
    NEW: explorer tab (memoryFilesJson, memoryFileContentJson)
    NEW: heartbeat message styling (isHeartbeat flag)
    CHANGED: isLoading indicator (already exists, no new binding needed)
```

---

## Data Flow: Claude API Migration

### Request Construction

```
CityAgentUISystem.OnSendMessage(text)
  → m_ClaudeAPI.BeginRequest(text, base64Png)

ClaudeAPISystem.RunRequestAsync:
  1. Read ApiKey, ModelName from Mod.ActiveSetting
  2. Read system prompt + narrative context from NarrativeMemorySystem
  3. Build system string (top-level, not in messages[])
  4. Build initial messages[]:
     - {role: "user", content: [ {type:"image",...}, {type:"text","text":userMsg} ]}
       OR if no image: {role: "user", content: userMsg}
  5. Loop (max 10):
     a. POST /v1/messages with {model, max_tokens, system, messages, tools}
     b. Parse response content array
     c. If stop_reason == "tool_use":
        - Collect all tool_use blocks (parallel calls possible)
        - Append assistant message (full content array) to messages[]
        - Execute each tool: m_ToolRegistry.Dispatch(name, input_json)
        - Append {role:"user", content:[{type:"tool_result", tool_use_id:id, content:result},...]}
        - Loop
     d. If stop_reason == "end_turn":
        - Extract text from content[].type=="text" blocks
        - Set PendingResult
        - Return
```

### Key Parsing Change

Tool call extraction changes from:
```csharp
// Ollama: tc["function"]["name"], tc["function"]["arguments"]
```
to:
```csharp
// Claude API: iterate content array, find type=="tool_use"
// toolId = block["id"], name = block["name"], input = block["input"]
```

The `tool_use_id` must be stored and echoed back in the corresponding `tool_result` block — this is new complexity not present in the Ollama format.

---

## Data Flow: Web Search

```
Claude (in tool-use loop): calls search_web({"query":"pedestrian zones"})
  → CityToolRegistry.Dispatch("search_web", inputJson)
  → SearchWebTool.Execute(inputJson):
       parse query from inputJson
       read SearchApiKey from Mod.ActiveSetting
       GET https://api.search.brave.com/res/v1/web/search?q={query}&count=5
         header: X-Subscription-Token: {key}
         (sync await within async task — fine on thread pool)
       parse response JSON
       format: {query, results:[{title, url, snippet},...]}
       return as JSON string
  → ToolRegistry returns result string
  → ClaudeAPISystem appends as tool_result in next messages[] entry
  → Claude uses search results to ground its response
```

---

## Build Order

The four components have these dependencies:

```
1. Claude API Migration  ← no dependencies, blocks heartbeat
2. Web Search Tool       ← no dependencies on other new components
3. Memory File Explorer  ← no dependencies on other new components
4. Heartbeat System      ← depends on working Claude API (migration must be done first)
```

Recommended build sequence:
1. Claude API Migration (highest risk, most code change, unblocks heartbeat)
2. Web Search Tool (isolated, low risk — one new file + settings field)
3. Memory File Explorer (isolated, medium risk — new bindings + React tab)
4. Heartbeat System (low risk once API works — counter + conditional call)

2 and 3 can be built in parallel if working across systems simultaneously.

---

## Integration Points with Existing Systems

| New Component | Existing System | Integration Point |
|---------------|----------------|-------------------|
| API Migration | ClaudeAPISystem | RunRequestAsync rewritten in-place |
| API Migration | Settings.cs | Field rename/addition |
| Web Search | ClaudeAPISystem | SearchWebTool registered in OnCreate |
| Web Search | Settings.cs | SearchApiKey field added |
| Memory Explorer | CityAgentUISystem | New bindings + trigger handlers |
| Memory Explorer | NarrativeMemorySystem | New public accessor methods |
| Memory Explorer | CityAgentPanel.tsx | New explorer tab/pane |
| Heartbeat | CityAgentUISystem | Frame counter in OnUpdate |
| Heartbeat | ClaudeAPISystem | BeginHeartbeatRequest() method |
| Heartbeat | Settings.cs | HeartbeatEnabled, HeartbeatIntervalMinutes |

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: New GameSystemBase for Heartbeat
**What:** Creating a `HeartbeatSystem : GameSystemBase` to manage the timer.
**Why bad:** Adds a new system lifecycle, cross-system communication channels, and scheduling complexity. The heartbeat is a counter increment — it does not need its own system.
**Instead:** Frame counter in `CityAgentUISystem.OnUpdate`, which already coordinates all request state.

### Anti-Pattern 2: System.Timers.Timer for Heartbeat
**What:** Using `System.Timers.Timer` (fires on thread pool) to trigger heartbeat calls.
**Why bad:** Introduces a second thread that reads `m_RequestInFlight` and calls `BeginRequest` — races with the game thread. Requires locking that is absent in the current architecture.
**Instead:** Frame counter on the game thread (UIUpdate phase).

### Anti-Pattern 3: Synchronous HTTP on Game Thread
**What:** Calling search API or Claude API directly in `OnUpdate` or a trigger handler.
**Why bad:** Blocks the game thread, causing visible freezes.
**Instead:** All HTTP calls inside `ClaudeAPISystem.RunRequestAsync` (thread pool). SearchWebTool executes synchronously only because it runs within the already-async RunRequestAsync task.

### Anti-Pattern 4: New System for Memory Explorer
**What:** Creating a `MemoryExplorerSystem : GameSystemBase` to handle file list queries.
**Why bad:** `NarrativeMemorySystem` already owns the file I/O. Adding a wrapper system duplicates responsibility.
**Instead:** New trigger handlers in `CityAgentUISystem` calling existing `NarrativeMemorySystem` methods directly.

### Anti-Pattern 5: Tool Result as role:"tool"
**What:** Sending tool results back with `{"role": "tool", "content": "..."}` (Ollama format).
**Why bad:** The Claude API rejects this. Tool results must be `role: "user"` with `type: "tool_result"` content blocks.
**Instead:** `{"role": "user", "content": [{"type": "tool_result", "tool_use_id": "toolu_...", "content": "..."}]}`

### Anti-Pattern 6: System Prompt as Messages Entry
**What:** Including `{"role": "system", "content": "..."}` in the `messages` array.
**Why bad:** The Claude API treats `system` as a top-level field, not a message role. Sending it in `messages` will be rejected or ignored.
**Instead:** `"system": "..."` at the top level of the request body.

---

## Confidence Assessment

| Area | Confidence | Source |
|------|------------|--------|
| Claude API message format | HIGH | Official Anthropic docs (platform.claude.com) |
| Image content block format | HIGH | Official Anthropic vision docs |
| Tool use / tool_result format | HIGH | Official Anthropic tool use docs |
| stop_reason values | HIGH | Official Anthropic docs |
| Brave Search API endpoint/header | HIGH | Official Brave Search API docs |
| CS2 UIUpdate frame counter pattern | HIGH | Established by existing CityDataSystem (128-frame counter) |
| ValueBinding pattern for explorer | HIGH | Established by existing system (7 existing bindings) |
| SearchWebTool in async task | HIGH | Standard .NET thread pool behavior |
| HeartbeatIntervalFrames math | MEDIUM | Assumes ~60 fps; actual frame rate varies |
| NarrativeMemorySystem public API surface | MEDIUM | Systems not fully read; assumed from tool implementations |

---

## Sources

- [Anthropic Tool Use — Implement Tool Use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use) — tool_use/tool_result JSON format, stop_reason values, tool_use_id requirement
- [Anthropic Vision Guide](https://platform.claude.com/docs/en/build-with-claude/vision) — base64 image content blocks, media_type values, request structure
- [Brave Search API Documentation](https://api-dashboard.search.brave.com/app/documentation/web-search/get-started) — endpoint, X-Subscription-Token header
- Existing codebase: `ClaudeAPISystem.cs`, `CityAgentUISystem.cs`, `ICityAgentTool.cs` — all interface and thread model patterns
