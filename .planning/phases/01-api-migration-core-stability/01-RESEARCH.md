# Phase 01: API Migration & Core Stability - Research

**Researched:** 2026-03-26
**Domain:** Anthropic /v1/messages API, C# async patterns (.NET Standard 2.1 / Unity Mono), CS2 ModSetting, thread safety
**Confidence:** HIGH (API format verified from official docs; threading patterns verified from MSDN; CS2 settings verified from official wiki)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Settings reorganized into two separate sections: **"Claude API"** (API key, model) and **"Ollama Fallback (optional)"** (base URL, optional API key, model). Clean break — old `OllamaApiKey` / `OllamaModel` / `OllamaBaseUrl` fields deleted entirely (not marked `[Obsolete]`).
- **D-02:** Default Claude model: `claude-sonnet-4-6`
- **D-03:** Default Ollama base URL: `http://localhost:11434`
- **D-04:** A read-only **"active provider" status label** is shown in settings (e.g., "Currently using: Claude API"). Not duplicated in the chat panel.
- **D-05:** Ollama Fallback section header is explicitly labeled "(optional)" so it is clear it is not required to use the mod.
- **D-06:** When Claude returns HTTP 429, show an **in-panel system notice**: `⚠️ Rate limited — retrying with [ollama-model-name]...` then send the request to Ollama. The Ollama response renders with normal styling — no footer, no provider label on the response itself.
- **D-07:** **Only HTTP 429 triggers fallback.** Other Claude errors (400, 401, 500) show `[Error]: ...` in chat without falling back to Ollama — those indicate config problems, not transient rate limits.
- **D-08:** If Claude rate-limits and **no Ollama fallback is configured**, show a clear error in chat: `⚠️ Rate limited by Claude. No Ollama fallback configured — set one up in mod settings.`
- **D-09:** No Ollama connectivity validation in Phase 1.
- **D-10:** Tool call format is **auto-selected per active provider**: `GetToolsJson()` (Anthropic format) for Claude; `GetToolsJsonOpenAI()` (OpenAI-compatible) for Ollama.
- **D-11:** **Full async refactor of NarrativeMemorySystem** — `WriteFile`, `AppendToLog`, `SaveChatSession`, and related methods become async throughout.
- **D-12:** Memory file writes are **fire-and-forget** on the calling side. Write failures are logged via `Mod.Log.Error` and swallowed.
- **D-13:** **Screenshot base64 encoding** moves to a background thread in Phase 1.
- **D-14:** `PendingResult` uses **`Interlocked.Exchange`** to replace the volatile-only pattern. `m_RequestInFlight` gets the **`volatile` keyword**.

### Claude's Discretion
- Exact async method signatures and Task patterns in NarrativeMemorySystem
- How the "active provider" label reads current state (polling vs. event-driven)
- Whether to introduce a `Provider` enum or string-based routing
- System notice message styling (role type for in-panel notices)

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CORE-01 | File writes (narrative log, chat session, screenshot encode) execute off the main game thread — no UI freezes during I/O | Async pattern section; fire-and-forget with Task.Run; ConfigureAwait(false) |
| CORE-02 | Concurrent API requests are safe — `PendingResult` uses `Interlocked.Exchange` to prevent race conditions | Interlocked.Exchange section; volatile for m_RequestInFlight |
| CORE-03 | End-to-end tested — full build → deploy → in-game cycle passes with screenshot, tool calls, narrative memory, and response rendering working together | Integration checklist in Validation Architecture section |
| API-01 | Claude API (`/v1/messages`) format fully supported — correct headers, system as top-level field, image content blocks, `tool_result` as user role with `tool_use_id` | Anthropic API format section with verified JSON examples |
| API-02 | Ollama API supported alongside Claude — user can select provider in settings | Ollama fallback section; Settings refactor section |
| API-03 | Automatic rate-limit fallback — Claude 429 retries with configured Ollama endpoint | Rate-limit fallback pattern section |
| API-04 | User-configurable model name in mod settings — changes take effect without restarting the game | Settings refactor section; settings are read per-request from `Mod.ActiveSetting` |
</phase_requirements>

---

## Summary

Phase 1 is a backend rewrite: swap the Ollama `/api/chat` format for the Anthropic `/v1/messages` format, fix two thread-safety defects in `ClaudeAPISystem`, make `NarrativeMemorySystem` fully async, and move screenshot base64 encoding off the game thread. The surface area for UI changes is limited to the in-game settings panel (renaming and restructuring fields).

The Anthropic `/v1/messages` API differs from Ollama's native format in three critical ways: (1) `system` is a top-level field, not a `messages` array entry; (2) images are `content` blocks with `{"type":"image","source":{"type":"base64","media_type":"image/png","data":"..."}}` rather than an `images[]` array; (3) tool results are sent as `role: "user"` messages containing `{"type":"tool_result","tool_use_id":"<id>","content":"..."}` blocks, not `role: "tool"` messages. The existing `GetToolsJson()` method in `CityToolRegistry` already produces the correct Anthropic tool definition format and just needs to be wired in.

