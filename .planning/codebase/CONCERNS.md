# CONCERNS
_Generated: 2026-03-26_

## Summary

CityAgent is a functional mod with core systems in place, but several meaningful risks exist across security, threading, performance, and feature completeness. The most critical concerns are: AI-generated HTML rendered via `dangerouslySetInnerHTML` without sanitization, a volatile-only thread synchronization model that has no locking, and synchronous file I/O on the game thread during every API request. Several planned data tools (budget, traffic, zone cell counts) are missing, and the API integration is wired to Ollama rather than the Claude API referenced in project docs.

---

## Security Concerns

**AI-generated HTML injected without sanitization:**
- File: `UI/src/components/CityAgentPanel.tsx` line 324
- Assistant messages are passed through `renderMarkdown()` and then inserted via `dangerouslySetInnerHTML`. The markdown renderer produces HTML from AI response text. If the LLM produces unexpected markup (e.g., `<script>`, event handlers, data URIs), it is injected directly into the Coherent GT DOM.
- Coherent GT may or may not enforce Content Security Policy restrictions. If it does not, this is an XSS vector. At minimum, the renderer should strip or escape `<script>`, `on*=` attributes, and `javascript:` href values before output.
- The `escapeHtml()` helper in `renderMarkdown.ts` is used for code blocks only; it is not applied globally before the inline pass runs.

