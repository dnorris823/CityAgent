# Phase 6: Proactive Heartbeat - Context

**Gathered:** 2026-03-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a background timer-driven system (`HeartbeatSystem`) that periodically checks city data and
surfaces advisor observations in the chat panel — without the player sending a message. Heartbeat
messages appear as normal assistant bubbles in the existing `messagesJson` binding. No new React
bindings or UI components required beyond settings fields. The system is off by default; players
enable it and configure the interval in a new "Heartbeat" settings section.

</domain>

<decisions>
## Implementation Decisions

### Pipeline Architecture
- **D-01:** A dedicated `HeartbeatSystem` (`GameSystemBase`) owns the heartbeat request pipeline
  entirely. It has its own `volatile string? PendingHeartbeatResult` and `bool m_HeartbeatInFlight`
  flag — independent of `ClaudeAPISystem.PendingResult` and `m_RequestInFlight`. Chat and heartbeat
  are parallel, non-blocking pipelines.
- **D-02:** `CityAgentUISystem.OnUpdate` polls **both** `ClaudeAPISystem.PendingResult` and
  `HeartbeatSystem.PendingHeartbeatResult` each frame. Whichever is non-null gets appended to the
  message history and cleared. Both follow the existing `Interlocked.Exchange` pattern from Phase 1.
- **D-03:** Heartbeat results are pushed into `messagesJson` as normal assistant messages. No new
  C#↔JS bindings needed. React sees heartbeat messages as regular assistant bubbles — same markdown
  rendering, same bubble styling.

### Output Format
- **D-04:** Heartbeat messages are styled identically to user-triggered assistant messages —
  same bubble, same markdown rendering. No visual distinction. The advisor just speaks.
- **D-05:** Silence is prompt-driven. The `HeartbeatSystemPrompt` instructs the AI to return an
  empty string (or a designated sentinel like `"[silent]"`) when nothing notable warrants attention.
  `CityAgentUISystem` only appends non-empty, non-sentinel results. No C# pre-filter on city data.

### Data Scope
- **D-06:** ECS city data is always included in a heartbeat request (all enabled city data tools).
  Screenshot is optional — controlled by `HeartbeatIncludeScreenshot` bool in the Heartbeat
  settings section. When enabled, screenshot is captured and base64-encoded exactly as in chat.
- **D-07:** If `HeartbeatIncludeScreenshot` is true but a user-triggered screenshot capture is
  already in progress (`CityAgentUISystem.m_ScreenshotCapturePending` or equivalent flag is true),
  the heartbeat fires that cycle **without** the screenshot — no delay, no retry. The city data
  portion still goes through.

### Timing
- **D-08:** Timer uses wall-clock real time (`DateTime.UtcNow`). Fires every N real-world minutes
  regardless of game speed or pause state. `HeartbeatSystem` records `m_LastFireTime` in `OnCreate`
  and compares against `DateTime.UtcNow` in `OnUpdate`.
- **D-09:** Default interval: **5 minutes**. Configurable range: **1–60 minutes** via
  `HeartbeatIntervalMinutes` (int) in Settings. Off by default (`HeartbeatEnabled = false`).
  Both settings take effect without restarting the game (read from `Mod.ActiveSetting` each cycle).
- **D-10:** First fire is after a **full interval delay** from load or from when the player enables
  heartbeat. `m_LastFireTime` is initialized to `DateTime.UtcNow` in `OnCreate`. No immediate
  fire on game startup.
- **D-11:** If `m_HeartbeatInFlight` is true when the timer fires (prior heartbeat still processing),
  skip that cycle silently. Reset `m_LastFireTime` to `DateTime.UtcNow` so the next fire is after
  another full interval.

### Heartbeat Prompt Design
- **D-12:** `HeartbeatSystemPrompt` is a separate settings field (distinct from `SystemPrompt`).
  Default instructs: observe the city silently, surface only what's meaningfully notable, return
  empty/silent if nothing warrants attention. The same `NarrativeMemorySystem.GetAlwaysInjectedContext()`
  call prepends `_index.md` + `style-notes.md` to the heartbeat system prompt — same memory
  injection as regular chat.
- **D-13:** Heartbeat AI has access to the **full tool registry** — all city data tools and all
  memory tools. It can autonomously read and update narrative memory files during the tool-use loop.
  Memory file conflicts use last-write-wins (same strategy as existing chat writes).

### UI Thread Safety
- **D-14:** `HeartbeatSystem.PendingHeartbeatResult` is a public `volatile string?` field.
  `CityAgentUISystem` reads it via `Interlocked.Exchange` in `OnUpdate`. No delegate, no queue,
  no locking — same pattern as `ClaudeAPISystem.PendingResult`. `HeartbeatSystem` must be
  retrieved via `World.GetOrCreateSystemManaged<HeartbeatSystem>()` in `CityAgentUISystem.OnCreate`.

### Error Backoff
- **D-15:** On API error, `HeartbeatSystem` sets a `m_BackoffCycles` counter to **3**. Each time
  the timer would fire, if `m_BackoffCycles > 0`, decrement and skip. After 3 skipped cycles,
  resume normal interval. Errors are logged via `Mod.Log.Error` but are **not** surfaced in the
  chat panel (silent backoff).

