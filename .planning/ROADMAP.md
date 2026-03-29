# Roadmap: CityAgent

## Overview

CityAgent ships in six phases, each delivering a coherent capability. The existing codebase is well-scaffolded but the API layer is broken — every feature depends on a working Claude API call, so migration comes first. From there: chat UI polish before adding new content surfaces, extended ECS tools and web search to raise advisor quality, a memory file explorer for player trust and control, and finally the proactive heartbeat once all foundations are stable.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: API Migration & Core Stability** - Rewrite ClaudeAPISystem to correct Claude API format; fix threading and race conditions; validate end-to-end
- [ ] **Phase 2: Chat UI Polish** - Distinct message bubbles, correct markdown rendering, and a loading indicator — polish the panel before adding new content surfaces
- [ ] **Phase 3: Extended City Data Tools** - Add budget, traffic, and services ECS tools; expose per-tool enable/disable toggles to give the advisor richer structured context
- [ ] **Phase 4: Web Search Tool** - Brave Search API integration as an agent tool; lets Claude ground recommendations in real urban planning sources
- [ ] **Phase 5: Memory File Explorer** - In-panel file tree for browsing, viewing, editing, and deleting per-city narrative memory files
- [ ] **Phase 6: Proactive Heartbeat** - Background periodic advisor check: surfaces noteworthy city events without the player asking

## Phase Details

### Phase 1: API Migration & Core Stability
**Goal**: The Claude API works correctly end-to-end — tool calls fire, screenshots are sent, memory is written, responses render
**Depends on**: Nothing (first phase)
**Requirements**: CORE-01, CORE-02, CORE-03, API-01, API-02, API-03, API-04
**Success Criteria** (what must be TRUE):
  1. Player sends a message in-game, Claude responds with narrative commentary — no HTTP 400 errors
  2. A screenshot of the current city is included in the API request and Claude can describe what it sees
  3. Claude invokes at least one agent tool (e.g., population) and the tool result is included in its final response
  4. Player can switch between Claude API and Ollama endpoint in mod settings without restarting the game
  5. File writes (memory log, chat session) complete on a background thread — no visible game stutter during save
**Plans:** 4/4 plans executed
Plans:
- [x] 01-01-PLAN.md — Settings refactor: Claude API primary + Ollama Fallback sections
- [x] 01-02-PLAN.md — NarrativeMemorySystem async refactor + tool class updates
- [x] 01-03-PLAN.md — ClaudeAPISystem rewrite: Anthropic format + Ollama fallback + thread safety
- [x] 01-04-PLAN.md — CityAgentUISystem wiring: Interlocked drain, async persistence, background screenshot

### Phase 2: Chat UI Polish
**Goal**: The chat panel looks and behaves like a polished in-game advisor interface
**Depends on**: Phase 1
**Requirements**: UI-01, UI-02, UI-03
**Success Criteria** (what must be TRUE):
  1. User messages and Claude responses are visually distinct — different alignment, color, or bubble style makes the conversation easy to scan
  2. Claude's formatted responses display as rendered text — headers appear as headers, bold text is bold, bullet lists are indented, no raw asterisks visible
  3. A loading or thinking indicator is visible from the moment a message is sent until the response appears
**Plans:** 2/3 plans executed
Plans:
- [x] 02-01-PLAN.md — C# data layer: error role promotion + thinking strip + API payload filter
- [x] 02-02-PLAN.md — React UI: 3-way message renderer, loading status text, type-ahead queue, welcome block
- [x] 02-03-PLAN.md — renderMarkdown fixes: nested lists, italic regex, code language label

### Phase 3: Extended City Data Tools
**Goal**: The advisor has structured access to budget, traffic, and services data — and the player controls which tools are active
**Depends on**: Phase 2
**Requirements**: DATA-01, DATA-02, DATA-03, DATA-04, DATA-05
**Success Criteria** (what must be TRUE):
  1. Player asks about city finances and Claude returns specific numbers — income, expenses, and balance from ECS
  2. Player asks about traffic and Claude describes current congestion conditions using live ECS data
  3. Player asks about city services and Claude identifies coverage gaps using ECS health, education, and deathcare data
  4. Player can toggle individual data tools on or off in mod settings — disabled tools are not included in any API call