**API key stored in CS2 mod settings (plaintext):**
- File: `src/Settings.cs` — `OllamaApiKey` property
- The API key is stored wherever CS2 persists mod settings (typically `%AppData%\...\Cities Skylines II\ModsData\`), not in a secrets manager. This is the expected CS2 pattern but means the key sits in a user-readable location. No masking occurs in the UI beyond the log output.
- Risk is low for personal use but would be a concern for any distributed build.

**Markdown renderer produces unescaped link URLs:**
- File: `UI/src/utils/renderMarkdown.ts` line 210
- The link pattern `\[([^\]]+)\]\(([^)]+)\)` substitutes the URL directly into `href="$2"` without sanitization. A `javascript:` URL in AI output would be inserted verbatim.

---

## Performance Concerns

**Synchronous disk I/O on the game's main thread (and async task thread) every API call:**
- File: `src/Systems/NarrativeMemorySystem.cs` — `GetAlwaysInjectedContext()` (lines 461–501)
- Called from `ClaudeAPISystem.RunRequestAsync` (line 95) which runs on the thread pool, so the disk read does not block the game's main thread directly. However, all other `NarrativeMemorySystem` methods (`WriteFile`, `AppendToLog`, `SaveChatSession`, `EnsureDirectoryStructure`) are called from the game's main thread in `CityAgentUISystem.OnUpdate`.
- `PersistChatSession()` is called after every user message and after every API response — potentially multiple times per conversation turn. For large chat histories this serializes and writes the entire markdown transcript on the main thread.
- `AppendToLog` reads the entire `narrative-log.md` into memory, rebuilds it, and writes it back on each call. For large log files this becomes O(n) on the main thread.

**Full screenshot PNG loaded into memory as base64 on the main thread:**
- File: `src/Systems/CityAgentUISystem.cs` lines 133–135
- `File.ReadAllBytes(m_ScreenshotPath)` and `Convert.ToBase64String(png)` run in `OnUpdate` (main thread). A full-resolution CS2 screenshot can be several megabytes. The base64 string then lives in `m_PendingBase64Image` until the user sends a message.
- Base64 encoding of a 4MB PNG produces ~5.3MB string held in managed memory until GC.

**Chat history serialized to JSON on every frame that has a pending result:**
- File: `src/Systems/CityAgentUISystem.cs` — `PushMessagesBinding()` line 240
- `JsonConvert.SerializeObject(m_History)` is called after every message and after every API response. The full history is always reserialized. For long sessions this grows without bound.

**Frame-by-frame polling for screenshot file existence:**
- File: `src/Systems/CityAgentUISystem.cs` lines 121–145
- `File.Exists(m_ScreenshotPath)` is called every frame for up to ~10 frames after a screenshot is queued. File system calls from a game update loop are generally safe at low frequency but add latency jitter.

**Settings re-read from `Mod.ActiveSetting` every 60 frames:**
- File: `src/Systems/CityAgentUISystem.cs` lines 159–169
- Low cost individually but represents a polling pattern; settings changes could instead be pushed via a CS2 settings-change callback if the API supports it.

---

## Technical Debt

**Thread synchronization via `volatile` field only — no memory barrier for compound reads:**
- File: `src/Systems/ClaudeAPISystem.cs` line 22 (`public volatile string? PendingResult`)
- The async task writes `PendingResult`; the main thread reads and nulls it. `volatile` guarantees visibility of the reference itself but does not provide atomicity for the read-then-null sequence in `CityAgentUISystem.OnUpdate` lines 148–155. A race where `PendingResult` is set between the read and the null assignment would silently drop the result. The window is tiny but non-zero. A proper fix would use `Interlocked.Exchange`.

**`m_RequestInFlight` flag not thread-safe:**
- File: `src/Systems/ClaudeAPISystem.cs` lines 23, 71, 206
- `m_RequestInFlight` is a plain `bool` field. It is set to `true` on the main thread (in `BeginRequest`) and set to `false` in the `finally` block of the async task (thread pool). Without `volatile` or `Interlocked`, the main thread may cache a stale value and allow a second concurrent request to fire.

**`GetToolsJson()` (Claude format) is never used — only `GetToolsJsonOpenAI()` is called:**
- File: `src/Systems/CityToolRegistry.cs` lines 29–46; `src/Systems/ClaudeAPISystem.cs` line 119
- `GetToolsJson()` produces a Claude API-format tools array, but `RunRequestAsync` calls `GetToolsJsonOpenAI()`. The project name and CLAUDE.md describe Claude API usage, but the implementation targets Ollama's OpenAI-compatible format. If the project migrates to the real Claude API, the tool format, image format (content blocks vs `images[]`), and endpoint will all need to change.

**Class naming mismatch — "Claude" vs "Ollama":**
- Files: `src/Systems/ClaudeAPISystem.cs`, `src/Settings.cs`
- The system is named `ClaudeAPISystem` but connects to Ollama (`/api/chat`, `images[]` array). Settings properties are named `OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl`. The naming is inconsistent and will confuse future contributors or a migration effort.

**Markdown renderer is hand-rolled to work around Coherent GT limitations:**
- File: `UI/src/utils/renderMarkdown.ts`
- ~350 lines of custom parser code. Any markdown edge case (nested lists, multi-paragraph list items, inline HTML, escaped characters) not explicitly handled will render incorrectly or be silently skipped. Maintenance burden grows as AI response formatting evolves.

**Session number format caps at 999 (`D3` format):**
- File: `src/Systems/NarrativeMemorySystem.cs` line 513
- `$"session-{m_SessionNumber:D3}.md"` — session 1000 becomes `session-1000.md`, which sorts lexicographically after `session-999.md`. The `LoadLatestChatSession` sort (`OrderByDescending`) relies on string sort order, so sessions above 999 will sort incorrectly relative to lower-numbered sessions (1000 < 999 lexicographically).

**Log archive rotation has a subtle bug — `RotateNarrativeLog` is called after writing the file, not before:**
- File: `src/Systems/NarrativeMemorySystem.cs` lines 390–392
- `AppendToLog` writes the updated file, then checks `if (entryCount > m_MaxNarrativeLogEntries)`. The rotation reads the file it just wrote, which is correct, but if rotation itself fails the main file remains over-limit indefinitely (the error is logged and swallowed).

---

## Incomplete Implementations

**Zone cell counts not implemented — demand indices used as proxy:**
- File: `src/Systems/Tools/GetZoningSummaryTool.cs` (description field and `note` in response)
- The tool description and response explicitly state "Direct zone cell counts not yet implemented." The AI receives demand pressure (0–100) rather than actual zoned area breakdown, which limits the quality of zoning advice.

**Budget data tool missing:**
- Planned in CLAUDE.md Phase 2 and Phase 5; no `GetBudgetTool.cs` file exists. The AI has no access to city finances, which is a significant gap for an advisor focused on city-building decisions.

**Traffic data tool missing:**
- Planned in CLAUDE.md Phase 5; no traffic tool file exists. Traffic is one of CS2's most complex systems and a frequent source of city problems; its absence limits advisory quality.

**`CityAdvisorButton.tsx` does not exist as a separate file:**
- Planned in CLAUDE.md project structure. The toggle button is inlined in `CityAgentPanel.tsx` line 292–293. Not a blocking issue but means the component structure diverges from documentation.

**`ChatMessage.tsx` does not exist as a separate file:**
- Planned in CLAUDE.md project structure. Message rendering is inlined in `CityAgentPanel.tsx`. Same issue as above.

**`moduleRegistry.append()` approach abandoned with no documented replacement:**
- File: `UI/src/index.tsx` (comment lines 6–8)
- The comment notes `moduleRegistry.append("Game.MainScreen", ...)` does not work in CS2 v1.5.5. The current workaround appends a raw `div` to `document.body`. This bypasses CS2's UI layering system, which may cause z-index conflicts with other mods or game overlays.

**Streaming disabled:**
- File: `src/Systems/ClaudeAPISystem.cs` line 129 (`"stream": false`)
- Hardcoded to non-streaming. The user sees nothing until the entire tool-use loop completes and the final response is received. For long conversations with multiple tool calls, this can take many seconds with no intermediate feedback beyond the loading dots.

---

## Hardcoded Values / Config Issues

**Default model set to `kimi-k2.5:cloud` (a specific cloud model):**
- File: `src/Settings.cs` lines 34, 71
- The default model is not Claude (despite the project name) and is a specific third-party cloud model. New users will need to change this before the mod works with any local Ollama instance or the Claude API. No validation prevents sending requests to an unavailable model.

**Default base URL set to `https://ollama.com` (the Ollama homepage, not an API endpoint):**
- File: `src/Settings.cs` lines 38, 72
- `https://ollama.com` is the Ollama marketing site, not an API endpoint. The correct local default would be `http://localhost:11434`. Out-of-the-box, the mod will receive HTTP errors from the Ollama homepage. New users must change this manually.

