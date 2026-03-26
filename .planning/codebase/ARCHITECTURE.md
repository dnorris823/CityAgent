# ARCHITECTURE
_Generated: 2026-03-26_

## Summary

CityAgent is a CS2 (Cities: Skylines 2) mod that bridges the game's ECS simulation layer to an external LLM (currently Ollama-compatible API) via a thin C# DLL and an in-game React panel. C# systems read live city data from Unity DOTS, capture screenshots, and orchestrate an async tool-use loop with the AI. The React UI communicates with C# exclusively through CS2's `ValueBinding`/`TriggerBinding` IPC system, with all state serialized as JSON strings over that bridge.

---

## Layers

### Entry Point — `src/Mod.cs`
- Implements `IMod`; CS2 calls `OnLoad(UpdateSystem)` on startup and `OnDispose()` on shutdown.
- Registers mod settings (`Setting`) in CS2's options UI.
- Registers the UI folder with `UIManager.defaultUISystem.AddHostLocation` so the React bundle is served at `coui://ui-mods/CityAgent.mjs`.
- Schedules all four C# systems into the CS2 `UpdateSystem` at the appropriate phases.

### Settings — `src/Settings.cs`
- Extends `ModSetting`; surfaces in the game's built-in options menu.
- Stores: API key, model name, base URL, system prompt, screenshot keybind, panel dimensions, font size, narrative memory limits.
- Read by systems via `Mod.ActiveSetting` (a static reference set during `OnLoad`).

### UI Bridge System — `src/Systems/CityAgentUISystem.cs`
- Extends `UISystemBase`; runs in `SystemUpdatePhase.UIUpdate` (same phase as the Coherent GT renderer).
- Owns all `ValueBinding<T>` and `TriggerBinding` objects — the **only** place C# state crosses to JavaScript.
- Bindings registered under the namespace `"cityAgent"`:
  - Values: `panelVisible` (bool), `messagesJson` (string/JSON array), `isLoading` (bool), `hasScreenshot` (bool), `panelWidth` (int), `panelHeight` (int), `fontSize` (int).
  - Triggers: `togglePanel`, `sendMessage` (string arg), `clearChat`, `removeScreenshot`, `captureScreenshot`.
- Holds the in-memory `List<ChatMessage>` (role + content + hadImage flag); serializes it to JSON for `messagesJson`.
- Screenshot flow: `ScreenCapture.CaptureScreenshot(path)` queues a file write; `OnUpdate` polls for the file over ~10 frames, reads bytes, base64-encodes them, stores in `m_PendingBase64Image`.
- API response drain: polls `ClaudeAPISystem.PendingResult` each frame; when non-null, appends to history and resets loading state.
- Settings polling: re-reads panel dimensions from `Mod.ActiveSetting` every ~60 frames (~1 s at 60 fps) and pushes updates to bindings.
- Delegates to `NarrativeMemorySystem` for chat session persistence and restore.

### City Data System — `src/Systems/CityDataSystem.cs`
- Extends `GameSystemBase`; runs in `SystemUpdatePhase.GameSimulation`.
- Queries four ECS archetypes every 128 simulation frames (~4 s at 30 fps):
  - Citizens (excluding tourists and temp entities) → `TotalPopulation`
  - Households with `PropertyRenter` → `TotalHouseholds`
  - `Employee` components → `TotalEmployed`
  - `WorkProvider` components (non-abandoned) → `TotalWorkplaces`
- Reads demand indices from `ResidentialDemandSystem`, `CommercialDemandSystem`, `IndustrialDemandSystem` singletons.
- Exposes all values as public properties; tool classes read from these — no direct ECS access outside this system.

### Claude/Ollama API System — `src/Systems/ClaudeAPISystem.cs`
- Extends `GameSystemBase`; `OnUpdate` is a no-op (all work is async).
- Owns the single static `HttpClient` instance.
- Maintains a `CityToolRegistry` populated with all tool implementations at `OnCreate`.
- `BeginRequest(userMessage, base64Png)`: fires a `Task` (`RunRequestAsync`) without blocking the game thread; result is written to the `volatile string? PendingResult` field.
- `RunRequestAsync` runs a loop (max 10 iterations):
  1. Builds an Ollama-native `/api/chat` request body with `model`, `messages`, `tools`, `stream:false`.
  2. Attaches base64 image in `images[]` array if a screenshot is present.
  3. Injects narrative memory context into the system prompt before the first call.
  4. If the response contains `tool_calls`, dispatches each via `CityToolRegistry.Dispatch`, appends `tool` role messages, and loops.
  5. On a plain text response (no tool calls), sets `PendingResult` and returns.

