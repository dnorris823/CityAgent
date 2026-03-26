# CONVENTIONS
_Generated: 2026-03-26_

## Summary
CityAgent is a dual-language codebase: C# (.NET Standard 2.1) for the game mod layer and TypeScript/React for the in-game UI. Each language follows its own idiomatic style with no shared linting config. Conventions are internally consistent within each layer but are not enforced by automated tooling — they are maintained by discipline and code review.

---

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
All bindings are declared in `OnCreate()` and stored as `m_`-prefixed fields:
```csharp
m_PanelVisible = new ValueBinding<bool>(kGroup, "panelVisible", false);
AddBinding(m_PanelVisible);
AddBinding(new TriggerBinding<string>(kGroup, "sendMessage", OnSendMessage));
```
Binding group is always the `kGroup` constant `"cityAgent"`. Binding names are camelCase strings.

### Lifecycle Methods
All systems implement CS2 `GameSystemBase` or `UISystemBase` with three lifecycle methods:
- `OnCreate()` — initialization, query creation, dependency wiring
- `OnUpdate()` — per-frame logic (or empty `{ }` if not needed)
- `OnDestroy()` — cleanup; always calls `base.OnDestroy()`

Each lifecycle method logs its invocation: `Mod.Log.Info($"{nameof(ClassName)}.{nameof(OnCreate)}")`.

### Error Handling
- All `try/catch` blocks log via `Mod.Log.Error(...)`.
- Async errors write to `PendingResult` as `"[Error]: ..."` strings that surface to the UI.
- Tool dispatch errors return a serialized JSON error object (never throw to caller):
  ```csharp
  return JsonConvert.SerializeObject(new { error = $"Tool '{toolName}' threw: {ex.Message}" });
  ```
- Guards against uninitialized state return `"[Error]: ..."` strings rather than throwing.
- `volatile` is used on `PendingResult` (written from async thread, read from game thread).

### Logging
- Single logger: `Mod.Log` (type `ILog`, initialized in `Mod.cs`).
- All log messages include the system prefix in brackets: `[ClaudeAPISystem]`, `[NarrativeMemorySystem]`.
- Three levels used: `Mod.Log.Info(...)`, `Mod.Log.Warn(...)`, `Mod.Log.Error(...)`.

### Async Pattern
Only `ClaudeAPISystem` uses async. Pattern:
```csharp
public void BeginRequest(string userMessage, string? base64Png)
{
    if (m_RequestInFlight) return;
    m_RequestInFlight = true;
    _ = RunRequestAsync(userMessage, base64Png);
}
private async Task RunRequestAsync(...) { ... }
```
`ConfigureAwait(false)` is used on all awaits. Results are communicated back to the game thread via `volatile PendingResult`.

### Null Handling
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`).
- Fields that are always set in `OnCreate` are annotated `null!` at declaration.
- Nullable parameters use `?` (e.g. `string? cityNameOverride`, `string? base64Png`).

### Throttling Pattern
`CityDataSystem` uses frame-index modulo to throttle updates:
```csharp
if (m_SimulationSystem.frameIndex % 128 != 77) return;
```
Settings polling in `CityAgentUISystem` uses a counter: `if (++m_SettingsPollCounter >= 60)`.

### Tool Interface Pattern
All agent tools implement `ICityAgentTool` (`src/Systems/Tools/ICityAgentTool.cs`):
- `Name` — snake_case string matching the API tool name (e.g. `"get_population"`)
- `Description` — plain English for the AI
- `InputSchema` — raw JSON Schema string (inline, not generated)
- `Execute(string inputJson)` — returns JSON result string

Tool implementations are one-responsibility classes, constructed with a data system reference injected via constructor.

### XML Doc Comments
Used on public interfaces and non-obvious methods. Single-line for properties, multi-line `<summary>` for methods with non-obvious behavior. Example from `ICityAgentTool.cs`:
```csharp
/// <summary>Tool name as Claude will reference it (e.g. "get_population").</summary>
string Name { get; }
```

---

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
CS2 bindings are lazy-initialized at first render (not at module scope) to avoid crashes before the runtime is ready:
```typescript
let panelVisible$: any = null;
let bindingsReady = false;

function ensureBindings() {
  if (bindingsReady) return;
  try {
    panelVisible$ = bindValue<boolean>("cityAgent", "panelVisible");
    bindingsReady = true;
  } catch (e: any) {
    bindError = e?.message || "Unknown binding error";
  }
}
```

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

---

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

---

## Gaps / Unknowns
- No linting config present (no `.eslintrc`, `biome.json`, or `.prettierrc`). Style is enforced by convention only.
- No `.editorconfig` for consistent indentation rules across editors.
- The `ChatMessage` nested class in `CityAgentUISystem.cs` uses lowercase property names (`role`, `content`, `hadImage`) — inconsistent with the PascalCase convention used everywhere else in C#. This is intentional for JSON serialization compatibility with the React side.
- No barrel files (`index.ts`) in the React source — components are imported by direct path.