**Screenshot key hardcoded to F8 as default:**
- File: `src/Settings.cs` line 46 / `src/Systems/CityAgentUISystem.cs` line 30
- F8 is configurable but F8 may conflict with other CS2 mods. Low severity.

**10-frame screenshot polling timeout hardcoded:**
- File: `src/Systems/CityAgentUISystem.cs` line 124 (`> 10`)
- Frame count for the screenshot timeout is not configurable. On low-fps systems, 10 frames may be insufficient for Unity to write the file.

**Tool loop capped at 10 iterations, hardcoded:**
- File: `src/Systems/ClaudeAPISystem.cs` line 122 (`iteration < 10`)
- Not configurable. A complex conversation with many memory reads/writes could theoretically hit this limit.

**Twemoji CDN URL hardcoded in renderer:**
- File: `UI/src/utils/renderMarkdown.ts` line 14
- `var TWEMOJI_BASE = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/"` — requires internet access during gameplay. If the CDN is unavailable, all emoji silently fail to render (graceful failure via failed image load, but still an external dependency in a game mod context).

**Settings polling interval hardcoded to 60 frames:**
- File: `src/Systems/CityAgentUISystem.cs` line 159 (`>= 60`)
- Assumes ~60fps. At 30fps this polls every 2 seconds; at 120fps every 0.5 seconds.