### Narrative Memory System — `src/Systems/NarrativeMemorySystem.cs`
- Extends `GameSystemBase`; `OnUpdate` is a no-op (called imperatively).
- Manages a per-city directory of markdown files at `{modDir}/memory/{city-slug}/`.
- City slug derived from city name read from CS2's `CityConfigurationSystem`, slugified (lowercase, hyphens).
- Core files (protected from deletion): `_index.md`, `characters.md`, `districts.md`, `city-plan.md`, `narrative-log.md`, `challenges.md`, `milestones.md`, `style-notes.md`, `economy.md`, `lore.md`.
- Chat session persistence: saves `chat-history/session-NNN.md` after each message; auto-prunes oldest sessions beyond `MaxChatHistorySessions`.
- Narrative log rotation: archives oldest entries to `archive/narrative-log-NNN.md` when entry count exceeds `MaxNarrativeLogEntries`.
- Context injection: `GetAlwaysInjectedContext()` returns `_index.md` + `style-notes.md` prepended to every API system prompt call.

### Tool System — `src/Systems/Tools/`

**Interface:** `ICityAgentTool` — `Name`, `Description`, `InputSchema` (JSON Schema string), `Execute(string inputJson) → string`.

**Registry:** `CityToolRegistry` — dictionary of tools; `GetToolsJsonOpenAI()` emits the OpenAI-format tools array; `Dispatch()` routes calls by name with exception handling.

**City data tools** (read from `CityDataSystem` properties):
- `GetPopulationTool` → `get_population`
- `GetBuildingDemandTool` → `get_building_demand`
- `GetWorkforceTool` → `get_workforce`
- `GetZoningSummaryTool` → `get_zoning_summary`

**Memory tools** (delegate to `NarrativeMemorySystem`):
- `ReadMemoryFileTool` → `read_memory_file`
- `WriteMemoryFileTool` → `write_memory_file`
- `AppendNarrativeLogTool` → `append_narrative_log`
- `CreateMemoryFileTool` → `create_memory_file`
- `DeleteMemoryFileTool` → `delete_memory_file`
- `ListMemoryFilesTool` → `list_memory_files`

### React UI — `UI/src/`

**Entry:** `UI/src/index.tsx` — CS2 loads this as an ES module (`CityAgent.mjs`). Appends a `div#city-agent-root` to `document.body` and renders `CityAgentPanel` into it via `window.ReactDOM` (injected by CS2 runtime).

**Main component:** `UI/src/components/CityAgentPanel.tsx`
- Outer wrapper `CityAgentPanel`: lazy-initializes `bindValue` bindings on first render (deferred to avoid crashes if `cs2/api` is not ready at import time); wraps `CityAgentInner` in an `ErrorBoundary`.
- Inner component `CityAgentInner`: all React hooks live here. Consumes seven `useValue()` subscriptions. Owns drag (header mouse-down → `mousemove`/`mouseup` listeners) and resize (five edge handles) logic in local state.
- Renders a floating toggle button (always visible) and the panel overlay (conditional on `panelVisible`).
- Assistant messages rendered through `renderMarkdown()` via `dangerouslySetInnerHTML`; user messages rendered as plain text.

**Markdown renderer:** `UI/src/utils/renderMarkdown.ts` — hand-rolled renderer (no external library) targeting Coherent GT's older Chromium. Handles headings, bold, italic, strikethrough, inline code, fenced code blocks, blockquotes, ordered/unordered lists, links, tables, and horizontal rules. Emoji replaced with Twemoji SVG `<span>` background-images (CDN: `cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0`). Unicode typography normalized to ASCII before parsing.

---

## Data Flow

### User sends a message with a screenshot

1. Player presses keybind (default F8) or clicks the SS button → `trigger("cityAgent","captureScreenshot")`.
2. `CityAgentUISystem.CaptureScreenshot()` calls `ScreenCapture.CaptureScreenshot(tempPath)`.
3. `OnUpdate` polls for the file; once found, reads bytes, base64-encodes, stores in `m_PendingBase64Image`; sets `hasScreenshot=true`.
4. Player types a message and sends → `trigger("cityAgent","sendMessage", text)`.
5. `OnSendMessage`: appends user message to `m_History`, calls `m_ClaudeAPI.BeginRequest(text, base64Png)`, clears screenshot state, sets `isLoading=true`.
6. `ClaudeAPISystem.RunRequestAsync` posts to Ollama `/api/chat` with the image in `images[]` and all registered tools.
7. If AI returns `tool_calls`, the system dispatches each tool synchronously within the async task and loops.
8. Final text response stored in `PendingResult`.
9. Next `CityAgentUISystem.OnUpdate` drains `PendingResult`, appends assistant message to history, pushes updated `messagesJson` to React, sets `isLoading=false`.
10. `NarrativeMemorySystem.SaveChatSession()` persists the full transcript to `chat-history/session-NNN.md`.

