# INTEGRATIONS
_Generated: 2026-03-26_

## Summary
CityAgent integrates with one external AI API (configurable OpenAI-compatible endpoint, defaulting to Ollama) over HTTP, and reads live city data from CS2's internal ECS systems. All integration surface is in the C# layer. The React UI communicates exclusively with C# through CS2's binding system — it has no direct external connections. There is no database, auth service, or cloud infrastructure beyond the AI API call.

## APIs & External Services

**AI / LLM Backend:**
- Provider: Any OpenAI-compatible chat API (default configured as Ollama cloud at `https://ollama.com`)
  - Endpoint: `POST {OllamaBaseUrl}/api/chat`
  - Protocol: Ollama native chat format (`{ model, messages, tools, stream: false }`)
  - Response format: `{ "message": { "role", "content", "tool_calls": [...] }, "done": true }`
  - Tool calling: OpenAI-compatible function call schema produced by `CityToolRegistry.GetToolsJsonOpenAI()`
  - Vision input: images passed as base64 strings in `messages[].images[]` array (Ollama native format)
  - Auth: Bearer token via `Authorization: Bearer {OllamaApiKey}` header; omitted if key is empty
  - Default model: `kimi-k2.5:cloud` (user-configurable via mod settings)
  - Implementation: `src/Systems/ClaudeAPISystem.cs`
  - HTTP client: single static `HttpClient` instance (`s_Http`) — not pooled per-request
  - Async: `async/await` via `Task`; called from game thread, result polled via `PendingResult` volatile field