---

## Architectural Risks

**`Mod.ActiveSetting` is a static mutable field — global state:**
- File: `src/Mod.cs` line 23, `src/Systems/ClaudeAPISystem.cs` line 81
- All systems read `Mod.ActiveSetting` directly. If CS2 ever calls `OnDispose` on one mod while another thread is mid-request, `ActiveSetting` is set to `null` (line 83) and `RunRequestAsync` would NullReferenceException (line 88 does null-check, but the window between lines 81 and 88 is unguarded if the task is mid-execution).

**`NarrativeMemorySystem` file writes occur on the main thread — no async I/O:**
- Files: `src/Systems/NarrativeMemorySystem.cs`, `src/Systems/CityAgentUISystem.cs`
- `PersistChatSession()` and `SaveChatSession()` are called from `OnUpdate` (main thread) without async or background thread dispatch. Large chat histories or slow disk will stall the frame.

**Memory system initialization deferred to `OnUpdate` with a flag — fragile startup sequence:**
- File: `src/Systems/CityAgentUISystem.cs` lines 83–113
- Memory initialization runs on the first `OnUpdate` call because the city name may not be available in `OnCreate`. If initialization fails, the flag is still set true and memory features are silently disabled. There is no retry mechanism and no user-visible notification of the failure.

**Single `HttpClient` instance shared across all requests (static field):**
- File: `src/Systems/ClaudeAPISystem.cs` line 16 (`private static readonly HttpClient s_Http`)
- Correct pattern for `HttpClient` reuse. However, if the base URL or auth header changes (user updates settings), the existing client still uses the old headers since headers are set per-request. The auth header is set per-request (line 141–142), so this is actually fine for auth — but connection pooling may hold stale connections to the old base URL. Low risk.

**Chat history restored from disk restores only the last session, not the full multi-session history:**
- File: `src/Systems/CityAgentUISystem.cs` lines 91–103
- `LoadLatestChatSession()` returns only the most recent `session-NNN.md`. On game restart, the UI shows only the previous session's messages. The AI receives the always-injected memory context but not prior session transcripts. This means the AI's tool-use loop starts fresh each game load (it must re-read memory files explicitly).

**No validation that the tool `InputSchema` JSON is well-formed:**
- File: `src/Systems/CityToolRegistry.cs` lines 41, 64
- `tool.InputSchema` is interpolated directly into JSON strings without parsing or validation. A malformed schema would produce invalid JSON sent to the API.

---

## Gaps / Unknowns

- `GetWorkforceTool.cs` follows the same pattern as other data tools (reads `CityDataSystem.TotalEmployed`/`TotalWorkplaces`) but was not fully audited.
- The CS2 `CityConfigurationSystem.cityName` property used in `NarrativeMemorySystem.ResolveCityName()` (`src/Systems/NarrativeMemorySystem.cs` line 133) is accessed via reflection-like direct property access; if CS2 changes the API in a game update, city name resolution silently falls back to "Unnamed City".
- No test suite exists. Changes to ECS query filters, memory file parsing, or the markdown renderer have no automated regression coverage.
- The `GetToolsJson()` method (Claude API format) in `CityToolRegistry.cs` is dead code — it is never called. If a Claude API migration happens, this method's format should be verified against current Anthropic docs before use.
- The `OnClearChat()` handler calls `PersistChatSession()` before clearing `m_History`, which correctly saves the session. However, `StartNewSession()` increments `m_SessionNumber` — if the player clears chat then immediately closes the game, the next startup will skip a session number. Cosmetic issue only.
- There is no mechanism to cancel an in-flight API request. If the user closes the panel or switches cities while a request is running, `RunRequestAsync` will complete and write to `PendingResult`; the next `OnUpdate` will drain it into whatever state the UI is currently in. If the city has changed, memory file writes from the old request's tool calls will have already been written to the wrong city's directory (or the correct one — depends on timing).
