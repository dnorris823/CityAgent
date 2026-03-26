# CityAgent — CLAUDE.md

## Project Overview

CityAgent is a Cities: Skylines 2 (CS2) mod that integrates Claude AI as an in-game narrative advisor.
Inspired by the storytelling style of YouTuber CityPlannerPlays, it lets players pass live city screenshots
to an LLM along with structured city data, which returns narrative context and build recommendations.

## Architecture

```
CS2 Game (Unity 2022.3.7f1 / DOTS-ECS)
  └── C# Mod (thin bridge layer — src/)
        ├── Reads city stats via ECS systems (population, budget, traffic, zoning)
        ├── Captures screenshots via Unity ScreenCapture API
        └── Calls Claude API over HTTP (async, non-blocking)
              └── Claude API (claude-sonnet-4-6, vision-enabled)
                    ├── Vision input: screenshot (base64 PNG)
                    ├── Tool calls: get_population(), get_budget(), get_traffic(), get_zoning()
                    └── Returns narrative + build recommendations
                          └── Displayed in in-game React/Coherent GT panel (UI/)
```

The C# mod layer stays thin. Intelligence lives in Claude. The React UI is just a web panel embedded in the game.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Mod / game bridge | C# (.NET Standard 2.1, compiled to DLL) |
| Game engine | Unity 2022.3.7f1 (DOTS / ECS architecture) |
| In-game UI | React + TypeScript (Coherent GT — CS2's embedded browser) |
| AI | Claude API (`claude-sonnet-4-6`, multimodal) |
| Distribution | Paradox Mods |

## Project Structure

```
CityAgent/
├── CLAUDE.md
├── README.md
├── .gitignore
├── src/                              # C# mod (compiled to DLL)
│   ├── CityAgent.csproj
│   ├── Mod.cs                        # IMod entry point
│   ├── Settings.cs                   # Mod options (API key, etc.)
│   └── Systems/
│       ├── CityAgentUISystem.cs      # UI bindings / C#↔JS bridge   [Phase 1]
│       ├── CityDataSystem.cs         # Reads ECS city data            [Phase 2]
│       └── ClaudeAPISystem.cs        # Claude API integration         [Phase 3]
└── UI/                               # React UI (Coherent GT)
    ├── package.json
    ├── webpack.config.js
    ├── tsconfig.json
    └── src/
        ├── index.tsx                 # UI entry point / component registration
        └── components/
            ├── CityAgentPanel.tsx    # Main chat panel                [Phase 1]
            ├── ChatMessage.tsx       # Individual message component   [Phase 6]
            └── CityAdvisorButton.tsx # Toolbar trigger button         [Phase 1]
```

## Build Phases

### Phase 1: Toolchain Validation ← CURRENT
**Goal**: A basic C# mod running in CS2 — a button that opens a panel.
- C# mod loads and registers with CS2's mod system
- `CityAgentUISystem` exposes `panelVisible` binding and `togglePanel` trigger
- React panel renders in-game, toggles open/closed
- **Success criteria**: See the panel in-game after a build → deploy cycle

### Phase 2: City Data Reading
**Goal**: Pull live city stats from the ECS and display them in the panel.
- Read population, budget, happiness from CS2 ECS `GameSystemBase` subclasses
- Expose data via `ValueBinding<T>` to the React UI
- Learn the DOTS/ECS access patterns (this is the steepest learning curve)

### Phase 3: Claude API Integration
**Goal**: Send a prompt to Claude and display the response in the panel.
- HTTP client in C# (`System.Net.Http.HttpClient`) calling `api.anthropic.com`
- Async/await to keep the game thread non-blocking
- Display streaming or complete response in the chat panel

### Phase 4: Screenshot Capture
**Goal**: Pass a city screenshot with the prompt (vision input).
- `ScreenCapture.CaptureScreenshot` or render to `RenderTexture`
- Base64-encode the PNG → include in Claude's `messages` array as image content
- Handle file I/O on a background thread

### Phase 5: Agent Tools
**Goal**: Give Claude structured access to live city data.
- Define tool schema: `get_population()`, `get_budget()`, `get_traffic_summary()`, `get_zoning_breakdown()`
- Implement tool_use / tool_result message loop
- Rich context for AI narrative and recommendations

### Phase 6: UI Polish
**Goal**: Production-quality in-game advisor UI.
- Chat history with scroll
- Loading / thinking states
- Narrative text formatting (markdown-ish)
- Settings panel for API key entry (never hardcode)

## Local Setup

### Prerequisites
- Cities: Skylines 2 (set `CS2_INSTALL_PATH` env var to the install directory)
  - Default Steam path: `C:/Program Files (x86)/Steam/steamapps/common/Cities Skylines II`
- Visual Studio 2022 with .NET desktop development workload
- CS2 Mod Template — search "colossal" in VS → Extensions → Manage Extensions
- Node.js 18+ (for UI build)

### Setting CS2_INSTALL_PATH
PowerShell (user-level, persists across sessions):
```powershell
[System.Environment]::SetEnvironmentVariable("CS2_INSTALL_PATH", "C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II", "User")
```

### Build & Deploy (C# mod)
```bash
# In src/ — close CS2 first!
dotnet build -c Release

# The DLL is output directly to:
# %AppData%/../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent/
```

### Build UI
```bash
cd UI
npm install
npm run build
# Output copies to the mod folder automatically (see webpack config)
```

## Key CS2 Modding Patterns

### C# ↔ React Binding System
CS2 uses a binding system to communicate between C# (game side) and React (UI side):

```csharp
// C# — expose a value and a trigger
m_PanelVisible = new ValueBinding<bool>("cityAgent", "panelVisible", false);
AddBinding(m_PanelVisible);
AddBinding(new TriggerBinding("cityAgent", "togglePanel", () => m_PanelVisible.Update(!m_PanelVisible.value)));
```

```tsx
// React — read the value and call the trigger
const panelVisible = useValue(bindValue<boolean>("cityAgent", "panelVisible"));
const toggle = () => trigger("cityAgent", "togglePanel");
```

### ECS Data Access (Phase 2+)
CS2 uses Unity DOTS. City data lives in ECS components, not MonoBehaviour fields.
Query via `EntityQuery` or access specific `GameSystemBase` singletons.

### UI Framework
CS2's UI runs in Coherent GT (Chromium-based). React, react-dom, and cs2 bindings are
injected by the runtime — reference them as webpack externals, not npm packages.

## Important Rules
- **Close CS2 before building** — the game locks the DLL file
- **Never hardcode the API key** — use mod settings (Phase 6) or a local `secrets.json` (gitignored)
- **Keep C# thin** — game hooks only; no business logic that could live elsewhere
- **CS2 is ECS, not OOP** — don't try to find MonoBehaviour components; query entity archetypes
- **UI is React/JS, not C#** — if you're writing layout code in C#, you're probably doing it wrong

## Reference Links
- CS2 Modding Wiki: https://wiki.paradoxinteractive.com/cs2/modding
- Official mod template docs: search "Colossal Order modding toolchain" on GitHub
- Game DLLs: `{CS2_INSTALL_PATH}/Cities2_Data/Managed/`
- Mod output path: `%AppData%/../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent/`
- Anthropic API docs: https://docs.anthropic.com

<!-- GSD:project-start source:PROJECT.md -->
## Project

**CityAgent**

CityAgent is a Cities: Skylines 2 mod that embeds Claude AI as an in-game city advisor, narrator, and chronicler. Inspired by CityPlannerPlays, it gives the player a conversational AI companion that sees the city via screenshot, reads live ECS data (population, budget, zoning, demand), remembers the city's story across sessions, and responds with narrative commentary, strategic recommendations, and real-world urban planning research. It's built for the player who wants their city to feel like it has history, personality, and a thoughtful advisor watching over it.

**Core Value:** You ask Claude something about your city, it sees the current screenshot and live stats, and responds with narrative commentary that remembers where the city has been — in a polished chat panel that feels like it belongs in the game.

### Constraints

- **Tech stack**: C# .NET Standard 2.1 for mod layer; React/TypeScript for UI — no deviation without CS2 mod ecosystem reasons
- **Game thread**: All HTTP calls must be async/non-blocking — the game runs on the main thread; blocking = freezes
- **CS2 binding limit**: State crosses the C#↔JS bridge as JSON strings via `ValueBinding`; complex state must be serialized
- **API key security**: Anthropic API key stored in CS2 mod settings (encrypted by game); never hardcoded, never logged
- **ECS access**: City data must be read through `CityDataSystem` (GameSystemBase with entity queries) — no MonoBehaviour patterns
- **Distribution**: No Paradox Mods submission until v2+ milestone; mod output path is the local `%AppData%/Mods/CityAgent/` folder
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Summary
## Languages
- C# (.NET Standard 2.1) — game bridge, ECS queries, HTTP API calls, file I/O, tool dispatch
- TypeScript (ES2020 target) — in-game React UI, markdown rendering, CS2 binding layer
- CSS — in-game panel styling (`UI/src/style.css`)
## Runtime
- .NET Standard 2.1 (compiled to a DLL loaded by CS2's Unity Mono runtime)
- Unity 2022.3.7f1 (DOTS/ECS architecture — `Unity.Entities`, `Unity.Mathematics`)
- No standalone .NET runtime; runs inside the game process
- Coherent GT (Chromium-based embedded browser shipped with CS2)
- React and ReactDOM injected by the game at runtime as `window.React` / `window.ReactDOM`
- CS2 binding APIs injected as `window["cs2/api"]`, `window["cs2/bindings"]`, etc.
- Older Chromium — lacks `Array.at()`, Unicode property escapes, and modern JS features
- npm (lockfile: `UI/package-lock.json` — present)
- User environment: Node.js v24.13.0, npm 11.6.2
## Frameworks
- `GameSystemBase` / `UISystemBase` — CS2 ECS system base classes (from `Game.dll`)
- `Colossal.UI.Binding` — `ValueBinding<T>`, `TriggerBinding` for C# ↔ React communication
- `Colossal.Logging` — logging via `ILog` / `LogManager`
- `Colossal.IO.AssetDatabase` — mod settings persistence
- `Game.Settings` (ModSetting) — options UI integration
- `Unity.Entities` — ECS entity queries (`EntityQuery`, `ComponentType`)
- React 18 (runtime-injected, not bundled) — component model
- TypeScript 5.3 — type checking, strict mode enabled
- Custom `renderMarkdown` utility (`UI/src/utils/renderMarkdown.ts`) — hand-rolled markdown-to-HTML renderer (no external markdown lib, due to Coherent GT compatibility constraints)
- Webpack 5.89 — bundles `UI/src/index.tsx` → `CityAgent.mjs` (ES module output)
- ts-loader 9.5 — TypeScript compilation within Webpack
- MiniCssExtractPlugin 2.7.6 — extracts CSS to `CityAgent.css`
- `dotnet build` (MSBuild / .NET SDK) — compiles C# → DLL
## Key Dependencies
- `Newtonsoft.Json` — JSON serialization for API payloads and tool results; sourced from `{CS2Dir}/Cities2_Data/Managed/Newtonsoft.Json.dll`
- `System.Net.Http` — `HttpClient` for async API calls; sourced from game Managed DLLs
- `UnityEngine.ScreenCaptureModule` — `ScreenCapture.CaptureScreenshot()` for vision input
- `UnityEngine.ImageConversionModule` — PNG byte handling
- `UnityEngine.InputLegacyModule` — `Input.GetKeyDown()` for screenshot keybind
- `cohtml.NET` — Coherent GT C# bindings (UI host registration)
- `typescript` ^5.3.0
- `webpack` ^5.89.0 + `webpack-cli` ^5.1.4
- `ts-loader` ^9.5.0
- `css-loader` ^6.8.1
- `mini-css-extract-plugin` ^2.7.6
- `style-loader` ^3.3.3
- `@types/react` ^18.2.0 + `@types/react-dom` ^18.2.0
- `react` → `window.React`
- `react-dom` → `window.ReactDOM`
- `cs2/api` → `window["cs2/api"]` (bindValue, useValue, trigger)
- `cs2/bindings`, `cs2/modding`, `cs2/ui`, `cs2/l10n`
- Twemoji SVG assets via `https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/` — emoji rendering workaround for Coherent GT's missing emoji font support
## Configuration
- `src/CityAgent.csproj` — `TargetFramework: netstandard2.1`
- Output path: `%APPDATA%/../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent/` (DLL deployed directly to mod folder on build)
- Game DLL references resolved from `CS2_INSTALL_PATH` env var (default: `C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II`)
- All game DLL references are `Private=False` (not copied to output)
- `UI/tsconfig.json` — target ES2020, module ES2020, strict mode, `moduleResolution: bundler`
- `UI/webpack.config.js` — entry `./src/index.tsx`, output `CityAgent.mjs` as ES module, output path = mod folder (`%APPDATA%/../LocalLow/...`)
- `publicPath: "coui://ui-mods/"` — asset URLs use CS2's shared mod UI host
- `externalsType: "window"` — CS2 runtime globals, not bundled
- Stored via `Colossal.IO.AssetDatabase` / `ModSetting` base class
- Settings class: `src/Settings.cs` (`Setting : ModSetting`)
- Key configurable values: `OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl`, `SystemPrompt`, `ScreenshotKeybind`, panel dimensions, font size, memory limits
## Platform Requirements
- Windows (mod output path uses Windows `%APPDATA%` convention)
- Cities: Skylines 2 installed (Steam default or custom `CS2_INSTALL_PATH`)
- .NET SDK (for `dotnet build`)
- Node.js 18+ (for `npm run build`)
- IDE: VSCode with dotnet CLI (no Visual Studio required)
- Distributed as a CS2 mod via Paradox Mods
- DLL + `CityAgent.mjs` + `CityAgent.css` deployed to the CS2 Mods folder
- Requires active internet connection for AI API calls (Ollama or compatible endpoint)
## Build Commands
# C# mod (close CS2 first — game locks the DLL)
# UI
# Both outputs go directly to %APPDATA%/../LocalLow/.../Mods/CityAgent/
## Gaps / Unknowns
- Exact .NET SDK version in use is not pinned in any config file
- No lock on the Newtonsoft.Json version — depends on whatever CS2 ships
- Coherent GT's exact Chromium version is not documented in the codebase
- No `engines` field in `package.json` to enforce Node.js version
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Summary
## C# Conventions
### Naming
- **Classes/Systems**: PascalCase. All game systems end in `System` (e.g. `CityDataSystem`, `ClaudeAPISystem`, `NarrativeMemorySystem`).
- **Interfaces**: `I` prefix + PascalCase (e.g. `ICityAgentTool`).
- **Private fields**: `m_` prefix + PascalCase (e.g. `m_PanelVisible`, `m_CityData`, `m_RequestInFlight`). Applied to all instance fields.
- **Private static fields**: `s_` prefix + PascalCase (e.g. `s_Http` in `ClaudeAPISystem`).
- **Public properties**: PascalCase, no prefix (e.g. `TotalPopulation`, `IsInitialized`, `CityName`).
- **Constants**: `k` prefix + PascalCase for group/section identifiers (e.g. `kGroup = "cityAgent"`, `kGeneralGroup`).
- **Methods**: PascalCase (e.g. `OnCreate`, `BeginRequest`, `PushMessagesBinding`).
- **Local variables**: camelCase (e.g. `modDir`, `apiKey`, `toolResult`).
### File Structure
- One class per file. File name matches class name exactly.
- Tools live in `src/Systems/Tools/` — one file per tool, filename matches class name.
- Namespace mirrors directory: `CityAgent` (root), `CityAgent.Systems`, `CityAgent.Systems.Tools`.
### C# ↔ React Binding Pattern
### Lifecycle Methods
- `OnCreate()` — initialization, query creation, dependency wiring
- `OnUpdate()` — per-frame logic (or empty `{ }` if not needed)
- `OnDestroy()` — cleanup; always calls `base.OnDestroy()`
### Error Handling
- All `try/catch` blocks log via `Mod.Log.Error(...)`.
- Async errors write to `PendingResult` as `"[Error]: ..."` strings that surface to the UI.
- Tool dispatch errors return a serialized JSON error object (never throw to caller):
- Guards against uninitialized state return `"[Error]: ..."` strings rather than throwing.
- `volatile` is used on `PendingResult` (written from async thread, read from game thread).
### Logging
- Single logger: `Mod.Log` (type `ILog`, initialized in `Mod.cs`).
- All log messages include the system prefix in brackets: `[ClaudeAPISystem]`, `[NarrativeMemorySystem]`.
- Three levels used: `Mod.Log.Info(...)`, `Mod.Log.Warn(...)`, `Mod.Log.Error(...)`.
### Async Pattern
### Null Handling
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`).
- Fields that are always set in `OnCreate` are annotated `null!` at declaration.
- Nullable parameters use `?` (e.g. `string? cityNameOverride`, `string? base64Png`).
### Throttling Pattern
### Tool Interface Pattern
- `Name` — snake_case string matching the API tool name (e.g. `"get_population"`)
- `Description` — plain English for the AI
- `InputSchema` — raw JSON Schema string (inline, not generated)
- `Execute(string inputJson)` — returns JSON result string
### XML Doc Comments
## TypeScript / React Conventions
### Naming
- **Components**: PascalCase, filename matches (e.g. `CityAgentPanel.tsx`, `CityAgentInner`).
- **Interfaces/Types**: PascalCase (e.g. `ChatMessage`).
- **Functions/hooks**: camelCase (e.g. `handleSend`, `safeTrigger`, `ensureBindings`).
- **Constants at module scope**: camelCase with `$` suffix for CS2 binding observables (e.g. `panelVisible$`, `messagesJson$`).
- **CSS class names**: `ca-` prefix + BEM-style (e.g. `ca-panel`, `ca-panel__header`, `ca-bubble--user`).
### File Organization
- Components in `UI/src/components/`
- Utilities in `UI/src/utils/`
- Global types in `UI/types/` (hand-authored `.d.ts` stubs for CS2 runtime APIs)
- Single CSS file: `UI/src/style.css` (no CSS modules, no styled-components)
### Binding Initialization Pattern
### Component Architecture
- Two-layer pattern: outer wrapper (`CityAgentPanel`) handles binding initialization and error display; inner component (`CityAgentInner`) contains all hooks and business logic.
- Error boundaries (`ErrorBoundary` class component) wrap the inner component tree.
- `safeTrigger()` wraps all `trigger()` calls to prevent uncaught errors from crashing the panel.
### React Hooks Usage
- `useState` for local UI state (`inputText`, `dragPos`, `resizedDims`)
- `useEffect` for side effects (auto-scroll, resetting state on prop changes)
- `useMemo` for expensive derivations (JSON parsing of `rawJson`)
- `useCallback` for handlers passed to DOM event listeners
- `useRef` for mutable values that don't trigger re-renders (drag/resize state, scroll container)
### Inline Styles vs. CSS Classes
- Dynamic values (position, dimensions, font size) use inline `style` props.
- All static/themeable styles are in `style.css` with `ca-` prefixed class names.
- CSS uses `em` units for padding/margins to scale with the `fontSize` binding.
### TypeScript Strictness
- `"strict": true` in `tsconfig.json`.
- CS2 runtime globals typed as `any` where the API surface is unknown (e.g. `(window as any).ReactDOM`).
- Component return types explicitly annotated as `React.FC`.
- Inline `React.CSSProperties` type used for style objects.
### Utilities
- Pure functions, no side effects, exported by name: `export function renderMarkdown(...)`.
- `renderMarkdown.ts` uses `var` declarations throughout (intentional — targets Coherent GT's older Chromium runtime that may lack `const`/`let` in some contexts).
### CSS Conventions
- All selectors prefixed `ca-` to avoid collisions with CS2's own UI.
- BEM naming: block (`ca-panel`), element (`ca-panel__header`), modifier (`ca-bubble--user`).
- Uses `rgba()` for all color values to support transparency.
- Avoids CSS features absent from Coherent GT: `gap`, `::placeholder`, `:disabled` pseudo-class (uses `[disabled]` attribute selector instead).
- Scrollbars styled via `-webkit-scrollbar` (Coherent GT supports this).
## Cross-Cutting Conventions
### Secret Handling
- API keys stored in CS2 mod settings (in-game options menu), never hardcoded.
- `secrets.json` and `*.apikey` are gitignored.
- API key masked in logs: first 4 + last 4 chars shown.
### JSON Serialization
- `Newtonsoft.Json` used throughout C# (game-bundled version).
- `JObject`/`JArray` used for dynamic API request/response construction.
- `JsonConvert.SerializeObject(anonymous_object)` used for tool results.
- Input schema strings are written inline as raw JSON strings (not generated from types).
### Markdown Files
- Memory files use YAML frontmatter with `---` delimiters.
- Frontmatter fields: `last_updated` (ISO 8601), type-specific counters.
- Narrative log entries are separated by `\n---\n`.
## Gaps / Unknowns
- No linting config present (no `.eslintrc`, `biome.json`, or `.prettierrc`). Style is enforced by convention only.
- No `.editorconfig` for consistent indentation rules across editors.
- The `ChatMessage` nested class in `CityAgentUISystem.cs` uses lowercase property names (`role`, `content`, `hadImage`) — inconsistent with the PascalCase convention used everywhere else in C#. This is intentional for JSON serialization compatibility with the React side.
- No barrel files (`index.ts`) in the React source — components are imported by direct path.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## Summary
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
- Holds the in-memory `List<ChatMessage>` (role + content + hadImage flag); serializes it to JSON for `messagesJson`.
- Screenshot flow: `ScreenCapture.CaptureScreenshot(path)` queues a file write; `OnUpdate` polls for the file over ~10 frames, reads bytes, base64-encodes them, stores in `m_PendingBase64Image`.
- API response drain: polls `ClaudeAPISystem.PendingResult` each frame; when non-null, appends to history and resets loading state.
- Settings polling: re-reads panel dimensions from `Mod.ActiveSetting` every ~60 frames (~1 s at 60 fps) and pushes updates to bindings.
- Delegates to `NarrativeMemorySystem` for chat session persistence and restore.
### City Data System — `src/Systems/CityDataSystem.cs`
- Extends `GameSystemBase`; runs in `SystemUpdatePhase.GameSimulation`.
- Queries four ECS archetypes every 128 simulation frames (~4 s at 30 fps):
- Reads demand indices from `ResidentialDemandSystem`, `CommercialDemandSystem`, `IndustrialDemandSystem` singletons.
- Exposes all values as public properties; tool classes read from these — no direct ECS access outside this system.
### Claude/Ollama API System — `src/Systems/ClaudeAPISystem.cs`
- Extends `GameSystemBase`; `OnUpdate` is a no-op (all work is async).
- Owns the single static `HttpClient` instance.
- Maintains a `CityToolRegistry` populated with all tool implementations at `OnCreate`.
- `BeginRequest(userMessage, base64Png)`: fires a `Task` (`RunRequestAsync`) without blocking the game thread; result is written to the `volatile string? PendingResult` field.
- `RunRequestAsync` runs a loop (max 10 iterations):
### Narrative Memory System — `src/Systems/NarrativeMemorySystem.cs`
- Extends `GameSystemBase`; `OnUpdate` is a no-op (called imperatively).
- Manages a per-city directory of markdown files at `{modDir}/memory/{city-slug}/`.
- City slug derived from city name read from CS2's `CityConfigurationSystem`, slugified (lowercase, hyphens).
- Core files (protected from deletion): `_index.md`, `characters.md`, `districts.md`, `city-plan.md`, `narrative-log.md`, `challenges.md`, `milestones.md`, `style-notes.md`, `economy.md`, `lore.md`.
- Chat session persistence: saves `chat-history/session-NNN.md` after each message; auto-prunes oldest sessions beyond `MaxChatHistorySessions`.
- Narrative log rotation: archives oldest entries to `archive/narrative-log-NNN.md` when entry count exceeds `MaxNarrativeLogEntries`.
- Context injection: `GetAlwaysInjectedContext()` returns `_index.md` + `style-notes.md` prepended to every API system prompt call.
### Tool System — `src/Systems/Tools/`
- `GetPopulationTool` → `get_population`
- `GetBuildingDemandTool` → `get_building_demand`
- `GetWorkforceTool` → `get_workforce`
- `GetZoningSummaryTool` → `get_zoning_summary`
- `ReadMemoryFileTool` → `read_memory_file`
- `WriteMemoryFileTool` → `write_memory_file`
- `AppendNarrativeLogTool` → `append_narrative_log`
- `CreateMemoryFileTool` → `create_memory_file`
- `DeleteMemoryFileTool` → `delete_memory_file`
- `ListMemoryFilesTool` → `list_memory_files`
### React UI — `UI/src/`
- Outer wrapper `CityAgentPanel`: lazy-initializes `bindValue` bindings on first render (deferred to avoid crashes if `cs2/api` is not ready at import time); wraps `CityAgentInner` in an `ErrorBoundary`.
- Inner component `CityAgentInner`: all React hooks live here. Consumes seven `useValue()` subscriptions. Owns drag (header mouse-down → `mousemove`/`mouseup` listeners) and resize (five edge handles) logic in local state.
- Renders a floating toggle button (always visible) and the panel overlay (conditional on `panelVisible`).
- Assistant messages rendered through `renderMarkdown()` via `dangerouslySetInnerHTML`; user messages rendered as plain text.
## Data Flow
### User sends a message with a screenshot
### AI reads city data
- During the tool-use loop, AI calls e.g. `get_population`.
- `CityToolRegistry.Dispatch("get_population", "{}")` → `GetPopulationTool.Execute()` → reads `CityDataSystem.TotalPopulation` / `TotalHouseholds` → returns JSON.
- `CityDataSystem` last refreshed from ECS up to 128 frames ago (cached, not live per-request).
### Memory context injection
- Before every API call, `NarrativeMemorySystem.GetAlwaysInjectedContext()` reads `_index.md` and `style-notes.md` from disk and appends them to the system prompt.
- AI can call memory tools to read/write other markdown files during the same tool-use loop.
## C# ↔ React Binding Contract
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
## Thread Model
- `CityAgentUISystem.OnUpdate` runs on the game's main thread (UI update phase).
- `CityDataSystem.OnUpdate` runs on the game's main thread (simulation phase).
- `ClaudeAPISystem.RunRequestAsync` runs on the .NET thread pool (via `Task`). It writes only to `volatile string? PendingResult`; all other state access happens on the main thread. No locking beyond the volatile field.
- `NarrativeMemorySystem` file I/O runs synchronously on whichever thread calls it (main thread for initialization; same thread pool task for the `GetAlwaysInjectedContext` call within `RunRequestAsync`).
## Error Handling Strategy
- API errors: returned as `[Error]: ...` strings written to `PendingResult` and displayed as assistant messages.
- Tool errors: `CityToolRegistry.Dispatch` catches exceptions and returns a JSON error object so the conversation continues.
- Screenshot failures: logged; `m_ScreenshotWaitFrames` reset after 10 frames timeout.
- Memory errors: logged; non-fatal; `m_MemoryInitialized` set true on failure to prevent retry loops.
- React rendering errors: `ErrorBoundary` class component catches render exceptions and shows an inline error with a retry button.
- Binding initialization errors: `ensureBindings()` catches exceptions and sets `bindError`; outer `CityAgentPanel` shows a red error box if bindings failed.
## Gaps / Unknowns
- `GetBuildingDemandTool` and `GetWorkforceTool` source files were not read in full; assumed to follow the same pattern as `GetPopulationTool` (reading `CityDataSystem` properties).
- Direct zone cell counts are explicitly noted as "not yet implemented" in `GetZoningSummaryTool`; demand indices are used as a proxy.
- No streaming support: `stream: false` is hardcoded; full response must arrive before display.
- `CityAdvisorButton` component referenced in CLAUDE.md does not exist as a separate file; toggle button is inlined in `CityAgentPanel.tsx`.
- `ChatMessage.tsx` referenced in CLAUDE.md does not exist as a separate file; message rendering is inlined in `CityAgentPanel.tsx`.
- Budget data (mentioned in CLAUDE.md as a planned tool) is not yet implemented.
- Traffic data tool is not yet implemented.
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
