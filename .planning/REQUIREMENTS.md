# Requirements: CityAgent

**Defined:** 2026-03-26
**Core Value:** You ask Claude something about your city, it sees the current screenshot and live stats, and responds with narrative commentary that remembers where the city has been — in a polished chat panel that feels like it belongs in the game.

## v1 Requirements

### Core Stability

- [ ] **CORE-01**: File writes (narrative log, chat session, screenshot encode) execute off the main game thread — no UI freezes during I/O
- [ ] **CORE-02**: Concurrent API requests are safe — `PendingResult` uses `Interlocked.Exchange` to prevent race conditions when heartbeat adds parallel requests
- [ ] **CORE-03**: End-to-end tested — full build → deploy → in-game cycle passes with screenshot, tool calls, narrative memory, and response rendering working together

### API Integration

- [ ] **API-01**: Claude API (`/v1/messages`) format is fully supported — correct headers (`x-api-key`, `anthropic-version`), system as top-level field, image content blocks, `tool_result` as user role with `tool_use_id`
- [ ] **API-02**: Ollama API (`/v1/chat/completions`) format is supported alongside Claude — user can select provider in settings
- [ ] **API-03**: Automatic rate-limit fallback — when Claude returns HTTP 429, system retries with configured Ollama endpoint (or surfaces clear error if no fallback configured)
- [ ] **API-04**: User-configurable model name in mod settings — changes take effect without restarting the game

### Chat UI

- [ ] **UI-01**: User and assistant messages are visually distinct — different bubble styles, alignment, or color differentiation
- [ ] **UI-02**: Claude responses render markdown correctly — headers, bold, italic, unordered lists, code blocks display as formatted text (no raw asterisks)
- [ ] **UI-03**: Loading / thinking indicator is visible while Claude generates a response — animation or status text in the panel

### City Data Tools

- [ ] **DATA-01**: `get_budget()` tool returns city financial data — income, expenses, current balance from ECS
- [ ] **DATA-02**: `get_traffic_summary()` tool returns traffic conditions — congestion level or flow indicator from ECS
- [ ] **DATA-03**: `get_services_summary()` tool returns city service coverage levels from ECS
- [ ] **DATA-04**: All available ECS data is exposed as agent tools — additional data surfaces (noise, pollution, land value, happiness, etc.) are implemented as tools when ECS queries are confirmed available
- [ ] **DATA-05**: Mod settings include per-tool enable/disable toggles — user can turn off individual data tools to manage context window usage and cost

### Web Search

- [ ] **SRCH-01**: `search_web(query)` agent tool calls Brave Search API from C# backend and returns relevant results to Claude
- [ ] **SRCH-02**: Brave Search API key is configurable in mod settings (separate field from Anthropic API key)
- [ ] **SRCH-03**: Claude automatically uses web search when answering questions about real-world urban planning — system prompt instructs when to search

### Memory File Explorer

- [ ] **MEM-01**: In-panel file tree view displays all per-city narrative memory files organized by directory
- [ ] **MEM-02**: User can click any file in the tree to view its full contents in the panel
- [ ] **MEM-03**: User can edit file contents directly in the panel and save changes back to disk
- [ ] **MEM-04**: User can delete non-protected memory files from the panel (protected core files are read-only)

### Proactive Heartbeat

- [ ] **HB-01**: Background periodic system checks city data every N minutes and surfaces noteworthy events, anomalies, or suggestions as advisor messages
- [ ] **HB-02**: Heartbeat interval and on/off toggle are configurable in mod settings — off by default
- [ ] **HB-03**: Heartbeat aggregates multiple issues into a single advisor message per cycle — minimum severity threshold filters minor events

## v2 Requirements

### Distribution

- **DIST-01**: Mod packaged for Paradox Mods public release with install documentation
- **DIST-02**: Automated build pipeline for mod release artifacts

### Cost Management

- **COST-01**: Estimated API cost counter visible in settings or panel
- **COST-02**: Configurable monthly spend limit with auto-pause

### Advanced Memory

- **MEM-05**: Memory file diff view — see what Claude changed in a memory file after a session
- **MEM-06**: Memory snapshots — save and restore the full memory state for a city

### Enhanced Persona

- **PERSONA-01**: Configurable advisor personality profiles (e.g., serious urban planner vs. enthusiastic narrator)
- **PERSONA-02**: Multi-language support for advisor responses

## Out of Scope

| Feature | Reason |
|---------|--------|
| Multiplayer / shared sessions | Single-player advisor; city story is personal — multi-user ownership is undefined |
| Auto-play / city control | Claude advises and narrates; it never places zones, roads, or buildings — safety boundary |
| Non-Anthropic model API keys via v1 UI | Ollama (local, no key) and Brave Search (separate key) are the only non-Anthropic endpoints needed in v1 |
| Paradox Mods public distribution | Validate experience for personal use first; publishing adds distribution complexity too early |
| CSS Grid in Coherent GT UI | Coherent GT (CS2's embedded browser) does not support CSS Grid — all layout must be flexbox |

## Traceability

*Populated during roadmap creation (2026-03-26).*

| Requirement | Phase | Status |
|-------------|-------|--------|
| CORE-01 | Phase 1 | Pending |
| CORE-02 | Phase 1 | Pending |
| CORE-03 | Phase 1 | Pending |
| API-01 | Phase 1 | Pending |
| API-02 | Phase 1 | Pending |
| API-03 | Phase 1 | Pending |
| API-04 | Phase 1 | Pending |
| UI-01 | Phase 2 | Pending |
| UI-02 | Phase 2 | Pending |
| UI-03 | Phase 2 | Pending |
| DATA-01 | Phase 3 | Pending |
| DATA-02 | Phase 3 | Pending |
| DATA-03 | Phase 3 | Pending |
| DATA-04 | Phase 3 | Pending |
| DATA-05 | Phase 3 | Pending |
| SRCH-01 | Phase 4 | Pending |
| SRCH-02 | Phase 4 | Pending |
| SRCH-03 | Phase 4 | Pending |
| MEM-01 | Phase 5 | Pending |
| MEM-02 | Phase 5 | Pending |
| MEM-03 | Phase 5 | Pending |
| MEM-04 | Phase 5 | Pending |
| HB-01 | Phase 6 | Pending |
| HB-02 | Phase 6 | Pending |
| HB-03 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 25 total
- Mapped to phases: 25
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-26*
*Last updated: 2026-03-26 — traceability populated after roadmap creation*
