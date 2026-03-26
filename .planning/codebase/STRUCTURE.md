# STRUCTURE
_Generated: 2026-03-26_

## Summary
CityAgent is a two-layer project: a C# mod DLL (`src/`) compiled for Cities: Skylines 2, and a React/TypeScript UI (`UI/`) bundled for Coherent GT (the game's embedded browser). The layers communicate via CS2's binding system. There is no shared code between layers.

## Directory Layout

```
CityAgent/
├── CLAUDE.md                          # Project instructions / architecture reference
├── README.md                          # Public documentation
├── .gitignore
├── src/                               # C# mod source (.NET Standard 2.1)
│   ├── CityAgent.csproj               # Project file — references game DLLs via CS2_INSTALL_PATH
│   ├── Mod.cs                         # IMod entry point, schedules all systems
│   ├── Settings.cs                    # ModSetting subclass — API key, panel dims, font size
│   ├── Systems/
│   │   ├── CityAgentUISystem.cs       # UISystemBase — ValueBindings + TriggerBindings bridge
│   │   ├── CityDataSystem.cs          # GameSystemBase — ECS queries for city stats
│   │   ├── ClaudeAPISystem.cs         # HTTP client → Claude API, tool loop, streaming
│   │   ├── NarrativeMemorySystem.cs   # File-based memory system for narrative continuity
│   │   └── Tools/                     # Agent tool implementations
│   │       ├── ICityAgentTool.cs      # Tool interface (Name, Description, Execute)
│   │       ├── CityToolRegistry.cs    # Registry + dispatcher for all tools
│   │       ├── GetPopulationTool.cs
│   │       ├── GetWorkforceTool.cs
│   │       ├── GetBuildingDemandTool.cs
│   │       ├── GetZoningSummaryTool.cs
│   │       ├── ReadMemoryFileTool.cs
│   │       ├── WriteMemoryFileTool.cs
│   │       ├── AppendNarrativeLogTool.cs
│   │       ├── CreateMemoryFileTool.cs
│   │       ├── DeleteMemoryFileTool.cs
│   │       └── ListMemoryFilesTool.cs
│   └── obj/                           # Build artifacts (gitignored)
│
└── UI/                                # React/TypeScript frontend
    ├── package.json
    ├── webpack.config.js              # Bundles to mod output folder; cs2/* are externals
    ├── tsconfig.json
    ├── types/
    │   ├── cs2-api.d.ts               # Type shims for cs2/api (trigger, bindValue, useValue)
    │   └── cs2-bindings.d.ts          # Type shims for CS2 binding primitives
    └── src/
        ├── index.tsx                  # Registers components with the CS2 UI runtime
        ├── style.css                  # Global panel styles
        ├── utils/
        │   └── renderMarkdown.ts      # Lightweight markdown → HTML renderer
        └── components/
            └── CityAgentPanel.tsx     # Main panel: chat history, input, drag/resize
```

## Key Files

| File | Purpose |
|------|---------|
| `src/Mod.cs` | Entry point — registers settings and schedules all `GameSystemBase` / `UISystemBase` systems |
| `src/Systems/CityAgentUISystem.cs` | All C#↔React bindings live here; source of truth for binding names |
| `src/Systems/ClaudeAPISystem.cs` | Claude API HTTP calls, tool-use loop, message assembly |
| `src/Systems/CityDataSystem.cs` | ECS queries — reads population, budget, zoning, traffic from game entities |
| `src/Systems/Tools/CityToolRegistry.cs` | Registers all `ICityAgentTool` implementations; dispatches tool calls by name |
| `UI/src/components/CityAgentPanel.tsx` | Entire React UI — chat history, markdown rendering, drag/resize |
| `UI/webpack.config.js` | Build config — output path points directly to mod folder; cs2 packages are externals |

## Naming Conventions

### C#
- Classes: `PascalCase` (e.g., `CityAgentUISystem`, `GetPopulationTool`)
- Tool files: `<Verb><Noun>Tool.cs` pattern (e.g., `GetZoningSummaryTool.cs`)
- Binding namespace string: `"cityAgent"` (camelCase, used in both C# and TypeScript)
- Binding value names: camelCase strings (e.g., `"panelVisible"`, `"chatHistory"`)
- Systems inherit from `UISystemBase` (UI bridge) or `GameSystemBase` (ECS access)

### TypeScript / React
- Components: `PascalCase.tsx`
- Utilities: `camelCase.ts`
- Binding calls use literal strings matching C# definitions exactly

## Where to Add New Code

| What to add | Where |
|-------------|-------|
| New Claude agent tool | New `<Verb><Noun>Tool.cs` in `src/Systems/Tools/`, implement `ICityAgentTool`, register in `CityToolRegistry.cs` |
| New ECS city data query | `src/Systems/CityDataSystem.cs` |
| New C#↔React binding | `src/Systems/CityAgentUISystem.cs` (ValueBinding or TriggerBinding) |
| New mod setting | `src/Settings.cs` |
| New UI component | `UI/src/components/` |
| UI utility | `UI/src/utils/` |
| New type shim for CS2 API | `UI/types/` |

## Gaps / Unknowns
- `ChatMessage.tsx` and `CityAdvisorButton.tsx` referenced in CLAUDE.md are not yet implemented (functionality merged into `CityAgentPanel.tsx`)
- No `UI/dist/` or build output in repo (expected — gitignored, written directly to mod folder by webpack)
- `src/obj/` build artifacts are present in repo (not gitignored — potential noise)
- Traffic tool (`GetTrafficTool`) not yet implemented despite being in Phase 5 plan
