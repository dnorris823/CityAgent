# Feature Landscape

**Domain:** CS2 mod / in-game LLM narrative advisor
**Researched:** 2026-03-26
**Confidence:** MEDIUM — No direct comparators (no other CS2 LLM-advisor mods found); conclusions drawn from adjacent domains (CS2 data mods, agentic UX patterns, general AI chat UI, urban planning AI tools)

---

## Context

CityAgent occupies a unique niche: it is the only known CS2 mod that integrates a multimodal LLM as a conversational city advisor with narrative memory. There is no direct comparator to benchmark against. The ChatGPT custom GPT "Cities Skylines 2 City Council" exists as a standalone web tool but has no game integration, screenshot vision, live ECS data, or memory. The feature landscape below is therefore synthesized from:

- CS2 data/stats mods (City Monitor, Info Loom, City Vitals Watch) for in-game panel UX norms
- Proactive AI agent design literature for heartbeat/alert patterns
- Minecraft and Lethal Company in-game config/file editor mods for memory explorer UX
- Agentic UX research for notification fatigue and interrupt design
- Brave Search API for web-grounding implementation patterns
- Urban planning AI tools (UrbanSim, Autodesk Forma) for what real-world grounding looks like

---

## Table Stakes

Features users (even a single developer as primary user) expect to be working. If broken or absent, the mod feels unfinished or unusable.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Chat panel opens/closes reliably | Core interaction surface — without this nothing else works | Low | Toggle is implemented; needs end-to-end validation |
| Visually distinct user vs. assistant messages | Every AI chat UI since 2020 differentiates sender visually; raw monochrome list feels broken | Low | Currently inlined in CityAgentPanel.tsx without distinct bubble styles |
| Loading / thinking indicator | Without this, users assume the mod has crashed during API calls; well-documented in chat UI research | Low | Hardcoded `stream: false` means full wait with no feedback — must fix |
| Working markdown rendering | Claude's responses use headers, bold, lists; raw asterisks and pound signs break the experience | Medium | Hand-rolled renderer exists but has known edge cases (nested lists, etc.) |
| Claude API format (not Ollama) | The mod is named CityAgent / ClaudeAPISystem; Ollama format is wrong endpoint, wrong tool schema, wrong image format | Medium | Single biggest blocker; tools format, image format, endpoint, auth headers all need change |
| Configurable API key | API key security is non-negotiable for any public or personal-use mod | Low | Settings.cs exists but field is named OllamaApiKey — needs rename and correct default URL |
| Screenshot capture included in messages | The entire vision premise fails without this; it's the differentiating input | Medium | Implemented but synchronous base64 read on main thread is a known performance concern |
| ECS city data in tool calls | The advisor cannot give useful advice without knowing population, budget, demand | Medium | Population/employment tools exist; budget and traffic tools are missing |
| Narrative memory persists across sessions | "The city has a history" is the core value — losing memory between sessions breaks the premise | Medium | File system exists; restored session on load shows only last session transcript |

---

## Differentiators

Features that set CityAgent apart from anything else in the CS2 modding ecosystem. No other CS2 mod offers any of these — they are the product's competitive identity.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Proactive heartbeat system | Advisor surfaces problems *before* the player asks — traffic spike, budget crisis, low happiness — without requiring user initiation; creates the feeling of a real advisor watching over the city | High | Needs its own CS2 system update loop; interval config needed; alert fatigue design critical (see Pitfalls) |
| Web search grounding | Claude can look up real urban planning research mid-conversation ("here's how Portland solved this") rather than relying purely on training data; turns the advisor from chatbot to research assistant | Medium | Brave LLM Context API is the right tool: pre-extracted content optimized for LLM consumption, ~130ms p90 overhead, $5/1k requests; C# HTTP call fits the existing ClaudeAPISystem pattern |
| Memory file explorer in-panel | Player can see and edit the city's narrative memory files without leaving the game; trust-building (player knows what Claude remembers); allows correcting wrong memories | High | Minecraft config-editor and LethalConfig mods demonstrate the pattern: file tree, text editor, save/discard; all reads/writes go through existing memory tool bindings |
| Narrative continuity across many sessions | City feels like it has a real history — Claude references past events, tracks recurring problems, maintains a "voice" | High | Foundation exists (narrative-log.md, core files); depends on always-injected context and memory tools working correctly end-to-end |
| CityPlannerPlays narrator persona | Advisor speaks with enthusiasm, urban planning expertise, and storytelling energy — not dry JSON summaries | Low (prompt) | Entirely prompt-controlled; the system prompt and persona tuning are already in Settings |
| Vision input (screenshot + ECS data combined) | Claude sees what the player sees AND has structured data; this is richer context than any other city-building advisor tool | Medium | Implemented but untested end-to-end; image + tool results in same conversation is the key pattern |

