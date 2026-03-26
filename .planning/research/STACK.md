# Technology Stack — Milestone Research
**Project:** CityAgent
**Milestone scope:** Web search tool, proactive heartbeat system, Claude API format migration, memory file explorer UI
**Researched:** 2026-03-26
**Overall confidence:** HIGH (Claude API format, Brave Search API, DOTS timer pattern from existing codebase); MEDIUM (React file tree without npm)

---

## Existing Stack (Do Not Change)

These layers are fixed by CS2's mod runtime. Every new feature must fit inside them.

| Layer | Technology | Constraint |
|-------|-----------|-----------|
| Game bridge | C# .NET Standard 2.1 (Mono inside Unity 2022.3.7f1) | No NuGet runtime installs; must reference game-provided DLLs only |
| UI runtime | Coherent GT (older Chromium) | No npm packages at runtime; React/ReactDOM/cs2 APIs are runtime-injected globals |
| JSON serialization | Newtonsoft.Json (sourced from `Cities2_Data/Managed/`) | Already available; do not add a second JSON library |
| HTTP client | `System.Net.Http.HttpClient` (static singleton `s_Http`) | Already present; reuse for all new HTTP calls |
| Build toolchain | `dotnet build` (C#) + Webpack 5.89 + ts-loader (UI) | No changes needed |

---

## Feature 1: Claude API Format Migration

### What must change

`ClaudeAPISystem.cs` currently sends Ollama native `/api/chat` format. The Claude API (`api.anthropic.com/v1/messages`) uses a different wire format. `CityToolRegistry.GetToolsJson()` already produces the correct Claude tool schema — it is not the API call wrapper.

### Endpoint

```
POST https://api.anthropic.com/v1/messages
```

### Required request headers

| Header | Value |
|--------|-------|
| `x-api-key` | `{ApiKey}` (from mod settings) |
| `anthropic-version` | `2023-06-01` (fixed string, required on every request) |
| `Content-Type` | `application/json` |

Do NOT send `Authorization: Bearer`. The Claude API uses `x-api-key` only.

### Request body shape

```json
{
  "model": "claude-sonnet-4-6",
  "max_tokens": 4096,
  "system": "<system prompt string>",
  "tools": [
    {
      "name": "get_population",
      "description": "...",
      "input_schema": { "type": "object", "properties": {}, "required": [] }
    }
  ],
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "image",
          "source": {
            "type": "base64",
            "media_type": "image/png",
            "data": "<base64-encoded PNG, no data: prefix>"
          }
        },
        {
          "type": "text",
          "text": "User message text"
        }
      ]
    }
  ]
}
```

Key differences from Ollama native format:

| Concern | Ollama `/api/chat` | Claude `/v1/messages` |
|---------|-------------------|-----------------------|
| System prompt | `messages[0]` with `"role":"system"` | Top-level `"system"` field |
| Image attachment | `userMsg["images"]` array of base64 strings | Content block with `"type":"image"` and `"source"` object |
| Tool schema key | `"parameters"` (OpenAI) | `"input_schema"` (Claude) |
| Tool calls in response | `message.tool_calls[].function.{name,arguments}` | `content[]` blocks with `"type":"tool_use"`, `id`, `name`, `input` |
| Tool result message | `{"role":"tool","content":"..."}` | `{"role":"user","content":[{"type":"tool_result","tool_use_id":"...","content":"..."}]}` |
| Auth header | `Authorization: Bearer {key}` | `x-api-key: {key}` |
| `stream` field | required as `false` | omit or set `"stream": false` |
| `done` field | response top-level | not present; check `stop_reason` |

### Response body shape

```json
{
  "id": "msg_...",
  "type": "message",
  "role": "assistant",
  "content": [
    { "type": "text", "text": "..." },
    { "type": "tool_use", "id": "toolu_...", "name": "get_population", "input": { ... } }
  ],
  "stop_reason": "tool_use",
  "model": "claude-sonnet-4-6-...",
  "usage": { "input_tokens": 123, "output_tokens": 45 }
}
```

When `stop_reason` is `"tool_use"`, iterate tool_use blocks, dispatch each, and send results back as a `user` message with `tool_result` content blocks. When `stop_reason` is `"end_turn"`, read text blocks for the final narrative.

### Tool result loop message structure

```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_abc123",
      "content": "{ \"population\": 12450 }"
    }
  ]
}
```

Multiple tool results from the same assistant turn go in a single `user` message as multiple `tool_result` content blocks.

### Settings renames

`Settings.cs` fields are named after Ollama. Rename (or add aliases) for clarity:

| Old name | New name | Default |
|----------|----------|---------|
| `OllamaApiKey` | `ApiKey` | `""` |
| `OllamaModel` | `ModelName` | `"claude-sonnet-4-6"` |
| `OllamaBaseUrl` | Remove or keep as override | `"https://api.anthropic.com"` |

The base URL override is worth keeping for users who proxy through a custom endpoint, but default to `api.anthropic.com`.

### No additional libraries needed

`System.Net.Http.HttpClient` + `Newtonsoft.Json` + `JObject`/`JArray` cover everything. The existing `GetToolsJson()` method on `CityToolRegistry` already produces the correct Claude-format schema (`input_schema`). Only `ClaudeAPISystem.cs` and `Settings.cs` need changes.

**Confidence:** HIGH — Claude API format verified against official docs (`docs.anthropic.com/en/api/messages`).

---

## Feature 2: Web Search Tool (Brave Search API)

### Why Brave, not Bing

Brave Search API is the correct choice:
- No Microsoft Azure account or subscription required
- Free tier: 2,000 queries/month (more than sufficient for infrequent agent tool use)
- Single auth header, simple GET request — one HTTP call, JSON response
- Independent index, not a wrapper over Google/Bing
- Bing Search API requires Azure subscription + Microsoft account

### Brave Search API integration

**Endpoint:**
```
GET https://api.search.brave.com/res/v1/web/search?q={query}&count=5
```

**Required headers:**
```
Accept: application/json
Accept-Encoding: gzip
X-Subscription-Token: {BraveApiKey}
```

Note: `Accept-Encoding: gzip` is listed in Brave's docs but .NET's `HttpClient` handles gzip decompression automatically when `HttpClientHandler.AutomaticDecompression` is set. The header is safe to include anyway.

**Key query parameters:**

| Parameter | Type | Notes |
|-----------|------|-------|
| `q` | string (required) | Max 400 chars, 50 words |
| `count` | int | Max 20; use 5 for agent tool calls |
| `offset` | int | Pagination, default 0 |
| `search_lang` | string | e.g. `"en"` |

**Response shape (simplified):**
```json
{
  "web": {
    "results": [
      {
        "title": "...",
        "url": "...",
        "description": "..."
      }
    ]
  }
}
```

For Claude's `search_web` tool, extract `title`, `url`, and `description` from each result and format as a compact string or JSON array that Claude can reason over.

### C# implementation approach

Reuse the existing static `s_Http` instance in `ClaudeAPISystem`. Add a `SearchWebAsync(string query)` method that:
1. Constructs the GET URL with `Uri.EscapeDataString(query)`
2. Adds `X-Subscription-Token` and `Accept: application/json` headers
3. Awaits `s_Http.GetAsync(...)` (already async, consistent with existing pattern)
4. Parses `web.results[]` from the JSON response
5. Returns a formatted string of top-N results

Keep the `BraveApiKey` in `Settings.cs` alongside `ApiKey`. Store it in the same CS2 mod settings storage. Never log it.

**No new DLLs or NuGet packages.** Everything needed is already present.

**Confidence:** HIGH — Brave Search API endpoint and auth pattern confirmed from official docs and Semantic Kernel integration reference.

---

## Feature 3: Proactive Heartbeat System

### Pattern from existing codebase

`CityDataSystem.OnUpdate()` already demonstrates the correct CS2 DOTS throttle pattern:

```csharp
// From CityDataSystem.cs line 89
if (m_SimulationSystem.frameIndex % 128 != 77) return;
```

This runs every 128 game simulation frames (~4 seconds at 30fps). The heartbeat system needs a much longer interval — minutes, not seconds.

### Recommended pattern for heartbeat

`SimulationSystem.frameIndex` is a `uint`. At 30fps, 60 seconds = 1,800 frames. A 5-minute interval = 9,000 frames. A 10-minute interval = 18,000 frames.

For long intervals, modulo arithmetic still works but loses precision when the game is paused or running at a different speed. A more robust approach uses real elapsed time:

```csharp
private float m_HeartbeatElapsed = 0f;
private float m_HeartbeatIntervalSeconds = 300f; // 5 minutes, user-configurable

protected override void OnUpdate()
{
    // World.Time.DeltaTime is the game's frame delta in seconds.
    // Available in GameSystemBase — no import beyond Game.dll needed.
    m_HeartbeatElapsed += World.Time.DeltaTime;

    if (m_HeartbeatElapsed < m_HeartbeatIntervalSeconds) return;
    m_HeartbeatElapsed = 0f;

    if (m_ClaudeAPI.IsRequestInFlight) return; // never interrupt an active request
    TriggerHeartbeat();
}
```

`World.Time.DeltaTime` is available on any `GameSystemBase` / `SystemBase` in Unity DOTS — it returns real seconds per frame (not game-speed adjusted). The paused game sends DeltaTime = 0, so the timer naturally freezes when the game is paused.

### Implementation choices

**Option A: New `HeartbeatSystem : GameSystemBase`** (recommended)
- Separate system with its own `OnUpdate`
- Calls `ClaudeAPISystem.BeginRequest()` with a pre-built heartbeat prompt
- Clean separation of concerns — does not complicate `CityAgentUISystem`

**Option B: Add heartbeat logic inside `CityAgentUISystem`**
- Simpler (one fewer system)
- But `CityAgentUISystem` already handles UI bindings, screenshot capture, key polling, and API result polling — adding heartbeat makes it significantly more complex

Choose Option A. Add a `HeartbeatSystem` registered after `ClaudeAPISystem` in `Mod.cs`.

### User-configurable interval

Add to `Settings.cs`:
```csharp
[SettingsUISection(kSection, kGeneralGroup)]
[SettingsUISlider(min = 1, max = 60, step = 1)]
public int HeartbeatIntervalMinutes { get; set; } = 10;

[SettingsUISection(kSection, kGeneralGroup)]
public bool HeartbeatEnabled { get; set; } = false; // off by default
```

The heartbeat is off by default. Players opt in. Unsolicited interruptions are a negative experience if the player is mid-build.

**Confidence:** HIGH — frame-based throttle confirmed from existing `CityDataSystem.cs` pattern; `World.Time.DeltaTime` is standard DOTS API available in Unity 2022.3.7f1.

---

## Feature 4: Memory File Explorer UI

### Constraint

Coherent GT's older Chromium does not support npm packages at runtime. React, ReactDOM, and cs2 APIs are injected as window globals. No third-party component libraries can be bundled. The file tree must be built from scratch in TypeScript/React.

### Approach: hand-rolled recursive component (no dependencies)

This is the correct choice — not a compromise. The memory tree structure is shallow (one directory level, flat file list with optional `archive/` and `chat-history/` subdirectories), not a general-purpose infinite-depth tree. A recursive component of ~100-150 lines covers it fully.

**Why not `react-complex-tree` or similar:**
- Requires bundling (Webpack must include it) — adds bundle size and a devDependency
- The CS2 Coherent GT environment is tested to break on packages using modern JS APIs absent from older Chromium
- The memory tree is simple enough that a library would add more indirection than value

### Data model

`ListMemoryFilesTool` already returns JSON metadata for all memory files. The file explorer reads this binding. No new bindings required for browse/view mode.

New bindings needed:
- `memoryFileContent` (string) — content of currently selected file (empty = none selected)
- `loadMemoryFile` trigger (string filename) — reads a file and writes content to binding
- `deleteMemoryFile` trigger (string filename) — wraps `DeleteMemoryFileTool`

These can be added to `CityAgentUISystem` using the existing `ValueBinding` + `TriggerBinding` pattern.

### Component structure

```
MemoryExplorerPanel.tsx
  MemoryFileTree.tsx          — renders grouped file list (core files, user files, subdirs)
    MemoryFileRow.tsx         — single row: icon + name + delete button (for non-core)
  MemoryFileViewer.tsx        — shows content of selected file (scrollable pre/code block)
```

`MemoryFileTree` receives the parsed `ListMemoryFilesTool` JSON (already flowing over the `messagesJson` binding or a dedicated binding). File state (selected file, content) lives in the panel component via `useState`.

### TypeScript type

```typescript
interface MemoryFile {
  name: string;
  path: string;
  sizeBytes: number;
  isCore: boolean;           // core files cannot be deleted
  subdirectory?: string;     // "archive" | "chat-history" | undefined
}
```

### Rendering

Plain CSS styling — consistent with the existing `CityAgent.css`. No CSS-in-JS library. Use `<ul>/<li>` with `cursor: pointer` and hover states for interactivity. The existing `renderMarkdown` utility can render file content.

**Confidence:** MEDIUM — no ecosystem verification of specific Coherent GT JS compatibility, but pattern is consistent with existing project constraint (all external libraries already excluded).

---

## What NOT to Use

| Option | Why Not |
|--------|---------|
| Bing Search API | Requires Azure subscription; more complex OAuth flow; overkill for this use case |
| `Claudia` (Cysharp/Claudia NuGet) | NuGet packages require runtime install; not compatible with CS2 mod DLL pattern |
| `Anthropic.SDK` (tghamm NuGet) | Same NuGet constraint — cannot be deployed as a mod DLL without bundling all dependencies |
| `System.Timers.Timer` for heartbeat | Fires on a thread pool thread, not the game thread; unsafe to touch ECS state from a pool thread |
| `react-complex-tree` or other tree libs | Requires bundling; Coherent GT compatibility untested; unnecessary for a shallow file list |
| Streaming API (`"stream": true`) | Coherent GT's JS bridge does not support streaming; keep `stream: false` (matches current implementation) |
| Multiple `HttpClient` instances | Already using a static singleton `s_Http`; creating per-feature instances wastes sockets |

---

## Summary Recommendations

| Feature | Approach | New Dependencies | Files Changed |
|---------|----------|-----------------|---------------|
| Claude API format | Rewrite `ClaudeAPISystem.cs` request/response handling; rename settings fields | None | `ClaudeAPISystem.cs`, `Settings.cs` |
| Web search | Add `search_web` tool + `SearchWebAsync` in `ClaudeAPISystem.cs`; new `SearchWebTool.cs`; add `BraveApiKey` to settings | None (reuse HttpClient) | `ClaudeAPISystem.cs`, `Settings.cs`, new tool file |
| Heartbeat | New `HeartbeatSystem.cs`; add `HeartbeatIntervalMinutes` + `HeartbeatEnabled` to settings | None | New system file, `Settings.cs`, `Mod.cs` |
| Memory explorer | New React components; 2-3 new C# bindings in `CityAgentUISystem.cs` | None | New `.tsx` files, `CityAgentUISystem.cs` |

All four features are buildable with zero new external dependencies. The existing `HttpClient`, `Newtonsoft.Json`, and React (runtime-injected) cover everything.

---

## Sources

- [Anthropic Claude Messages API reference](https://docs.anthropic.com/en/api/messages) — request/response format, headers, tool_use schema, image content blocks
- [Claude API Vision docs](https://platform.claude.com/docs/en/build-with-claude/vision) — base64 image content block format (`type: "image"`, `source.type: "base64"`)
- [Brave Search API documentation](https://api-dashboard.search.brave.com/app/documentation/web-search/get-started) — endpoint, `X-Subscription-Token` auth, query params, response structure
- [Brave Search API quickstart](https://api-dashboard.search.brave.com/documentation/quickstart) — curl example confirming GET pattern
- [Microsoft Semantic Kernel BraveConnector PR](https://github.com/microsoft/semantic-kernel/pull/11308) — confirms C#/.NET integration pattern for Brave Search API
- [Unity DOTS Time.DeltaTime discussion](https://discussions.unity.com/t/time-deltatime-in-dots/830729) — confirms `World.Time.DeltaTime` in SystemBase
- Existing codebase — `CityDataSystem.cs` line 89: frame-modulo throttle pattern; `CityToolRegistry.cs`: `GetToolsJson()` already produces Claude-format tool schema; `ClaudeAPISystem.cs`: full current Ollama-format implementation reviewed