The thread-safety fixes are straightforward: `Interlocked.Exchange(ref PendingResult, null)` atomically reads and clears the pending result in one operation, eliminating the read-then-null race. Adding `volatile` to `m_RequestInFlight` (read on thread pool, written on main thread) prevents the double-send race.

**Primary recommendation:** Migrate `ClaudeAPISystem.RunRequestAsync` to Anthropic format first, verify tool-use loop end-to-end in-game, then apply threading fixes and async NarrativeMemory as parallel work. The API format is the critical path; everything else is polish.

---

## Standard Stack

### Core (unchanged — all already in project)
| Library | Source | Purpose | Notes |
|---------|--------|---------|-------|
| `System.Net.Http.HttpClient` | Game Managed DLLs | HTTP calls to Anthropic and Ollama | Reuse existing `s_Http` static instance |
| `Newtonsoft.Json` (`JObject`, `JArray`) | Game Managed DLLs | JSON construction and parsing | Already used; continue with same pattern |
| `System.Threading.Interlocked` | .NET Standard 2.1 BCL | Atomic exchange for `PendingResult` | `Interlocked.Exchange(ref field, value)` |
| `System.Threading.Tasks.Task` | .NET Standard 2.1 BCL | Async work off game thread | `_ = Task.Run(async () => {...})` pattern |
| `System.IO.File` (async variants) | .NET Standard 2.1 BCL | Async file I/O in NarrativeMemory | `File.WriteAllTextAsync`, `File.ReadAllTextAsync` |

### No New Dependencies Required
The entire phase is implemented using libraries already present in the project or the .NET Standard 2.1 BCL. No NuGet packages need to be added.

---

## Architecture Patterns

### Pattern 1: Anthropic /v1/messages Request Format

**Key differences from current Ollama native format:**

```
POST https://api.anthropic.com/v1/messages
x-api-key: {API_KEY}
anthropic-version: 2023-06-01
content-type: application/json
```

Request body:
```json
{
  "model": "claude-sonnet-4-6",
  "max_tokens": 4096,
  "system": "You are CityAgent...",
  "messages": [
    {
      "role": "user",
      "content": [
        {
          "type": "image",
          "source": {
            "type": "base64",
            "media_type": "image/png",
            "data": "<base64_string>"
          }
        },
        {
          "type": "text",
          "text": "What should I build next?"
        }
      ]
    }
  ],
  "tools": [
    {
      "name": "get_population",
      "description": "Returns city population data.",
      "input_schema": {
        "type": "object",
        "properties": {},
        "required": []
      }
    }
  ]
}
```