### Memory Persistence
- **D-16:** Heartbeat observations are appended to the **current session file** (`session-NNN.md`)
  via `NarrativeMemorySystem.SaveChatSession()` — same as user/assistant messages. A heartbeat
  cycle where the AI returns silent/empty is not persisted (nothing to save).

### Claude's Discretion
- Exact `HeartbeatSystemPrompt` default text — planner writes a sensible default
- Whether `HeartbeatIncludeScreenshot` defaults to true or false (cost vs. richness tradeoff)
- Number of backoff skip cycles to hardcode (3 is the discussed default, planner confirms)
- Settings section order: Heartbeat is the 6th section after Web Search

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — HB-01, HB-02, HB-03 (heartbeat requirements); CORE-01, CORE-02
  (threading/safety); API-01–API-04 (provider routing that heartbeat must also follow)
- `.planning/ROADMAP.md` — Phase 6 goal and 4 success criteria

### Prior Phase Decisions (architecture continuity)
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — D-11 (async NarrativeMemory),
  D-12 (fire-and-forget writes), D-13 (screenshot background thread), D-14 (Interlocked.Exchange),
  D-06/D-07/D-08 (rate-limit fallback — heartbeat requests go through same provider routing)
- `.planning/phases/03-extended-city-data-tools/03-CONTEXT.md` — D-08/D-09 (per-tool toggles;
  heartbeat respects enabled/disabled state of each city data tool)
- `.planning/phases/04-web-search-tool/04-CONTEXT.md` — D-08/D-09 (settings section order)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ClaudeAPISystem.cs` — Template for `HeartbeatSystem`: same `GameSystemBase` pattern, same
  `volatile string? PendingResult` + `bool m_RequestInFlight` field pattern, same `RunRequestAsync`
  task pattern. Heartbeat system is effectively a clone with a different prompt and timer.
- `NarrativeMemorySystem.GetAlwaysInjectedContext()` — Called in `ClaudeAPISystem.RunRequestAsync`;
  heartbeat uses the same call to inject `_index.md` + `style-notes.md`.
- `CityToolRegistry` — Fully reusable. `HeartbeatSystem` creates its own registry instance (or
  shares one) with the same tool set.
- `Settings.cs` — All new heartbeat settings (`HeartbeatEnabled`, `HeartbeatIntervalMinutes`,
  `HeartbeatIncludeScreenshot`, `HeartbeatSystemPrompt`) added here following established field
  patterns with `[SettingsUISection]` and locale entries.

### Established Patterns
- `volatile string? PendingResult` + `Interlocked.Exchange` drain in UISystem — the only
  approved thread-safe handoff pattern in this codebase.
- `m_RequestInFlight` bool guards — must be reset in both success and error paths of `RunRequestAsync`.
- Settings read via `Mod.ActiveSetting.{Field}` (live-read each cycle, no caching).
- `Mod.Log.Error(...)` for all error paths; `Mod.Log.Info(...)` for key lifecycle events.

### Integration Points
- `CityAgentUISystem.OnUpdate` — Add poll for `HeartbeatSystem.PendingHeartbeatResult` alongside
  the existing `ClaudeAPISystem.PendingResult` poll.
- `Mod.cs` `OnLoad` — Schedule `HeartbeatSystem` via `updateSystem.World.GetOrCreateSystemManaged`.
- `CityAgentUISystem.OnCreate` — Retrieve `HeartbeatSystem` reference the same way
  `ClaudeAPISystem` retrieves `CityDataSystem`.

</code_context>

<specifics>
## Specific Ideas

- User's memory file (project_planned_features.md) described the heartbeat concept as a two-phase
  rollout: basic (ECS data only) → vision (with screenshot). Phase 6 ships both modes gated by
  the `HeartbeatIncludeScreenshot` setting, so the player controls which phase they're at.
- The heartbeat "feel" target: the advisor already knows what the player has been building when
  they open the chat — feels like a companion, not a chatbot.
- Silent gating sentinel: `"[silent]"` or empty string return from AI. `CityAgentUISystem` must
  handle both so the system prompt doesn't need to be exact.

</specifics>

<deferred>
## Deferred Ideas

- **Aggregation pre-filter (C# side)** — Whether to add a C# threshold check (e.g. skip if
  population delta < X%) before sending to AI. Not discussed in depth — left to AI's prompt-based
  judgment per the current design.
- **Settings field for backoff count** — Making the 3-cycle backoff configurable was not discussed.
  Hardcoded for Phase 6.
- **Separate heartbeat log file** — Keeping heartbeat observations out of session files to keep
  them "clean" was proposed but not chosen. Deferred as a v2 option.
- **Change detection (perceptual hash)** — Screenshot change detection mentioned in memory file
  (skip redundant frames). Not in Phase 6 scope — deferred to v2.
- **Minimum severity threshold** — HB-03 mentions aggregation; implementing a C# severity filter
  pre-AI was identified but not decided. Prompt-driven silence covers this in Phase 6.

</deferred>

---

*Phase: 06-proactive-heartbeat*
*Context gathered: 2026-03-28*