---

## Missing Tools (Gap Features)

These are not differentiators — they are table stakes for the advisor to give competent advice. They are gaps in the current implementation.

| Missing Tool | Impact of Gap | Complexity | Notes |
|-------------|---------------|------------|-------|
| `get_budget()` | Claude cannot advise on finances at all — income, expenses, debt, tax rates all opaque | Medium | CS2 ECS has budget data in `EconomySystem` or equivalent; needs a new GameSystemBase subclass query |
| `get_traffic_summary()` | Traffic is CS2's most common failure mode; blind advisor cannot diagnose or recommend traffic fixes | Medium-High | Traffic data in CS2 ECS is complex; start with congestion index or average speed per road type rather than full network analysis |
| `get_zoning_breakdown()` (actual cell counts) | Current tool returns demand indices (0–100) not actual zoned area; advisor gives vague zone advice | Medium | CS2 ECS stores zone cell data; requires correct entity query for zoned tile counts by type |
| `get_services_summary()` | Happiness, education, healthcare, deathcare coverage gaps are common advisor topics | Medium | CS2 exposes service coverage data via ECS; one tool covering key service metrics |

---

## Anti-Features

Things to deliberately NOT build. Each would waste effort, introduce fragility, or harm the user experience.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Streaming response display | Streaming in Coherent GT via C#↔JS bridge requires SSE parsing across the binding layer — significant complexity for a single-developer mod with no streaming infrastructure; the game's `ValueBinding` is not designed for incremental updates | Show a well-designed loading indicator (animated dots, "Claude is thinking...") for the full wait; streaming is a future milestone if response latency becomes a pain point |
| Auto-city-control (zoning, roads, buildings) | Violates the explicit project out-of-scope decision; also CS2 modding APIs for placing objects are significantly more complex and mod-version-fragile than read-only ECS queries | Keep Claude as advisor only; if "execute this recommendation" is wanted later, it is its own milestone |
| Multiplayer / shared sessions | Out of scope; single-player city story is personal and stateful | N/A |
| Full markdown library via npm | Coherent GT has no npm runtime; importing a markdown library requires bundling it, and the existing hand-rolled renderer handles the actual Claude output patterns | Fix the specific known edge cases in the existing renderer (nested lists, escaped chars) rather than replacing it |
| On-screen persistent HUD overlay | CS2 mods using raw `document.body.appendChild` (current workaround) are fragile; a persistent HUD adds z-index fights with other mods and game overlays | The toggle panel is the right model; heartbeat alerts go into the chat panel as advisor messages, not a separate HUD layer |
| External LLM support (Ollama, OpenAI, etc.) | v1 scope is Claude API specifically; multi-provider adds configuration complexity, format divergence, and testing surface area | Make model name user-configurable (done) so users can switch between Claude model versions; defer non-Anthropic support to post-v1 |
| Notification spam from heartbeat | Research is clear: alert fatigue causes users to disable the feature entirely; if heartbeat fires more than once every few minutes it becomes noise | Heartbeat must be configurable (interval), filterable (min severity), and aggregating (one alert for multiple issues, not one per issue) |
| In-game web browser / URL display | Coherent GT is Chromium-based but mod panels are not general browsers; navigating external pages would break the game UI context | Web search returns text summaries to Claude; Claude synthesizes them into advice; raw URLs in the chat are fine as citations |

---

## Feature Dependencies

```
Claude API migration (correct endpoint + auth + format)
  └── Everything: without correct API format, tool calls fail, image blocks fail, tool loop fails

ECS tool completeness (budget, traffic, zoning cell counts, services)
  └── Heartbeat system (needs richer data to detect meaningful events)
  └── Web search grounding (search is most useful when Claude has context to ask the right question)
  └── Narrative quality (advisor advice is only as good as the data it sees)

Chat UI polish (bubbles, markdown, loading indicator)
  └── Memory file explorer (shares the panel; polish first reduces rework)
  └── End-to-end validation (hard to validate if rendering is broken)

Narrative memory (working end-to-end)
  └── Memory file explorer (explorer reads/writes the same files; memory must work first)
  └── Heartbeat (heartbeat writes observations to memory; memory must be reliable)

Web search (Brave API key in settings)
  └── No hard dependencies, but quality improves with better ECS tools
  └── Requires new mod settings field for search API key

Heartbeat system (background CS2 system loop)
  └── ECS tool completeness (needs budget + traffic to detect meaningful events)
  └── Chat UI polish (alerts appear in panel; panel must handle unsolicited messages gracefully)
  └── Narrative memory (heartbeat observations should be persisted)
```