Source: Verified from [platform.claude.com/docs/en/build-with-claude/vision](https://platform.claude.com/docs/en/build-with-claude/vision) and [platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) — HIGH confidence.

### Pattern 2: Anthropic Tool-Use Response Parsing

When Claude calls a tool, `stop_reason` is `"tool_use"` and `content` is an array of blocks:

```json
{
  "id": "msg_01Aq9w938a90dw8q",
  "type": "message",
  "role": "assistant",
  "stop_reason": "tool_use",
  "content": [
    {
      "type": "text",
      "text": "Let me check the population."
    },
    {
      "type": "tool_use",
      "id": "toolu_01A09q90qw90lq917835lq9",
      "name": "get_population",
      "input": {}
    }
  ]
}
```

Parse loop:
1. Check `stop_reason == "tool_use"`
2. Find all blocks where `type == "tool_use"`
3. Extract `id`, `name`, `input` from each block
4. Execute tool, then send `tool_result` back

### Pattern 3: Tool Result Message Format (Anthropic)

The tool result is a **user-role** message (NOT `role: "tool"`):

```json
{
  "role": "user",
  "content": [
    {
      "type": "tool_result",
      "tool_use_id": "toolu_01A09q90qw90lq917835lq9",
      "content": "{\"population\": 12500, \"households\": 4200}"
    }
  ]
}
```

**Critical:** When multiple tools are called in one turn, all `tool_result` blocks go in the same user message. `tool_result` blocks must come FIRST in the content array if any text is also included. Missing this causes a 400 error.

Source: [platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) — HIGH confidence.

### Pattern 4: Full Message History Structure for Claude

The messages array must alternate user/assistant. The assistant's full `content` array (including `tool_use` blocks) must be appended before the tool_result user message:

```
messages = [
  { "role": "user",      "content": [image_block, text_block] },          // 1. Initial user message
  { "role": "assistant", "content": [text_block, tool_use_block] },        // 2. Claude's tool call
  { "role": "user",      "content": [tool_result_block] },                 // 3. Tool result
  { "role": "assistant", "content": [text_block] }                         // 4. Final response
]
```

When appending the assistant turn in the loop: use the entire `content` array from the response (JArray), not just the tool_use portion.

### Pattern 5: Anthropic vs Ollama Format Comparison

| Aspect | Anthropic /v1/messages | Ollama /api/chat (current) | Ollama /v1/chat/completions (fallback) |
|--------|----------------------|--------------------------|---------------------------------------|
| System prompt | Top-level `"system"` field | `{"role":"system","content":"..."}` in messages | `{"role":"system","content":"..."}` in messages |
| Image | Content block `{"type":"image","source":{"type":"base64","media_type":"image/png","data":"..."}}` | `"images": ["<base64>"]` on message | Content block (OpenAI-style) |
| Tool definition | `{"name","description","input_schema"}` | `{"type":"function","function":{"name","description","parameters"}}` | Same as Anthropic tool def format for tools array |
| Tool call response | `stop_reason: "tool_use"`, `content[].type == "tool_use"` with `id` | `message.tool_calls[].function.{name,arguments}` | `choices[0].finish_reason: "tool_calls"`, `choices[0].message.tool_calls[].{id,function}` |
| Tool result | `role: "user"`, `content: [{type:"tool_result","tool_use_id","content"}]` | `role: "tool"`, `content: "..."` | `role: "tool"`, `tool_call_id: "..."`, `content: "..."` |
| Auth header | `x-api-key: {KEY}` | `Authorization: Bearer {KEY}` | `Authorization: Bearer {KEY}` (key often ignored) |
| API version header | `anthropic-version: 2023-06-01` (required) | None | None |
| End-of-turn signal | `stop_reason == "end_turn"` | `done: true` with no `tool_calls` | `finish_reason: "stop"` |

### Pattern 6: Ollama Fallback (OpenAI-Compatible) Format

The Ollama fallback branch uses `/v1/chat/completions` (OpenAI-compatible endpoint). The existing `GetToolsJsonOpenAI()` already produces the correct tool definition format. Differences from native Ollama format:

- Endpoint: `{baseUrl}/v1/chat/completions` (not `/api/chat`)
- System as a message with `role: "system"` (same as current code)
- Tool result: `role: "tool"`, `tool_call_id: "<id from response>"`, `content: "<result>"`
- Response check: `choices[0].finish_reason == "tool_calls"` (not `done: true`)
- Tool calls in: `choices[0].message.tool_calls[]` with `{id, type, function: {name, arguments}}`
- `arguments` is a string (serialized JSON), not an object — must parse it

### Pattern 7: Interlocked.Exchange for PendingResult

Replace the two-step read-then-null with a single atomic operation:

```csharp
// In CityAgentUISystem.OnUpdate (main thread):
// BEFORE (race: another thread could write between read and null):
string? result = m_ClaudeAPI.PendingResult;
if (result != null)
{
    m_ClaudeAPI.PendingResult = null;
    ...
}

// AFTER (atomic: reads and replaces with null in one operation):
string? result = Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null);
if (result != null)
{
    ...
}
```

For this to work, `PendingResult` must be a non-volatile plain field (or remove `volatile` — `Interlocked` provides its own memory barrier). Change declaration to:

```csharp
public string? PendingResult = null;  // volatile keyword removed; Interlocked provides barrier
```

And in `RunRequestAsync`, write via `Interlocked.Exchange` as well to be symmetric:

```csharp
Interlocked.Exchange(ref PendingResult, finalContent ?? "[Error]: Empty response.");
```

Source: [Microsoft Learn — Interlocked.Exchange](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.exchange) — HIGH confidence.

### Pattern 8: volatile for m_RequestInFlight

`m_RequestInFlight` is written from two threads (main thread sets `true` in `BeginRequest`; thread pool sets `false` in `finally`). Add `volatile`:

```csharp
private volatile bool m_RequestInFlight = false;
```

The `volatile` keyword guarantees the main thread always reads the current value, preventing a stale `false` read that would allow a second concurrent request to fire.

### Pattern 9: Fire-and-Forget Async Pattern for NarrativeMemory Writes

D-11 and D-12 require memory writes to be async and fire-and-forget. The pattern in .NET Standard 2.1 (no `Task.Run` overhead for I/O-bound work):

```csharp
// Call site in CityAgentUISystem (main thread) — fire and forget:
_ = m_NarrativeMemory.SaveChatSessionAsync(markdown);

// In NarrativeMemorySystem — async method:
public async Task SaveChatSessionAsync(string transcriptMarkdown)
{
    if (!m_Initialized) return;
    try
    {
        string histDir = Path.Combine(m_CityDir, "chat-history");
        Directory.CreateDirectory(histDir);
        string filename = $"session-{m_SessionNumber:D3}.md";
        string path = Path.Combine(histDir, filename);
        await File.WriteAllTextAsync(path, transcriptMarkdown).ConfigureAwait(false);
        Mod.Log.Info($"[NarrativeMemorySystem] Saved chat session: {filename}");
        await PruneChatHistoryAsync(histDir).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Mod.Log.Error($"[NarrativeMemorySystem] SaveChatSession failed: {ex.Message}");
        // Swallow — non-fatal per D-12
    }
}
```

Key points:
- `_ = methodAsync()` discards the Task — fire-and-forget idiom
- `ConfigureAwait(false)` prevents capture of Unity's synchronization context (Unity Mono may not have one, but defensive use is correct)
- Exceptions caught inside the async method (not at call site) so they're logged rather than becoming unobserved
- `File.WriteAllTextAsync` and `File.ReadAllTextAsync` are available in .NET Standard 2.1

**Availability:** `File.WriteAllTextAsync` is available in .NET Standard 2.1. Confirmed via .NET Standard 2.1 API surface docs.

### Pattern 10: Screenshot Encoding on Background Thread

Move `File.ReadAllBytes` + `Convert.ToBase64String` out of `OnUpdate`:

```csharp
// In OnUpdate, when screenshot file is detected:
else if (File.Exists(m_ScreenshotPath))
{
    string screenshotPath = m_ScreenshotPath;
    _ = Task.Run(() =>
    {
        try
        {
            byte[] png = File.ReadAllBytes(screenshotPath);
            File.Delete(screenshotPath);
            string base64 = Convert.ToBase64String(png);
            // Must get back to main thread to update bindings:
            // Store in a volatile field that OnUpdate drains
            m_PendingBase64Image = base64;
            m_ScreenshotReady = true;  // volatile flag
        }
        catch (Exception ex)
        {
            Mod.Log.Error($"[CityAgentUISystem] Screenshot encode failed: {ex.Message}");
        }
    });
    m_ScreenshotWaitFrames = -1;
}
```

Then `OnUpdate` checks `m_ScreenshotReady` (volatile bool) and reads `m_PendingBase64Image` on the next frame, then calls `m_HasScreenshot.Update(true)`.

### Pattern 11: CS2 Settings — Read-Only Status Label

The CS2 ModSetting system supports read-only display fields via plain getter-only string properties:

```csharp
// A string property with only a getter renders as read-only text in the settings UI:
[SettingsUISection(kSection, kClaudeGroup)]
public string ActiveProviderStatus => m_IsUsingOllama ? "Currently using: Ollama Fallback" : "Currently using: Claude API";
```

No `SettingsUITextInput` attribute — the absence of an input attribute on a string property makes it display-only. The `LocaleEN` class must include entries for the label and description of this property.

Source: [cs2.paradoxwikis.com/Options_UI](https://cs2.paradoxwikis.com/Options_UI) — HIGH confidence.

### Pattern 12: Settings Groups for Two Providers

The current `kGeneralGroup` must be replaced by two new groups. The `SettingsUIGroupOrder` and `SettingsUIShowGroupName` class attributes must be updated:

```csharp
[SettingsUIGroupOrder(kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup)]
[SettingsUIShowGroupName(kClaudeGroup, kOllamaGroup, kUIGroup, kMemoryGroup)]
public class Setting : ModSetting
{
    public const string kSection = "Main";
    public const string kClaudeGroup  = "Claude API";
    public const string kOllamaGroup  = "Ollama Fallback (optional)";
    public const string kUIGroup      = "UI";
    public const string kMemoryGroup  = "Memory";
    ...
}
```

All `LocaleEN` entries for removed fields (`OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl`) must be replaced with entries for new fields (`ClaudeApiKey`, `ClaudeModel`, `OllamaFallbackBaseUrl`, `OllamaFallbackModel`, `OllamaFallbackApiKey`). The `GetOptionGroupLocaleID` call is needed for the two new group names.

### Anti-Patterns to Avoid

- **Never add `role: "system"` to the messages array for Claude API.** The system prompt is `"system": "..."` at the top level of the request body. Putting it in messages will cause a 400 error.
- **Never use `role: "tool"` for Anthropic.** Tool results must be `role: "user"` with `type: "tool_result"` content blocks. The `role: "tool"` pattern is Ollama/OpenAI only.
- **Never put the `tool_use` block's content array partially in the history.** The entire assistant `content` array (text + tool_use blocks) must be appended as the assistant message before the tool_result user message.
- **Never call `File.WriteAllTextAsync` on the game's main thread without `await`.** Calling it synchronously still blocks; the whole point is `await`ing it on a background task.
- **Never combine `volatile` with `Interlocked.Exchange` on the same field.** `Interlocked` provides its own memory barriers; `volatile` is redundant and can mask intent.
- **Do not wrap `GetAlwaysInjectedContext()` in `Task.Run` — it is already called from within `RunRequestAsync` which runs on the thread pool.** Only the call sites in `CityAgentUISystem.OnUpdate` (main thread) need async dispatch.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Atomic read+clear of string field | Custom lock | `Interlocked.Exchange(ref field, null)` | Single CPU instruction; no deadlock risk |
| Async file I/O | Thread.Sleep polling | `await File.WriteAllTextAsync(...)` | Built into .NET Standard 2.1; handles OS async I/O |
| Fire-and-forget with error swallow | Try/catch wrapping Task.Wait | `_ = MethodAsync()` with internal try/catch | Standard idiom; no blocking |
| JSON construction for API payload | Custom string builder | `JObject`/`JArray` (Newtonsoft) | Already used; handles escaping; already in project |

---

## Common Pitfalls

### Pitfall 1: system prompt as a message entry
**What goes wrong:** Request returns HTTP 400 "messages: roles must alternate between user and assistant" or similar.
**Why it happens:** The current Ollama code adds `{role:"system", content:sysPrompt}` to the messages list. Anthropic rejects system as a message role.
**How to avoid:** Remove system from messages. Place it as `["system"] = sysPrompt` at the top level of the JObject request body.
**Warning signs:** HTTP 400 with message about invalid roles.

### Pitfall 2: Forgetting tool_use_id in tool_result
**What goes wrong:** HTTP 400 "tool_use ids were found without tool_result blocks immediately after" or tool loop never terminates.
**Why it happens:** Current Ollama code doesn't track tool call IDs. Anthropic requires `tool_use_id` to match the `id` from the `tool_use` block.
**How to avoid:** Extract `id` from each `tool_use` block in the response content array. Include it verbatim in `tool_result.tool_use_id`.
**Warning signs:** 400 errors referencing tool_use ids.

### Pitfall 3: Appending only tool_use block instead of full content array
**What goes wrong:** Claude complains about malformed history or the conversation loses context.
**Why it happens:** The current Ollama loop appends `message!` (the message object). In Anthropic format, the assistant's message has a `content` array that may contain both text and tool_use blocks. The full content array must be appended.
**How to avoid:** Extract `responseJson["content"]` as JArray and append the full array as the assistant message content.

### Pitfall 4: max_tokens not set
**What goes wrong:** HTTP 400 "max_tokens is required" from Anthropic.
**Why it happens:** Ollama doesn't require max_tokens. Anthropic does.
**How to avoid:** Always include `["max_tokens"] = 4096` (or configurable value) in every Anthropic request.
**Warning signs:** Immediate 400 on first request.

### Pitfall 5: m_RequestInFlight not volatile — double request fires
**What goes wrong:** Two concurrent requests fire, both write to `PendingResult`, one gets silently dropped.
**Why it happens:** Main thread reads a stale `false` value for `m_RequestInFlight` because the thread pool's write has not been flushed to main memory.
**How to avoid:** Add `volatile` keyword. The main thread always reads the memory-visible value.

### Pitfall 6: Unobserved task exception crashes Unity Mono
**What goes wrong:** Fire-and-forget task throws, exception is unobserved, Unity Mono may terminate the process on unhandled task exceptions (behavior depends on runtime configuration).
**Why it happens:** `_ = MethodAsync()` discards the Task but exceptions thrown after the first `await` become unobserved if not caught inside the method.
**How to avoid:** Wrap the entire body of every fire-and-forget async method in `try/catch(Exception ex)` with `Mod.Log.Error`. Never let an exception escape the async method.

### Pitfall 7: GetAlwaysInjectedContext reading large files synchronously in RunRequestAsync
**What goes wrong:** The thread pool thread blocks on disk I/O before sending the API request, adding latency.
**Why it happens:** `GetAlwaysInjectedContext()` calls `File.ReadAllText` synchronously. This is acceptable on the thread pool (not main thread) but adds latency.
**How to avoid (Phase 1):** D-11 makes memory methods async; update `GetAlwaysInjectedContext` to `GetAlwaysInjectedContextAsync` returning `Task<string>` so it can be `await`ed in `RunRequestAsync`.

### Pitfall 8: Settings field deletion breaks saved settings deserialization
**What goes wrong:** Players upgrading from the old version may see errors if saved settings reference deleted fields.
**Why it happens:** CS2 mod settings are persisted to a file; deleted properties that existed in saved data are silently ignored by most deserializers, but it's worth noting.
**How to avoid:** CS2's `ModSetting` serialization is robust to missing fields — this is low risk. Just delete the fields cleanly and update `SetDefaults()`.

---

## Code Examples

### Full Anthropic /v1/messages Request (C# / Newtonsoft.Json)

```csharp
// Source: Verified against platform.claude.com/docs/en/api/overview and /build-with-claude/vision

string apiKey    = (setting.ClaudeApiKey ?? "").Trim();
string model     = (setting.ClaudeModel ?? "claude-sonnet-4-6").Trim();
string sysPrompt = setting.SystemPrompt ?? "";

if (m_NarrativeMemory.IsInitialized)
    sysPrompt += await m_NarrativeMemory.GetAlwaysInjectedContextAsync().ConfigureAwait(false);

// Build user content blocks
var userContent = new JArray();
if (!string.IsNullOrEmpty(base64Png))
{
    userContent.Add(new JObject
    {
        ["type"] = "image",
        ["source"] = new JObject
        {
            ["type"]       = "base64",
            ["media_type"] = "image/png",
            ["data"]       = base64Png
        }
    });
}
userContent.Add(new JObject { ["type"] = "text", ["text"] = userMessage });

var messages = new JArray
{
    new JObject { ["role"] = "user", ["content"] = userContent }
};

var toolsArray = JArray.Parse(m_ToolRegistry.GetToolsJson());   // Anthropic format
string endpoint = "https://api.anthropic.com/v1/messages";

// Loop
for (int iteration = 0; iteration < 10; iteration++)
{
    var requestBody = new JObject
    {
        ["model"]      = model,
        ["max_tokens"] = 4096,
        ["system"]     = sysPrompt,          // Top-level, NOT in messages
        ["messages"]   = messages,
        ["tools"]      = toolsArray,
        ["stream"]     = false
    };

    var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
        Content = new StringContent(requestBody.ToString(Formatting.None), Encoding.UTF8, "application/json")
    };
    request.Headers.Add("x-api-key", apiKey);
    request.Headers.Add("anthropic-version", "2023-06-01");

    HttpResponseMessage response = await s_Http.SendAsync(request).ConfigureAwait(false);
    string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

    if ((int)response.StatusCode == 429)
    {
        // D-06/D-07/D-08: trigger Ollama fallback or surface error
        return await RunOllamaFallbackAsync(userMessage, base64Png, sysPrompt, messages).ConfigureAwait(false);
    }

    if (!response.IsSuccessStatusCode)
    {
        return $"[Error]: HTTP {(int)response.StatusCode} — {responseBody}";
    }

    var responseJson = JObject.Parse(responseBody);
    string stopReason = responseJson["stop_reason"]?.Value<string>() ?? "";
    var contentArray  = responseJson["content"] as JArray ?? new JArray();

    if (stopReason == "tool_use")
    {
        // Append full assistant content array to history
        messages.Add(new JObject { ["role"] = "assistant", ["content"] = contentArray });

        // Collect all tool_result blocks into one user message
        var toolResults = new JArray();
        foreach (var block in contentArray)
        {
            if (block["type"]?.Value<string>() != "tool_use") continue;

            string toolUseId  = block["id"]?.Value<string>() ?? "";
            string toolName   = block["name"]?.Value<string>() ?? "";
            var    toolInput  = block["input"] as JObject ?? new JObject();
            string toolArgs   = toolInput.ToString(Formatting.None);

            string toolResult = m_ToolRegistry.Dispatch(toolName, toolArgs);
            toolResults.Add(new JObject
            {
                ["type"]        = "tool_result",
                ["tool_use_id"] = toolUseId,
                ["content"]     = toolResult
            });
        }

        messages.Add(new JObject { ["role"] = "user", ["content"] = toolResults });
    }
    else // stop_reason == "end_turn"
    {
        var textBlock = contentArray.FirstOrDefault(b => b["type"]?.Value<string>() == "text");
        return textBlock?["text"]?.Value<string>() ?? "[Error]: Empty response content.";
    }
}

return "[Error]: Tool call loop exceeded maximum iterations.";
```

### Anthropic API Headers (verified)

```
x-api-key: sk-ant-...        (required; no "Bearer" prefix)
anthropic-version: 2023-06-01 (required; this is the current stable version as of 2026)
content-type: application/json
```

Source: All `curl` examples in official Anthropic docs use these exact headers. HIGH confidence.

### Interlocked.Exchange Drain Pattern

```csharp
// In CityAgentUISystem.OnUpdate:
string? result = Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null);
if (result != null)
{
    m_History.Add(new ChatMessage { role = "assistant", content = result });
    PushMessagesBinding();
    m_IsLoading.Update(false);
    _ = m_NarrativeMemory.SaveChatSessionAsync(/* ... */);  // fire-and-forget
}
```

### Ollama Fallback Branch (OpenAI-compatible)

```csharp
// POST {OllamaBaseUrl}/v1/chat/completions
// Uses GetToolsJsonOpenAI() tool format
// system as messages[0] with role:"system"
// images NOT supported in the same way — Ollama /v1 multimodal varies by model

// Response parsing:
string stopReason = responseJson["choices"]?[0]?["finish_reason"]?.Value<string>() ?? "";
var message = responseJson["choices"]?[0]?["message"] as JObject;
var toolCalls = message?["tool_calls"] as JArray;

if (stopReason == "tool_calls" && toolCalls != null)
{
    foreach (var tc in toolCalls)
    {
        string id       = tc["id"]?.Value<string>() ?? "";
        string funcName = tc["function"]?["name"]?.Value<string>() ?? "";
        string funcArgs = tc["function"]?["arguments"]?.Value<string>() ?? "{}"; // string, not object

        string toolResult = m_ToolRegistry.Dispatch(funcName, funcArgs);

        // tool result for Ollama OpenAI format:
        messages.Add(new JObject
        {
            ["role"]         = "tool",
            ["tool_call_id"] = id,
            ["content"]      = toolResult
        });
    }
}
```

---

## State of the Art

| Old Approach (current code) | New Approach (Phase 1) | Impact |
|-----------------------------|-----------------------|--------|
| Ollama `/api/chat` format | Anthropic `/v1/messages` | Enables actual Claude usage; tools and vision now work |
| `images: [base64]` array | `content: [{type:"image","source":{...}}]` block | Required for Anthropic; Ollama vision also uses content blocks in OpenAI compat mode |
| `role: "system"` in messages | `"system": "..."` top-level field | Required by Anthropic API; prevents 400 errors |
| `role: "tool"` results | `role: "user"` with `type: "tool_result"` blocks | Required by Anthropic tool loop |
| `volatile string? PendingResult` | `Interlocked.Exchange` | Eliminates read-then-null race condition |
| `bool m_RequestInFlight` (no volatile) | `volatile bool m_RequestInFlight` | Prevents double-request race |
| Sync `File.WriteAllText` on main thread | `await File.WriteAllTextAsync` fire-and-forget | Eliminates main-thread I/O stalls |
| `File.ReadAllBytes` + `Convert.ToBase64String` in `OnUpdate` | `Task.Run(...)` with volatile flag | Moves 4-5 MB operation off game thread |
| `OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl` settings | `ClaudeApiKey`, `ClaudeModel` + `OllamaFallbackBaseUrl`, `OllamaFallbackModel` | Reflects actual provider roles |

---

## Open Questions

1. **max_tokens value for Claude requests**
   - What we know: Anthropic requires `max_tokens`. `claude-sonnet-4-6` supports up to 64K output tokens.
   - What's unclear: What's the right default? Too low truncates long narrative responses; too high increases cost.
   - Recommendation: Use `4096` as default. Add it as a settings field if needed in a later phase. 4096 is safe for narrative + tool calls without being wasteful.

2. **Ollama image format via /v1/chat/completions**
   - What we know: Ollama supports vision via its native `/api/chat` with `images[]`. The OpenAI-compat `/v1/chat/completions` may also support image content blocks for vision models.
   - What's unclear: Whether Ollama's `/v1/chat/completions` correctly handles `{"type":"image","source":{"type":"base64",...}}` content blocks for the fallback.
   - Recommendation: For Phase 1, the Ollama fallback is triggered by 429 rate-limits — typically text-only retries. Omit image from the Ollama fallback call (the image was already part of the failed Claude call). Document this limitation.

3. **`File.WriteAllTextAsync` availability in Unity Mono**
   - What we know: `File.WriteAllTextAsync` is part of .NET Standard 2.1. The project targets `netstandard2.1`. Unity Mono supports .NET Standard 2.1.
   - What's unclear: Whether the specific Mono version shipped with CS2 (Unity 2022.3.7f1) has the async File I/O methods.
   - Recommendation: Use `Task.Run(() => File.WriteAllText(...))` as a safe fallback if `WriteAllTextAsync` is unavailable. The Task.Run approach offloads synchronous I/O to the thread pool without relying on OS-level async. Test which works in the actual game at the start of Phase 1.

4. **Active provider status field polling**
   - What we know: CS2 settings properties are read when the settings page is displayed.
   - What's unclear: Whether a getter-only property in settings is re-evaluated every time the settings panel is opened or is cached.
   - Recommendation: Implement as a getter-only string property. CS2 settings panels typically re-read all property values when opened. This is polling (on demand) rather than event-driven, which is sufficient.

---

## Environment Availability

| Dependency | Required By | Available | Version | Notes |
|------------|------------|-----------|---------|-------|
| .NET SDK | `dotnet build` | Yes | 10.0.201 | Present on dev machine |
| Node.js | UI build | Yes | v24.13.0 | Present on dev machine |
| Cities: Skylines 2 | In-game validation (CORE-03) | Assumed | Unknown | Must be installed for end-to-end test |
| Anthropic API key | API-01 | Assumed | — | Developer must have key in settings |
| Ollama (local) | API-02, API-03 fallback test | Unknown | — | Optional; required only to test 429 fallback path |

**Missing dependencies with no fallback:**
- Anthropic API key: required for CORE-03 (end-to-end test). Cannot mock without code changes.

**Missing dependencies with fallback:**
- Ollama: 429 fallback path can be tested by temporarily returning a fake 429 from code.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | No automated test suite exists in this project |
| Config file | None — no test project found |
| Quick run command | `cd src && dotnet build -c Release` (build validation only) |
| Full suite command | Manual in-game validation: build → deploy → launch CS2 → exercise panel |

No automated unit test infrastructure exists (confirmed by codebase scan — no `.Tests.csproj`, no test directories). All validation for this phase is manual in-game.

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated? | How to Verify |
|--------|----------|-----------|-----------|---------------|
| CORE-01 | No UI freeze on file write or screenshot encode | Manual | No | Send a message with screenshot; game should not stutter |
| CORE-02 | No duplicate requests on rapid send | Manual | No | Click send rapidly twice; only one API call fires |
| CORE-03 | Full pipeline: screenshot + tool calls + memory + response | Manual in-game | No | Send screenshot + message; verify tool calls, memory write, response displayed |
| API-01 | Claude /v1/messages format correct | Manual (log inspection) | No | Check CS2 log for HTTP 200 from api.anthropic.com |
| API-02 | Ollama fallback selectable in settings | Manual | No | Open settings; confirm Ollama fields present |
| API-03 | 429 triggers Ollama fallback notice + retry | Manual (simulate) | No | Temporarily force a 429 response in code; verify in-panel notice appears |
| API-04 | Model change takes effect without restart | Manual | No | Change Claude model in settings; send message; check log for new model name |

### Wave 0 Gaps

No test framework to create. Validation is entirely manual in-game.

---

## Project Constraints (from CLAUDE.md)

| Constraint | Implication for Phase 1 |
|------------|------------------------|
| C# .NET Standard 2.1; no deviation from tech stack | All async patterns must be .NET Standard 2.1 compatible |
| All HTTP calls must be async/non-blocking | `RunRequestAsync` stays async; fire-and-forget for memory writes |
| State crosses C#↔JS bridge as JSON strings via `ValueBinding` | In-panel system notice for 429 appended to `m_History` as a `"system"` role message; serialized normally |
| API key never hardcoded, never logged | New `ClaudeApiKey` field masked in logs (first 4 + last 4 chars) |
| Keep C# thin — no business logic that could live elsewhere | Provider routing logic stays in `ClaudeAPISystem`; no new abstraction layers |
| `m_` prefix for private instance fields; `s_` for private static | New fields in Settings.cs and ClaudeAPISystem follow convention |
| XML doc comments on public members | New public methods on `NarrativeMemorySystem` need `/// <summary>` comments |
| Error presentation: `[Error]: ...` written to `PendingResult` | Non-429 Claude errors follow existing pattern |
| Close CS2 before building — game locks the DLL | Reminder for the executor; part of deploy checklist |
| `SetDefaults()` must be updated when settings fields change | `SetDefaults()` must be updated to reflect deleted + new fields |

---

## Sources

### Primary (HIGH confidence)
- [platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls](https://platform.claude.com/docs/en/agents-and-tools/tool-use/handle-tool-calls) — tool_use response format, tool_result message format, tool_use_id requirement, ordering constraints
- [platform.claude.com/docs/en/build-with-claude/vision](https://platform.claude.com/docs/en/build-with-claude/vision) — base64 image content block format, media_type values, max image size
- [platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use](https://platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use) — tool definition format with input_schema
- [learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.exchange](https://learn.microsoft.com/en-us/dotnet/api/system.threading.interlocked.exchange) — Interlocked.Exchange semantics and memory barriers
- [cs2.paradoxwikis.com/Options_UI](https://cs2.paradoxwikis.com/Options_UI) — CS2 SettingsUI attributes, getter-only read-only fields, group ordering

### Secondary (MEDIUM confidence)
- [docs.ollama.com/api/openai-compatibility](https://docs.ollama.com/api/openai-compatibility) — Ollama OpenAI-compat endpoint feature list; tool_calls listed as supported; tool_choice not supported
- [ollama.com/blog/tool-support](https://ollama.com/blog/tool-support) — Ollama tool_calls response uses role:"tool" for results; arguments is a string

### Tertiary (LOW confidence)
- General WebSearch results on fire-and-forget async patterns — consistent with MSDN; treated as corroboration only

---

## Metadata

**Confidence breakdown:**
- Anthropic API format: HIGH — verified directly from official docs
- Tool-use loop structure: HIGH — verified from official implementation guide
- Thread safety (Interlocked.Exchange, volatile): HIGH — verified from MSDN
- CS2 settings attributes: HIGH — verified from official CS2 modding wiki
- Ollama fallback format: MEDIUM — official docs confirm tool_calls and tool role, but full JSON schema not shown explicitly
- File.WriteAllTextAsync in Unity Mono: MEDIUM — .NET Standard 2.1 API surface confirmed; Unity Mono compatibility is assumed

**Research date:** 2026-03-26
**Valid until:** 2026-06-26 (Anthropic API changes infrequently at this level; CS2 modding API stable)
