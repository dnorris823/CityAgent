# CityAgent

## What This Is

CityAgent is a Cities: Skylines 2 mod that embeds Claude AI as an in-game city advisor, narrator, and chronicler. Inspired by CityPlannerPlays, it gives the player a conversational AI companion that sees the city via screenshot, reads live ECS data (population, budget, zoning, demand), remembers the city's story across sessions, and responds with narrative commentary, strategic recommendations, and real-world urban planning research. It's built for the player who wants their city to feel like it has history, personality, and a thoughtful advisor watching over it.

## Core Value

You ask Claude something about your city, it sees the current screenshot and live stats, and responds with narrative commentary that remembers where the city has been — in a polished chat panel that feels like it belongs in the game.

## Requirements

### Validated

*Inferred from existing codebase (2026-03-26)*

- ✓ CS2 mod loads and registers with the game's mod system — existing
- ✓ Toggle panel button opens/closes the chat UI — existing
- ✓ C#↔React binding system via `ValueBinding`/`TriggerBinding` over `"cityAgent"` namespace — existing
- ✓ ECS city data queries: population, households, employment, demand indices — existing
- ✓ LLM API integration with async tool-use loop (max 10 iterations) — existing
- ✓ 12 agent tools: city data (population, workforce, building demand, zoning) + memory CRUD — existing
- ✓ Narrative memory system with per-city file storage and 10 core protected files — existing
- ✓ Chat session persistence (session-NNN.md files, auto-pruned) — existing
- ✓ Screenshot capture → base64 → included in API call — existing
- ✓ Mod settings: API key, model name, base URL, system prompt, panel dims, font size — existing
- ✓ Drag and resize panel — existing
- ✓ Basic markdown rendering utility — existing

### Active

*What we're building toward for v1 personal use*

- [ ] **Chat UI polish** — proper chat bubble styling (user vs assistant visually distinct), scrollable history, clean typography
- [ ] **Markdown rendering quality** — Claude's formatted responses render correctly (headers, bold, lists, code blocks — no raw asterisks)
- [ ] **Loading / thinking indicator** — visible feedback in the panel while Claude is generating a response
- [ ] **Memory file explorer** — in-panel file system view: browse the per-city narrative memory tree, view/edit/delete individual files
- [ ] **Web search tool** — `search_web(query)` agent tool backed by Brave/Bing Search API; API key configurable in mod settings; Claude uses it to ground recommendations in real urban planning sources
- [ ] **Proactive heartbeat system** — Claude periodically checks city stats in the background and surfaces noteworthy events, anomalies, or suggestions; interval and behavior configurable
- ✓ **Claude API format** — `ClaudeAPISystem` sends correct `/v1/messages` format; explicit provider toggle (Claude API / Ollama) in settings — validated Phase 1
- ✓ **End-to-end validated** — full build → deploy → in-game cycle approved at human-verify checkpoint — validated Phase 1

### Out of Scope

- Multiplayer / shared sessions — single-player advisor only; the city story is personal
- Paradox Mods public distribution — v1 is for personal use; publishing is a future milestone
- Auto-play / city control — Claude advises and narrates; it never places zones, roads, or buildings
- Non-Anthropic models in v1 — Revised: Ollama is now a first-class provider option via the provider toggle added in Phase 1

## Context

- **Codebase state**: Phase 1 complete. `ClaudeAPISystem` sends correct Anthropic `/v1/messages` format with tool-use loop, vision input, and Ollama as an explicit primary provider option. All file I/O is async. Thread safety uses `Interlocked.Exchange`. End-to-end pipeline validated in-game. Several planned features (UI polish, heartbeat, memory explorer, web search) remain.
- **Persona**: Claude shifts roles based on context — narrating events (CityPlannerPlays energy), advising on strategy (urban planning expert), and chronicling the city's ongoing story (historian). The narrative memory system is the foundation of continuity.
- **Tech environment**: Unity 2022.3.7f1 DOTS/ECS, .NET Standard 2.1 DLL, React/TypeScript in Coherent GT (CS2's embedded Chromium). No npm packages — React, react-dom, and cs2 bindings are runtime-injected externals.
- **Developer**: Single developer, VSCode (not Visual Studio), working toward personal-use v1.

## Constraints

- **Tech stack**: C# .NET Standard 2.1 for mod layer; React/TypeScript for UI — no deviation without CS2 mod ecosystem reasons
- **Game thread**: All HTTP calls must be async/non-blocking — the game runs on the main thread; blocking = freezes
- **CS2 binding limit**: State crosses the C#↔JS bridge as JSON strings via `ValueBinding`; complex state must be serialized
- **API key security**: Anthropic API key stored in CS2 mod settings (encrypted by game); never hardcoded, never logged
- **ECS access**: City data must be read through `CityDataSystem` (GameSystemBase with entity queries) — no MonoBehaviour patterns
- **Distribution**: No Paradox Mods submission until v2+ milestone; mod output path is the local `%AppData%/Mods/CityAgent/` folder

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Explicit provider toggle (Claude / Ollama) | User may not have a Claude API key; Ollama needed as a first-class option, not just a fallback | Implemented Phase 1 — dropdown in mod settings, defaults to Ollama |
| User-configurable model name | Allows switching between models without a code change | Implemented Phase 1 |
| Web search via Brave/Bing in C# backend | Keeps search calls on the C# side (same HTTP client pattern); Claude calls a tool, C# fetches results | — Pending |
| Heartbeat as background system | Periodic proactive checks need their own CS2 system update loop; design TBD | — Pending |
| Memory explorer in React panel | File system view embedded in the chat panel; reads/writes via existing memory tool bindings | — Pending |
| Personal-use v1 (no Paradox Mods) | Validate the experience first; public release adds distribution complexity too early | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd:transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd:complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-03-29 after Phase 1 completion*
