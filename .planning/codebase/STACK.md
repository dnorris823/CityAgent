# STACK
_Generated: 2026-03-26_

## Summary
CityAgent is a two-layer mod: a C# game bridge (.NET Standard 2.1) compiled to a DLL that runs inside Cities: Skylines 2's Unity 2022.3.7f1 DOTS/ECS runtime, and a React + TypeScript UI rendered inside CS2's embedded Coherent GT browser. The AI backend is an OpenAI-compatible HTTP API (configured to use Ollama or any compatible endpoint), called asynchronously from C# via `System.Net.Http`.

## Languages

**Primary:**
- C# (.NET Standard 2.1) — game bridge, ECS queries, HTTP API calls, file I/O, tool dispatch
- TypeScript (ES2020 target) — in-game React UI, markdown rendering, CS2 binding layer

**Secondary:**
- CSS — in-game panel styling (`UI/src/style.css`)

## Runtime

**C# Environment:**
- .NET Standard 2.1 (compiled to a DLL loaded by CS2's Unity Mono runtime)
- Unity 2022.3.7f1 (DOTS/ECS architecture — `Unity.Entities`, `Unity.Mathematics`)
- No standalone .NET runtime; runs inside the game process

**JS/UI Environment:**
- Coherent GT (Chromium-based embedded browser shipped with CS2)
- React and ReactDOM injected by the game at runtime as `window.React` / `window.ReactDOM`
- CS2 binding APIs injected as `window["cs2/api"]`, `window["cs2/bindings"]`, etc.
- Older Chromium — lacks `Array.at()`, Unicode property escapes, and modern JS features

**Package Manager:**
- npm (lockfile: `UI/package-lock.json` — present)
- User environment: Node.js v24.13.0, npm 11.6.2

## Frameworks

**Core (C#):**
- `GameSystemBase` / `UISystemBase` — CS2 ECS system base classes (from `Game.dll`)
- `Colossal.UI.Binding` — `ValueBinding<T>`, `TriggerBinding` for C# ↔ React communication
- `Colossal.Logging` — logging via `ILog` / `LogManager`
- `Colossal.IO.AssetDatabase` — mod settings persistence
- `Game.Settings` (ModSetting) — options UI integration
- `Unity.Entities` — ECS entity queries (`EntityQuery`, `ComponentType`)

**Core (UI):**
- React 18 (runtime-injected, not bundled) — component model
- TypeScript 5.3 — type checking, strict mode enabled
- Custom `renderMarkdown` utility (`UI/src/utils/renderMarkdown.ts`) — hand-rolled markdown-to-HTML renderer (no external markdown lib, due to Coherent GT compatibility constraints)

**Build:**
- Webpack 5.89 — bundles `UI/src/index.tsx` → `CityAgent.mjs` (ES module output)
- ts-loader 9.5 — TypeScript compilation within Webpack
- MiniCssExtractPlugin 2.7.6 — extracts CSS to `CityAgent.css`
- `dotnet build` (MSBuild / .NET SDK) — compiles C# → DLL

## Key Dependencies

**C# (all resolved from game-provided DLLs — not NuGet packages):**
- `Newtonsoft.Json` — JSON serialization for API payloads and tool results; sourced from `{CS2Dir}/Cities2_Data/Managed/Newtonsoft.Json.dll`
- `System.Net.Http` — `HttpClient` for async API calls; sourced from game Managed DLLs
- `UnityEngine.ScreenCaptureModule` — `ScreenCapture.CaptureScreenshot()` for vision input
- `UnityEngine.ImageConversionModule` — PNG byte handling
- `UnityEngine.InputLegacyModule` — `Input.GetKeyDown()` for screenshot keybind
- `cohtml.NET` — Coherent GT C# bindings (UI host registration)

**UI (devDependencies — build-time only, not shipped):**
- `typescript` ^5.3.0
- `webpack` ^5.89.0 + `webpack-cli` ^5.1.4
- `ts-loader` ^9.5.0
- `css-loader` ^6.8.1
- `mini-css-extract-plugin` ^2.7.6
- `style-loader` ^3.3.3
- `@types/react` ^18.2.0 + `@types/react-dom` ^18.2.0

**UI (runtime externals — injected by CS2, not bundled):**
- `react` → `window.React`
- `react-dom` → `window.ReactDOM`
- `cs2/api` → `window["cs2/api"]` (bindValue, useValue, trigger)
- `cs2/bindings`, `cs2/modding`, `cs2/ui`, `cs2/l10n`

**External CDN (runtime, from renderMarkdown):**
- Twemoji SVG assets via `https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/` — emoji rendering workaround for Coherent GT's missing emoji font support

## Configuration

**C# Build:**
- `src/CityAgent.csproj` — `TargetFramework: netstandard2.1`
- Output path: `%APPDATA%/../LocalLow/Colossal Order/Cities Skylines II/Mods/CityAgent/` (DLL deployed directly to mod folder on build)
- Game DLL references resolved from `CS2_INSTALL_PATH` env var (default: `C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II`)
- All game DLL references are `Private=False` (not copied to output)

**UI Build:**
- `UI/tsconfig.json` — target ES2020, module ES2020, strict mode, `moduleResolution: bundler`
- `UI/webpack.config.js` — entry `./src/index.tsx`, output `CityAgent.mjs` as ES module, output path = mod folder (`%APPDATA%/../LocalLow/...`)
- `publicPath: "coui://ui-mods/"` — asset URLs use CS2's shared mod UI host
- `externalsType: "window"` — CS2 runtime globals, not bundled

**Mod Settings (in-game):**
- Stored via `Colossal.IO.AssetDatabase` / `ModSetting` base class
- Settings class: `src/Settings.cs` (`Setting : ModSetting`)
- Key configurable values: `OllamaApiKey`, `OllamaModel`, `OllamaBaseUrl`, `SystemPrompt`, `ScreenshotKeybind`, panel dimensions, font size, memory limits

## Platform Requirements

**Development:**
- Windows (mod output path uses Windows `%APPDATA%` convention)
- Cities: Skylines 2 installed (Steam default or custom `CS2_INSTALL_PATH`)
- .NET SDK (for `dotnet build`)
- Node.js 18+ (for `npm run build`)
- IDE: VSCode with dotnet CLI (no Visual Studio required)

**Production:**
- Distributed as a CS2 mod via Paradox Mods
- DLL + `CityAgent.mjs` + `CityAgent.css` deployed to the CS2 Mods folder
- Requires active internet connection for AI API calls (Ollama or compatible endpoint)

## Build Commands

```bash
# C# mod (close CS2 first — game locks the DLL)
cd src && dotnet build -c Release

# UI
cd UI && npm install && npm run build
# Both outputs go directly to %APPDATA%/../LocalLow/.../Mods/CityAgent/
```

## Gaps / Unknowns
- Exact .NET SDK version in use is not pinned in any config file
- No lock on the Newtonsoft.Json version — depends on whatever CS2 ships
- Coherent GT's exact Chromium version is not documented in the codebase
- No `engines` field in `package.json` to enforce Node.js version