**Emoji CDN (UI-side, passive):**
- Provider: jsDelivr CDN serving Twemoji SVG assets
  - URL pattern: `https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/{codepoint}.svg`
  - Used by: `UI/src/utils/renderMarkdown.ts` — replaces emoji characters with `<span>` background-image tags
  - Failure mode: SVG load failures are silent (images simply don't render)
  - No auth required

## Game Engine Integrations (CS2 / Unity)

**ECS City Data (read-only):**
- Framework: Unity DOTS / ECS (`Unity.Entities`)
- Implementation: `src/Systems/CityDataSystem.cs`
- Data polled every ~128 game frames (≈4 seconds at 30fps)
- Entity queries:
  - `Citizen` (excluding `Deleted`, `Temp`, `TravelPurpose`) → population count
  - `Household` + `PropertyRenter` (excluding `Deleted`, `Temp`) → household count
  - `Employee` (excluding `Deleted`, `Temp`) → employed count
  - `WorkProvider` (excluding `Deleted`, `Temp`, `Abandoned`) → workplace count
- System references (demand data):
  - `Game.Simulation.ResidentialDemandSystem` → `buildingDemand.x` (low-density residential, 0–100)
  - `Game.Simulation.CommercialDemandSystem` → `buildingDemand` (0–100)
  - `Game.Simulation.IndustrialDemandSystem` → `industrialBuildingDemand`, `officeBuildingDemand` (0–100)

**City Name Resolution:**
- Attempts `Game.City.CityConfigurationSystem.cityName` via `World.GetExistingSystemManaged<>`
- Falls back to `"Unnamed City"` on failure
- Implementation: `src/Systems/NarrativeMemorySystem.cs`

**Screenshot Capture:**
- API: `UnityEngine.ScreenCapture.CaptureScreenshot(path)` — writes PNG to `Application.temporaryCachePath`
- Capture path: `{Application.temporaryCachePath}/cityagent_screenshot.png`
- Timing: queued at frame N, file polled over subsequent frames (up to 10-frame timeout)
- Encoding: raw PNG bytes read → `Convert.ToBase64String()` → sent in API request
- Trigger: configurable keybind (default F8, `UnityEngine.Input.GetKeyDown()`) or UI button
- Implementation: `src/Systems/CityAgentUISystem.cs`

**CS2 C# ↔ React Binding System:**
- Framework: `Colossal.UI.Binding` (`ValueBinding<T>`, `TriggerBinding`)
- Binding namespace: `"cityAgent"`
- All bindings registered in `src/Systems/CityAgentUISystem.cs`
- Value bindings (C# → React, read-only from UI):
  - `panelVisible` (bool) — panel open/closed state
  - `messagesJson` (string) — JSON-serialized chat history
  - `isLoading` (bool) — API request in flight
  - `hasScreenshot` (bool) — screenshot buffered and ready
  - `panelWidth` (int), `panelHeight` (int), `fontSize` (int) — UI dimensions from settings
- Trigger bindings (React → C#):
  - `togglePanel` — open/close panel
  - `sendMessage` (string) — submit user message, triggers API call
  - `clearChat` — start new session, persist current session
  - `removeScreenshot` — discard pending screenshot
  - `captureScreenshot` — trigger screenshot capture

**CS2 Mod Settings:**
- Framework: `Game.Settings.ModSetting` + `Colossal.IO.AssetDatabase`
- UI: Rendered in CS2's native options menu via `SettingsUI*` attributes
- Localization: `LocaleEN : IDictionarySource` registered with `GameManager.instance.localizationManager`
- Settings file: `src/Settings.cs`

**CS2 UI Asset Hosting:**
- API: `UIManager.defaultUISystem.AddHostLocation()`
- Registration: mod root folder registered under the shared `"ui-mods"` host
- Result: `coui://ui-mods/CityAgent.mjs` serves the UI module; game auto-imports it
- Cleanup: `RemoveHostLocation` called in `Mod.OnDispose()`

## Data Storage

**Databases:** None

**File Storage (local disk — narrative memory):**
- Location: `{ModDir}/memory/{city-slug}/`
- Format: Markdown files with YAML frontmatter
- Managed by: `src/Systems/NarrativeMemorySystem.cs`
- Core files (undeletable): `_index.md`, `characters.md`, `districts.md`, `city-plan.md`, `narrative-log.md`, `challenges.md`, `milestones.md`, `style-notes.md`, `economy.md`, `lore.md`
- Subdirectories: `chat-history/` (session transcripts `session-NNN.md`), `archive/` (rotated narrative log chunks)
- Rotation: narrative log archived when entry count exceeds `MaxNarrativeLogEntries` (default 50); chat history pruned beyond `MaxChatHistorySessions` (default 20)
- Path security: filename validation rejects `..`, `/`, `\` (path traversal prevention)

**Caching:** None (no Redis, Memcached, or similar)

## Authentication & Identity

**Auth Provider:** None — no user auth system

**API Key:** Single bearer token (`OllamaApiKey`) stored in CS2 mod settings via `Colossal.IO.AssetDatabase`. Settings are stored in the game's standard mod settings location (managed by CS2). The key is never committed to source (enforced by `.gitignore` and CLAUDE.md rules).

## Monitoring & Observability

**Error Tracking:** None (no Sentry, Datadog, etc.)

**Logging:**
- Framework: `Colossal.Logging` (`ILog` via `LogManager.GetLogger()`)
- Logger instance: `Mod.Log` (static, shared across all systems)
- `SetShowsErrorsInUI(false)` — errors do not surface in CS2's in-game UI overlay
- Log levels used: `Info`, `Warn`, `Error`
- UI-side logging: `console.error()` for React error boundary and trigger failures

## CI/CD & Deployment

**Hosting:** Paradox Mods (distribution target — no automation in repo)

**CI Pipeline:** None detected (no GitHub Actions, no CI config files)

**Build process:** Fully manual — developer runs `dotnet build` and `npm run build` locally; outputs land directly in the CS2 Mods folder.

## Webhooks & Callbacks

**Incoming:** None

**Outgoing:** None (the AI API call is a standard synchronous HTTP POST, not a webhook)

## Agent Tools (AI function calling)

The mod exposes a tool registry to the AI via OpenAI-compatible function call schema. All tools are implemented in `src/Systems/Tools/`.

**City data tools (read-only ECS access via `CityDataSystem`):**
- `GetPopulationTool` — returns population and household counts
- `GetBuildingDemandTool` — returns residential/commercial/industrial/office demand (0–100)
- `GetWorkforceTool` — returns employed count and workplace count
- `GetZoningSummaryTool` — returns zoning breakdown summary

**Narrative memory tools (disk file I/O via `NarrativeMemorySystem`):**
- `ReadMemoryFileTool` — reads a named `.md` file from city memory directory
- `WriteMemoryFileTool` — overwrites an existing memory file
- `AppendNarrativeLogTool` — appends timestamped entry to `narrative-log.md`
- `CreateMemoryFileTool` — creates a new `.md` file (fails if exists)
- `DeleteMemoryFileTool` — deletes a non-core memory file
- `ListMemoryFilesTool` — returns JSON list of all memory files with metadata

Tool interface: `src/Systems/Tools/ICityAgentTool.cs`
Tool registry: `src/Systems/Tools/CityToolRegistry.cs` (produces both Claude-format and OpenAI-format tool schemas; dispatches by name)

## Gaps / Unknowns
- The `OllamaBaseUrl` default (`https://ollama.com`) and model (`kimi-k2.5:cloud`) suggest the intent is Ollama's cloud service, but the code sends Ollama native `/api/chat` format — compatibility with `https://ollama.com` specifically is unverified in the codebase
- No retry logic or timeout configuration on `HttpClient` — network failures result in a single error response
- No streaming support — `"stream": false` is hardcoded; long responses block until complete
- The mod name in settings is "Ollama API Key" / "Ollama Model" but the system is designed to work with any OpenAI-compatible endpoint (naming is misleading)
- Screenshot base64 payload size is unbounded — large screenshots could cause memory pressure or API rejection