**Plans:** 3 plans
Plans:
- [ ] 03-01-PLAN.md — CityDataSystem ECS expansion + budget/traffic/services tool implementations
- [ ] 03-02-PLAN.md — Settings Data Tools toggles + CityToolRegistry filtering + tool registration + system prompt
- [ ] 03-03-PLAN.md — Build, deploy, and in-game verification of all new tools and toggles

### Phase 4: Web Search Tool
**Goal**: Claude can fetch real-world urban planning information on demand during a conversation
**Depends on**: Phase 1
**Requirements**: SRCH-01, SRCH-02, SRCH-03
**Success Criteria** (what must be TRUE):
  1. Player asks a question about real urban planning (e.g., "how do cities reduce highway noise?") and Claude's response cites or references external sources retrieved via search
  2. Brave Search API key is configurable in mod settings as a separate field from the Anthropic API key
  3. Claude decides autonomously when to invoke web search — the player does not need to trigger it explicitly
**Plans:** 2 plans
Plans:
- [ ] 04-01-PLAN.md — Settings Web Search section + SearchWebTool implementation
- [ ] 04-02-PLAN.md — Tool registration wiring + system prompt extension + in-game verification

### Phase 5: Memory File Explorer
**Goal**: Players can browse, read, edit, and delete the per-city narrative memory files directly from the in-game panel
**Depends on**: Phase 2
**Requirements**: MEM-01, MEM-02, MEM-03, MEM-04
**Success Criteria** (what must be TRUE):
  1. Player opens the memory explorer tab and sees all per-city narrative memory files organized in a directory tree
  2. Player clicks a file and its full contents appear in the panel without leaving the game
  3. Player edits a file's contents in the panel and saves — the change persists to disk and is reflected in Claude's next response
  4. Player attempts to delete a protected core file and is prevented — a clear message explains it is read-only
**Plans:** 3 plans
Plans:
- [ ] 05-01-PLAN.md — C# backend: ListFiles() field extension + memory explorer bindings and triggers
- [ ] 05-02-PLAN.md — React UI: tab navigation, file list, file viewer, edit mode, delete confirmation, CSS
- [ ] 05-03-PLAN.md — Full build + in-game verification checkpoint

### Phase 6: Proactive Heartbeat
**Goal**: The advisor surfaces noteworthy city events in the background — without the player having to ask
**Depends on**: Phase 3, Phase 4
**Requirements**: HB-01, HB-02, HB-03
**Success Criteria** (what must be TRUE):
  1. After the configured interval elapses, an advisor message appears in the chat panel describing a city condition worth attention — without the player sending any message
  2. Heartbeat is off by default; player enables it and sets the interval in mod settings, and the change takes effect without restarting the game
  3. When multiple city issues exist in one heartbeat cycle, they are aggregated into a single advisor message — not a flood of individual alerts
  4. When the API returns an error during a heartbeat cycle, the system backs off and retries later rather than flooding the error log
**Plans:** 3 plans
Plans:
- [ ] 06-01-PLAN.md — Settings heartbeat fields + Mod.cs HeartbeatSystem scheduling
- [ ] 06-02-PLAN.md — HeartbeatSystem implementation: timer, async dispatch, backoff, tool loop
- [ ] 06-03-PLAN.md — CityAgentUISystem integration: dual pipeline drain + silence filter + build verification

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

Note: Phase 4 depends only on Phase 1 (not Phase 2 or 3) and could execute in parallel with Phases 2-3 if needed.

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. API Migration & Core Stability | 1/4 | In Progress|  |
| 2. Chat UI Polish | 2/3 | In Progress|  |
| 3. Extended City Data Tools | 0/3 | Planning complete | - |
| 4. Web Search Tool | 0/2 | Planning complete | - |
| 5. Memory File Explorer | 0/3 | Planning complete | - |
| 6. Proactive Heartbeat | 0/3 | Planning complete | - |
