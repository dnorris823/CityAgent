# Domain Pitfalls

**Project:** CityAgent — CS2 mod + Claude AI advisor
**Researched:** 2026-03-26
**Scope:** CS2 modding, Claude API tool loops, async/threading in game mods, in-game file editor UX, end-to-end testing without a test harness

---

## Critical Pitfalls

Mistakes that cause freezes, crashes, silent data loss, runaway API spend, or require rewrites.

---

### Pitfall 1: Blocking the Game Thread with Disk I/O

**What goes wrong:** File reads and writes called from `OnUpdate` (the game's main thread update loop) stall the entire simulation. The game freezes, frame rate drops to zero, and the player sees a hang. This is not a crash — it is worse, because it is invisible and repeatable.

**Why it happens:** Unity's game loop is single-threaded. Any synchronous I/O in `OnUpdate` holds the frame until the I/O completes. The existing `NarrativeMemorySystem` calls `WriteFile`, `AppendToLog`, `SaveChatSession`, and `EnsureDirectoryStructure` from `CityAgentUISystem.OnUpdate`. `AppendToLog` is particularly dangerous: it reads the entire `narrative-log.md` into memory, rebuilds it, and writes it back — O(n) on every call, on the main thread.

**Consequences:** Noticeable stutters on every save. Freeze on first load (directory structure creation). Worsens as log grows. Hard to diagnose because Unity profiler may not clearly attribute the stall to file I/O.

**Prevention:**
- Move all `NarrativeMemorySystem` file writes to a background `Task` using `Task.Run(() => ...)`.
- Set a flag on the main thread (`m_MemoryWritePending = true`) and do the actual write in the task.
- Never call `File.ReadAllBytes`, `File.WriteAllText`, or `Directory.CreateDirectory` from `OnUpdate`.
- The screenshot base64 encode (`File.ReadAllBytes` + `Convert.ToBase64String`) is also on the main thread and should move to the async task that initiates the API call.

**Warning signs:** Frame time spikes in Unity profiler when chat messages are sent. Visible stutter on first city load.

**Phase:** Claude API migration milestone — fix before adding heartbeat (which would multiply write frequency).

---

### Pitfall 2: Race Condition on `PendingResult` and `m_RequestInFlight`

**What goes wrong:** The async API task writes `PendingResult` on the thread pool; the main thread reads and nulls it in `OnUpdate`. With only a `volatile` field and no lock, a race where the task sets `PendingResult` between the main thread's read and its null-assignment silently drops the API response. The player sees no reply, the system thinks the request completed, and there is no error logged.

**Why it happens:** `volatile` guarantees visibility of the reference itself but not atomicity of a read-then-write sequence. The existing code does `var r = m_Api.PendingResult; m_Api.PendingResult = null;` — two operations, not one.

**Consequences:** Silent dropped responses. Especially likely under load (heartbeat + user-triggered request both completing near-simultaneously).

**Prevention:**
- Replace the `PendingResult` pattern with `Interlocked.Exchange(ref _pendingResult, null)` which atomically reads and nulls in one operation.
- Replace the plain `bool m_RequestInFlight` with `Interlocked.CompareExchange` or a proper lock guard.
- Consider a `ConcurrentQueue<string>` for results if the heartbeat introduces multiple concurrent responses.

**Warning signs:** Intermittent missing responses that cannot be reproduced reliably. More frequent after adding the heartbeat system.

**Phase:** Claude API migration milestone — fix before adding the heartbeat (which increases concurrency).

---

### Pitfall 3: Wrong API Wire Format — Ollama vs. Claude API

**What goes wrong:** The existing `ClaudeAPISystem` sends Ollama's `/api/chat` format with an `images[]` array. The Claude API uses `/v1/messages` with content blocks (`{"type": "image", "source": {"type": "base64", ...}}`). The tool format also differs: Claude uses `{"name": ..., "description": ..., "input_schema": ...}` at the top level, not wrapped in a type field. Sending the wrong format returns HTTP 400 with a schema validation error.

**Why it happens:** The system was scaffolded against Ollama's OpenAI-compatible endpoint. `GetToolsJson()` (Claude format) exists in `CityToolRegistry` but is dead code — `GetToolsJsonOpenAI()` is called instead. The vision image format diverges entirely.

**Consequences:** Every API call fails with 400. Tool calls fail silently or return schema errors. Screenshots are never sent. Migration that changes only the base URL will not work.

**Prevention:**
- The Claude API requires: endpoint `POST /v1/messages`, header `x-api-key`, header `anthropic-version: 2023-06-01`, body structure `{model, max_tokens, system, messages, tools}`.
- Image content block: `{"type": "image", "source": {"type": "base64", "media_type": "image/png", "data": "<base64>"}}`.
- Tool results: all results for one turn must be in a single user message; text before tool_result in the content array causes a 400 error.
- Audit `GetToolsJson()` against current Anthropic docs before enabling it — it is dead code and may be stale.
- Resize screenshots to under 1.15 megapixels (roughly 1092x1092) before encoding to avoid hitting the 5 MB per-image API limit and to reduce token cost (~1,600 tokens per image at that size).

**Warning signs:** HTTP 400 responses on first API call after migration. Error body will describe the specific schema violation.

**Phase:** Claude API migration milestone — this is the primary migration task.

---

### Pitfall 4: Context Window Bloat from Accumulated Tool Calls

**What goes wrong:** Each tool call appends the tool_use block (input) and tool_result block (output) to the message history. Over a long session with many memory reads, city data queries, and web searches, the accumulated history can push the context toward the 200k token limit. This is especially acute for the heartbeat system, which runs tool calls on a timer without user-driven conversation pruning.

**Why it happens:** The Claude API does not discard history between calls — `m_History` grows every turn. Each tool invocation adds at minimum 200–500 tokens (tool_use + tool_result). A 10-iteration tool loop adds up to 5,000 tokens per user message, not counting the image (~1,600 tokens) and system prompt.

**Consequences:** Requests eventually fail with a context window exceeded error. Cost scales with context length on every call (input tokens are billed each time). Heartbeat system could silently accumulate a very large context.

**Prevention:**
- Track token usage from API response `usage` field on every call and log it.
- Implement a hard context limit (e.g., truncate history beyond 150k tokens before building the next request).
- For heartbeat: use a separate, short-lived message history per heartbeat invocation — do not share it with the interactive chat history.
- Summarize and prune old tool_result blocks before they crowd out recent context.
- Avoid injecting the full screenshot on every heartbeat tick — use a lower-resolution capture or skip the image for background checks.

**Warning signs:** API responses getting slower over a long session (larger payload). Response cost increasing monotonically per call.

**Phase:** Claude API migration + heartbeat milestone.

---

### Pitfall 5: Runaway API Cost from Heartbeat Loop

**What goes wrong:** A periodic heartbeat that fires every N minutes, sends a screenshot, triggers tool calls, and generates a response can cost $0.01–0.05 per invocation depending on image size and response length. At 5-minute intervals over an 8-hour play session: 96 invocations. At $0.02 each: ~$2/session. Bugs that cause the heartbeat to fire too frequently (or not back off on failure) can drain API credits rapidly.

**Why it happens:** Game mods do not have natural billing visibility. The cost is invisible until the API key hits its monthly limit or the bill arrives.

**Consequences:** Uncontrolled spend. API rate limits triggered. Key deactivated mid-session.

**Prevention:**
- Make the heartbeat interval configurable in mod settings (default: 10 minutes, not 5).
- Add a per-session heartbeat call counter, log it, expose it in the UI ("14 advisor checks this session").
- Implement exponential backoff if heartbeat API calls fail (network error, 429, 529).
- Add a "pause heartbeat" toggle in the UI so the player can stop background calls.
- Cap heartbeat image resolution aggressively (downscale to 800x450 or skip image entirely for background checks).
- Never trigger a new heartbeat if a request is already in flight.

**Warning signs:** Unbounded API call frequency. No back-off on failure. No user visibility into background call count.

**Phase:** Heartbeat system milestone — design these controls in from the start, not as a retrofit.

---

### Pitfall 6: CS2 DLL Locked by the Running Game

**What goes wrong:** Attempting to run `dotnet build` while CS2 is open fails with `MSB3231: Unable to remove directory — Access to the path '..._win_x86_64.dll' is denied`. The build appears to succeed (no compiler errors) but the DLL is not deployed. The developer loads CS2, tests against the old version, and is confused why changes have no effect.

**Why it happens:** CS2 loads the mod DLL on startup and holds an OS file lock for the process lifetime. The build post-step that copies the DLL to the mod folder cannot overwrite a locked file.

**Consequences:** Testing against stale code. Time lost debugging a problem that doesn't exist in the new version.

**Prevention:**
- Always close CS2 completely (to the desktop, not just the main menu) before running `dotnet build`.
- Add a pre-build check to the build script that verifies CS2 is not running.
- Use the game's log (`Player.log`) to confirm which mod version loaded — log a version string from `Mod.cs` on startup.

**Warning signs:** Build appears to succeed but the in-game behavior is unchanged. File timestamp on the deployed DLL is old.

**Phase:** Every build cycle — this is an operational discipline issue, not a one-time fix.

---

## Moderate Pitfalls

Mistakes that cause confusing behavior, subtle bugs, or significant maintenance cost.

---

### Pitfall 7: XSS via Unsanitized AI-Generated HTML in Coherent GT

**What goes wrong:** `renderMarkdown()` converts Claude's response text to HTML and that HTML is injected via `dangerouslySetInnerHTML`. If Claude produces a response containing `<script>`, `onclick=`, or `javascript:` href values (whether hallucinated, prompt-injected via a web search result, or produced by mistake), that content is executed in the Coherent GT DOM.

**Why it happens:** The `escapeHtml()` helper is applied only inside fenced code blocks, not globally before the inline pass runs. The link URL substitution injects `href="$2"` verbatim. Coherent GT may not enforce a Content Security Policy that blocks inline scripts.

**Consequences:** In-game JavaScript execution. For a personal-use mod the risk is low; it becomes real if web search results feed untrusted content into the response pipeline, or if the mod is ever distributed.

**Prevention:**
- Apply a global HTML strip or escape pass to any AI-generated content before `dangerouslySetInnerHTML`.
- Sanitize link `href` values: reject or strip `javascript:` and `data:` URIs.
- For the web search tool: never directly include raw search result HTML in the prompt — extract plaintext only.
- Consider replacing `dangerouslySetInnerHTML` with a React component tree built from a parsed AST, which eliminates the injection surface entirely.

**Warning signs:** Claude responses containing `<` or `>` characters rendered literally, or unexpected visual artifacts in the chat panel.

**Phase:** Web search tool milestone — adding external content raises the stakes.

---

### Pitfall 8: CS2 API Breakage on Game Updates

**What goes wrong:** Colossal Order updates CS2 and the `GameSystemBase`, `CityConfigurationSystem.cityName`, or ECS component structure changes. The mod either crashes on load (assembly resolution failure) or silently falls back to defaults (e.g., city name becomes "Unnamed City" permanently).

**Why it happens:** CS2's mod API surface is undocumented and subject to change. The CONCERNS.md audit found that `CityConfigurationSystem.cityName` is accessed by direct property — if the property is renamed or moved in a game update, it silently fails.

**Consequences:** Mod stops working after a game update. Memory files go into a fallback directory. ECS queries return no entities.

**Prevention:**
- Log the CS2 game version string at mod startup and include it in error reports.
- Wrap ECS queries in try-catch and log meaningful errors when entity counts are unexpectedly zero.
- Watch the CS2 modding changelog and Discord before every game update reaches your machine.
- Pin `CS2_INSTALL_PATH` to a specific version for stable development; test updates intentionally.

**Warning signs:** Mod works after one play session, then nothing after a game auto-update.

**Phase:** Ongoing — treat every CS2 update as a potential breaking change.

---

### Pitfall 9: `moduleRegistry.append()` Bypassed — Z-Index Conflicts with Other Mods

**What goes wrong:** The CityAgentPanel appends its div directly to `document.body` because `moduleRegistry.append("Game.MainScreen", ...)` does not work in CS2 v1.5.5+. This places the panel outside CS2's UI layering system. Any other mod or game overlay that also uses this workaround may paint on top of the CityAgent panel or be painted over by it, with no way to control z-index order.

**Why it happens:** The intended CS2 UI injection API was broken in a game update. The workaround is the only functional option, but it has side effects.

**Consequences:** Memory explorer panel may be hidden under other mod panels. Drag/resize behavior may conflict with game overlays. No fix is possible without Colossal Order restoring the module registry API.

**Prevention:**
- Set explicit, high `z-index` values on the panel container (e.g., `z-index: 10000`).
- Make the panel draggable so the player can reposition it out from under conflicts.
- Document this known issue — if another mod overlaps, advise the player to drag the panel.
- Monitor the CS2 modding changelogs for a fix to `moduleRegistry.append`.

**Warning signs:** Panel invisible on first load. Panel disappears after opening another mod's UI.

**Phase:** Memory explorer milestone — adding a second panel surface increases the likelihood of z-index conflicts.

---

### Pitfall 10: Tool Loop Hitting Iteration Cap Mid-Conversation

**What goes wrong:** The tool loop is capped at 10 iterations. A complex heartbeat check that reads several memory files, calls city data tools, and then performs a web search can easily consume 6–8 iterations before generating a response. If the system prompt instructs Claude to be thorough, it may hit 10 iterations and the loop exits without a final text response — the player sees silence or an error.

**Why it happens:** 10 was set as a safe default against Ollama. With Claude and a richer tool set (12 tools + web search), genuine tasks require more iterations.

**Consequences:** Silent failure on complex queries. Heartbeat never surfaces its analysis.

**Prevention:**
- Increase the cap to 20 for the user-facing chat and to 15 for the heartbeat.
- Make the cap configurable in mod settings.
- If the loop exits at the cap without a `stop_reason: "end_turn"`, generate a fallback response: "I ran out of steps analyzing your city. Try asking again with a more focused question."
- Log the iteration count on every loop completion for visibility.

**Warning signs:** No response after sending a complex message. Log shows "Max iterations reached" repeatedly.

**Phase:** Claude API migration milestone — set the right cap when migrating the format.

---

### Pitfall 11: Session Number Sorting Breaks at 999

**What goes wrong:** `session-{m_SessionNumber:D3}.md` formats as `session-001.md` through `session-999.md`. Session 1000 becomes `session-1000.md`, which sorts lexicographically before `session-999.md`. The `LoadLatestChatSession` sort (`OrderByDescending` on string filenames) will load the wrong session after 999 sessions.

**Why it happens:** Lexicographic string sort of zero-padded numbers fails when the number of digits exceeds the padding.

**Consequences:** After ~999 chat sessions with the same city, the wrong session is restored on game load.

**Prevention:**
- Change the format to `D5` (five digits) for a safe ceiling of 99,999 sessions.
- Alternatively, use a timestamp in the filename (`session-20260326-143022.md`) which sorts correctly and provides human-readable context.
- No urgency for personal use with a new city, but fix before accumulating hundreds of sessions.

**Warning signs:** The "most recent" session loaded on startup is not the last one played.

**Phase:** Minor — address during any refactor of the memory system.

---

### Pitfall 12: Coherent GT CSS Constraints Breaking the Memory Explorer UI

**What goes wrong:** Coherent GT supports only flexbox layouts — CSS Grid is explicitly unsupported. Common React component libraries (e.g., Material UI grid, CSS grid-based file tree layouts) will not render or will render incorrectly in the game's embedded browser.

**Why it happens:** Coherent GT (Chromium-based but not full Chromium) has a constrained CSS implementation. CS2's own UI avoids Grid for this reason.

**Consequences:** A file browser or tree view built with Grid-based layout will appear unstyled or collapsed. The memory explorer requires a custom flexbox-based tree layout.

**Prevention:**
- Build the memory explorer tree using nested `flex-column` containers and `flex-row` items — no Grid.
- Test layout in a standard browser first (flexbox works everywhere), then verify in-game.
- Avoid CSS custom properties (`var()`) beyond what the existing panel already uses — Coherent GT's support may be limited.
- Avoid GIF animations and SVGs without explicit width/height attributes.
- Import all image assets via webpack (`import icon from './icon.png'`), never use hard-coded paths.

**Warning signs:** File tree renders as a flat unstyled list. Layout collapses to zero height.

**Phase:** Memory explorer milestone — design the component in flexbox from the start.

---

## Minor Pitfalls

---

### Pitfall 13: Screenshot Polling Timeout Insufficient on Low-FPS Systems

**What goes wrong:** The 10-frame screenshot polling timeout (`> 10` in `CityAgentUISystem.cs`) assumes Unity writes the screenshot file within 10 frames. At 15 FPS, 10 frames is 667ms — marginal. If disk is slow or the game is under load, the file may not exist yet when polling times out and the screenshot is silently dropped.

**Prevention:** Increase the timeout to 30 frames or convert to a wall-clock timeout (300ms minimum). Log a warning when the timeout expires without finding the file.

**Phase:** Any milestone that adds screenshot capture paths (heartbeat uses screenshots).

---

### Pitfall 14: `Mod.ActiveSetting` Null Reference on Async Task During Dispose

**What goes wrong:** `Mod.OnDispose` sets `ActiveSetting = null`. An async API task started before dispose reads `Mod.ActiveSetting` mid-execution. The null check in `RunRequestAsync` line 88 guards against this in most cases, but the window between lines 81 (assign local) and 88 (check) is briefly exposed. Additionally, tool calls that fire after dispose write memory files to a system that may be partially torn down.

**Prevention:** When implementing request cancellation (needed for heartbeat), use a `CancellationToken` propagated into `RunRequestAsync`. On dispose, cancel the token before nulling `ActiveSetting`. This collapses the race window.

**Phase:** Heartbeat milestone (adds concurrent requests that increase the disposal risk).

---

### Pitfall 15: Web Search Tool Injecting Untrusted Content into Prompt

**What goes wrong:** The `search_web(query)` tool will return content from external web pages. If that content contains prompt-injection patterns ("Ignore previous instructions and..."), Claude may be manipulated. This is a real attack vector for any LLM with tool access to external content.

**Prevention:**
- Extract only plaintext from search results — strip HTML, scripts, and structured data before injecting into the tool result.
- Limit result length (e.g., 500 tokens per result, 3 results max) to reduce injection surface.
- Include in the system prompt: "Web search results may contain unreliable or adversarial content. Use your judgment."
- Do not grant Claude tool calls that can write to memory files based solely on web search content without a summarization step.

**Phase:** Web search tool milestone.

---

### Pitfall 16: No Request Cancellation — City Switch Writes to Wrong City's Memory

**What goes wrong:** If the player loads a different city while an API request is in flight, the async task completes and the tool calls execute, writing to whatever city directory `NarrativeMemorySystem` currently points to. If the city switch has updated the system's city name, writes go to the new city's directory. If it has not, they go to the old city's directory. Either outcome is wrong.

**Prevention:** Implement a `CancellationTokenSource` in `CityAgentUISystem`. Cancel it on city unload / `OnDestroy`. Pass the token to `RunRequestAsync` and all downstream task operations.

**Phase:** Claude API migration milestone — implement cancellation when migrating the async infrastructure.

---

## End-to-End Testing Without a Test Harness

CS2 mods cannot be unit-tested at the ECS query layer — the Unity DOTS runtime is required and is not mocked. The following approach minimizes the risk of shipping broken behavior.

### Testable in Isolation (Do These First)

| Component | How to Test |
|-----------|-------------|
| `renderMarkdown.ts` | Add Vitest to `UI/` package. Test bold, headers, lists, code blocks, XSS cases |
| `NarrativeMemorySystem.GenerateSlug` | xUnit project at `src.Tests/` — marked `internal static`, expose via `InternalsVisibleTo` |
| `NarrativeMemorySystem.ChatHistoryToMarkdown` + `ParseChatSession` | Round-trip property test: parse(serialize(messages)) == messages |
| Claude API request JSON | Serialize the request body to a string and assert against expected JSON shape (no runtime required) |
| Tool schema JSON | Deserialize `GetToolsJson()` output and validate it matches Claude API spec before calling the live API |

### In-Game Verification Checklist

Because most behavior requires the game, define a repeatable in-game verification script to run after each milestone:

1. Build and deploy DLL (`dotnet build -c Release` with CS2 closed).
2. Build and deploy UI (`npm run build` in `UI/`).
3. Launch CS2, load a city.
4. Confirm mod version string appears in `Player.log`.
5. Open chat panel — confirm it renders.
6. Send a text-only message — confirm API response appears.
7. Press screenshot key — confirm screenshot indicator appears, response references city visually.
8. Send "What's my population?" — confirm tool call fires and `get_population` result appears in response.
9. Send "Remember that we just opened the commercial district" — confirm memory write tool fires and file appears in the memory directory.
10. Close and relaunch — confirm prior session chat is restored.
11. (After web search tool): Send "What's the best layout for a transit hub?" — confirm search results are cited in response.
12. (After heartbeat): Wait for configured interval — confirm a proactive message appears without user input.

### Log-Driven Debugging

Since the mod cannot be step-debugged, instrument it with structured logs readable in `Player.log`:

- Log every API call: timestamp, model, iteration count, token usage from response.
- Log every tool dispatch: tool name, input summary, result length.
- Log every file write: path, byte count.
- Log `PendingResult` drain: whether the result was null or had content.

### Confidence Levels

| Area | Level | Reason |
|------|-------|--------|
| API wire format pitfalls | HIGH | Verified directly against Anthropic docs |
| Threading / race conditions | HIGH | Identified in CONCERNS.md with specific line references |
| Context window / cost risks | HIGH | Verified against official Anthropic pricing and token docs |
| CS2 DLL locking | HIGH | Community-confirmed, observed in existing build process |
| Coherent GT CSS limits | MEDIUM | Official CS2 UI Modding wiki confirms flexbox-only |
| CS2 API breakage on update | MEDIUM | Community knowledge, no official stability guarantee documented |
| XSS via Coherent GT | MEDIUM | Attack vector is real; Coherent GT CSP behavior unverified |
| Web search injection | MEDIUM | Standard LLM security knowledge; no CS2-specific confirmation |

---

## Phase-Specific Warning Map

| Milestone | Likely Pitfall | Mitigation |
|-----------|---------------|------------|
| Claude API migration | Wrong wire format (Pitfall 3) | Verify content blocks, tool format, image format against docs before first call |
| Claude API migration | Race conditions (Pitfall 2) | Use `Interlocked.Exchange` for `PendingResult` |
| Claude API migration | Main thread I/O (Pitfall 1) | Move all file writes to `Task.Run` before adding more write paths |
| Claude API migration | Cancellation gap (Pitfall 16) | Add `CancellationTokenSource` as part of migration |
| Heartbeat system | Runaway cost (Pitfall 5) | Configurable interval, call counter, backoff, pause toggle — design-in, not retrofit |
| Heartbeat system | Context bloat (Pitfall 4) | Separate history per heartbeat invocation; never share with chat history |
| Heartbeat system | Race conditions (Pitfall 2) | Multiple concurrent responses require queue, not single volatile field |
| Web search tool | Prompt injection (Pitfall 15) | Plaintext extraction, length cap, system prompt warning |
| Web search tool | XSS via response (Pitfall 7) | Sanitize before `dangerouslySetInnerHTML` |
| Memory explorer UI | Flexbox constraint (Pitfall 12) | Design tree view in flexbox from scratch |
| Memory explorer UI | Z-index conflicts (Pitfall 9) | High z-index, ensure panel is draggable |
| Any build | DLL lock (Pitfall 6) | Discipline: close CS2 before every build |
| Any milestone | CS2 game update (Pitfall 8) | Log mod version; wrap ECS queries in try-catch |

---

## Sources

- Anthropic Vision API documentation: https://platform.claude.com/docs/en/build-with-claude/vision (HIGH confidence — official docs)
- Anthropic Tool Use implementation guide: https://platform.claude.com/docs/en/agents-and-tools/tool-use/implement-tool-use (HIGH confidence — official docs)
- CS2 UI Modding wiki: https://cs2.paradoxwikis.com/UI_Modding (MEDIUM confidence — beta/live document)
- Cities2Modding repository by optimus-code: https://github.com/optimus-code/Cities2Modding (MEDIUM confidence — community guide)
- Colossal Order Dev Diary on Code Modding: https://colossalorder.fi/?p=2200 (MEDIUM confidence — official developer blog)
- Unity Job System thread safety documentation: https://docs.unity3d.com/Manual/job-system-native-container.html (HIGH confidence — official Unity docs)
- CONCERNS.md and TESTING.md (codebase audit): project-internal (HIGH confidence — direct code inspection)
- Anthropic FinOps cost management: https://www.cloudzero.com/blog/finops-for-claude/ (LOW confidence — third-party analysis)