**Recommended build order based on dependencies:**

1. Claude API migration — unblocks everything
2. Chat UI polish + loading indicator — unblocks end-to-end validation, reduces rework
3. End-to-end validation — confirms foundation before layering features
4. Missing ECS tools (budget, traffic, zoning counts) — raises advisor intelligence floor
5. Web search tool (Brave API) — extends advisor to real-world knowledge
6. Memory file explorer — trust and control layer; memory must be solid first
7. Heartbeat system — requires all of the above for meaningful, non-spammy alerts

---

## MVP Recommendation

The minimal set that makes CityAgent feel like a real product rather than a scaffolded demo:

**Must ship:**
1. Claude API format migration (correct endpoint, tool format, image blocks)
2. Chat UI: distinct message bubbles, scrollable history, loading indicator
3. Working markdown rendering (fix known edge cases)
4. At least one missing tool: `get_budget()` — finances are the most universally relevant city topic
5. End-to-end validated: screenshot + tool calls + narrative memory + rendered response, in-game

**Ship soon after:**
- Web search grounding (Brave LLM Context API — straightforward C# HTTP call)
- Memory file explorer (highest trust-building feature)

**Defer:**
- Heartbeat system — high complexity, needs everything else stable first; alert fatigue design deserves careful thought
- Traffic tool — complex ECS queries; start with a congestion index approximation
- Full zoning cell counts — demand indices are good enough for advisor quality at MVP

---

## Sources

- Agentic UX interrupt patterns: [Designing For Agentic AI — Smashing Magazine](https://www.smashingmagazine.com/2026/02/designing-agentic-ai-practical-ux-patterns/) | [Designing for Autonomy — UXmatters](https://www.uxmatters.com/mt/archives/2025/12/designing-for-autonomy-ux-principles-for-agentic-ai.php)
- Notification fatigue and alert design: [Design Guidelines For Better Notifications UX — Smashing Magazine](https://www.smashingmagazine.com/2025/07/design-guidelines-better-notifications-ux/) | [Signal Detection Theory — UX Bulletin](https://www.ux-bulletin.com/signal-detection-theory-in-ux/)
- Chat UI patterns and loading states: [16 Chat UI Design Patterns That Work in 2025 — Bricxlabs](https://bricxlabs.com/blogs/message-screen-ui-deisgn) | [AI UI Patterns — patterns.dev](https://www.patterns.dev/react/ai-ui-patterns/)
- Brave LLM Context API for web search grounding: [Brave Search API](https://brave.com/search/api/) | [Introducing AI Grounding — Brave](https://brave.com/blog/ai-grounding/) | [Brave LLM Context docs](https://api-dashboard.search.brave.com/documentation/services/llm-context)
- In-game file/config editor UX: [LethalConfig — Thunderstore](https://thunderstore.io/c/lethal-company/p/AinaVT/LethalConfig/) | [Minecraft InGameFileExplorer — GitHub](https://github.com/MrSpring/InGameFileExplorer)
- CS2 data panel mods (comparators): [Info Loom — Thunderstore](https://thunderstore.io/c/cities-skylines-ii/p/Infixo/Info_Loom/) | [Introducing City Monitor and HookUI — Paradox Forums](https://forum.paradoxplaza.com/forum/threads/introducing-city-monitor-and-hookui-mods.1609310/)
- Urban planning AI (real-world grounding reference): [AI for Urban Planning — ArchiVinci](https://www.archivinci.com/blogs/ai-for-urban-planning) | [UrbanSim](https://www.urbansim.com/)
- CS2 UI modding: [UI Modding — CS2 Wiki](https://cs2.paradoxwikis.com/UI_Modding)
- CityPlannerPlays channel context: [City Planner Plays](https://cityplannerplays.com/)
- Proactive AI agent design: [Proactive AI Agents — Lyzr](https://www.lyzr.ai/glossaries/proactive-ai-agents/) | [From Reactive to Proactive — Medium](https://medium.com/@manuedavakandam/from-reactive-to-proactive-how-to-build-ai-agents-that-take-initiative-10afd7a8e85d)