### AI reads city data

- During the tool-use loop, AI calls e.g. `get_population`.
- `CityToolRegistry.Dispatch("get_population", "{}")` → `GetPopulationTool.Execute()` → reads `CityDataSystem.TotalPopulation` / `TotalHouseholds` → returns JSON.
- `CityDataSystem` last refreshed from ECS up to 128 frames ago (cached, not live per-request).

### Memory context injection

- Before every API call, `NarrativeMemorySystem.GetAlwaysInjectedContext()` reads `_index.md` and `style-notes.md` from disk and appends them to the system prompt.
- AI can call memory tools to read/write other markdown files during the same tool-use loop.

---

## C# ↔ React Binding Contract

All bindings use namespace `"cityAgent"`.

| Direction | Name | Type | Meaning |
|-----------|------|------|---------|
| C# → JS | `panelVisible` | bool | Panel open/closed |
| C# → JS | `messagesJson` | string | JSON array of `{role, content, hadImage}` |
| C# → JS | `isLoading` | bool | API request in flight |
| C# → JS | `hasScreenshot` | bool | Screenshot queued for next send |
| C# → JS | `panelWidth` | int | Panel width (px) from settings |
| C# → JS | `panelHeight` | int | Panel height (px) from settings |
| C# → JS | `fontSize` | int | Base font size from settings |
| JS → C# | `togglePanel` | trigger | Open/close panel |
| JS → C# | `sendMessage` | trigger(string) | Send user text |
| JS → C# | `clearChat` | trigger | Start new session |
| JS → C# | `removeScreenshot` | trigger | Discard pending screenshot |
| JS → C# | `captureScreenshot` | trigger | Initiate screenshot capture |

---

## Thread Model

- `CityAgentUISystem.OnUpdate` runs on the game's main thread (UI update phase).
- `CityDataSystem.OnUpdate` runs on the game's main thread (simulation phase).
- `ClaudeAPISystem.RunRequestAsync` runs on the .NET thread pool (via `Task`). It writes only to `volatile string? PendingResult`; all other state access happens on the main thread. No locking beyond the volatile field.
- `NarrativeMemorySystem` file I/O runs synchronously on whichever thread calls it (main thread for initialization; same thread pool task for the `GetAlwaysInjectedContext` call within `RunRequestAsync`).

---

## Error Handling Strategy

- API errors: returned as `[Error]: ...` strings written to `PendingResult` and displayed as assistant messages.
- Tool errors: `CityToolRegistry.Dispatch` catches exceptions and returns a JSON error object so the conversation continues.
- Screenshot failures: logged; `m_ScreenshotWaitFrames` reset after 10 frames timeout.
- Memory errors: logged; non-fatal; `m_MemoryInitialized` set true on failure to prevent retry loops.
- React rendering errors: `ErrorBoundary` class component catches render exceptions and shows an inline error with a retry button.
- Binding initialization errors: `ensureBindings()` catches exceptions and sets `bindError`; outer `CityAgentPanel` shows a red error box if bindings failed.

---

## Gaps / Unknowns

- `GetBuildingDemandTool` and `GetWorkforceTool` source files were not read in full; assumed to follow the same pattern as `GetPopulationTool` (reading `CityDataSystem` properties).
- Direct zone cell counts are explicitly noted as "not yet implemented" in `GetZoningSummaryTool`; demand indices are used as a proxy.
- No streaming support: `stream: false` is hardcoded; full response must arrive before display.
- `CityAdvisorButton` component referenced in CLAUDE.md does not exist as a separate file; toggle button is inlined in `CityAgentPanel.tsx`.
- `ChatMessage.tsx` referenced in CLAUDE.md does not exist as a separate file; message rendering is inlined in `CityAgentPanel.tsx`.
- Budget data (mentioned in CLAUDE.md as a planned tool) is not yet implemented.
- Traffic data tool is not yet implemented.
