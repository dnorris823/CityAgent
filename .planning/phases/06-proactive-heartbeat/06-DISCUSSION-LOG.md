# Phase 6: Proactive Heartbeat - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-28
**Phase:** 06-proactive-heartbeat
**Areas discussed:** API pipeline architecture, Output format, Data scope, Timing, Heartbeat prompt design, Thread-safe UI write, Error backoff, Screenshot conflict handling, Memory persistence

---

## API Pipeline Architecture

### Q1: How should the heartbeat API call coexist with user-initiated chat requests?

| Option | Description | Selected |
|--------|-------------|----------|
| Separate HeartbeatSystem | New GameSystemBase with its own volatile PendingResult and in-flight flag. Chat and heartbeat fully independent pipelines. | ✓ |
| Share ClaudeAPISystem | Heartbeat calls BeginRequest() on the existing system. If m_RequestInFlight is true, cycle is skipped. | |
| Queue in ClaudeAPISystem | Heartbeat requests queue behind active chat. | |

**User's choice:** Separate HeartbeatSystem
**Notes:** Full independence — user can chat while heartbeat processes in background.

---

### Q2: Where does the heartbeat result flow into the UI?

| Option | Description | Selected |
|--------|-------------|----------|
| Push into messagesJson | Append to same message history in CityAgentUISystem. No new React bindings. | ✓ |
| Dedicated heartbeat binding | New ValueBinding<string> heartbeatMessage shown in separate notification area. | |
| Claude's discretion | Planner decides binding wiring. | |

**User's choice:** Push into messagesJson
**Notes:** Heartbeat messages look like normal assistant messages — no new bindings needed.

---

## Output Format

### Q3: What role/style should heartbeat messages have in the chat?

| Option | Description | Selected |
|--------|-------------|----------|
| Styled as assistant | Same bubble, same markdown rendering. Advisor just speaks. | ✓ |
| Tagged system notice | Distinct visual treatment with Heartbeat label. | |
| Silent — memory only | No panel display; updates only narrative memory files. | |

**User's choice:** Styled as assistant
**Notes:** No visual distinction from user-triggered responses.

---

### Q4: When the heartbeat has nothing noteworthy to say?

| Option | Description | Selected |
|--------|-------------|----------|
| Silent skip — no message | AI returns empty/sentinel. UISystem only appends non-empty results. | ✓ |
| Always produce a message | Every cycle produces output, even if brief. | |
| Claude's discretion | Planner decides silence gating approach. | |

**User's choice:** Silent skip
**Notes:** Silence is prompt-driven — the AI is instructed to stay quiet when nothing is notable.

---

## Data Scope

### Q5: What does a heartbeat cycle send to the AI?

| Option | Description | Selected |
|--------|-------------|----------|
| ECS data only | Fast, cheap. Phase 1 of rollout. | |
| Screenshot + ECS data | Vision-enabled. Richer but heavier. Phase 2 of rollout. | |
| Configurable in settings | Toggle in Heartbeat section: player chooses cost vs. richness. | ✓ |

**User's choice:** Configurable in settings
**Notes:** HeartbeatIncludeScreenshot bool in the Heartbeat settings section. Both modes shipped in Phase 6.

---

### Q6: Where does the heartbeat screenshot setting live?

| Option | Description | Selected |
|--------|-------------|----------|
| In the Heartbeat settings section | All controls in one place: enabled, interval, include screenshot. | ✓ |
| Claude's discretion | Planner decides placement. | |

**User's choice:** Heartbeat settings section
**Notes:** Cohesive — all heartbeat configuration in one section.

---

## Timing

### Q7: What should "every N minutes" mean?

| Option | Description | Selected |
|--------|-------------|----------|
| Wall-clock real time | DateTime.UtcNow. Fires regardless of game speed or pause. | ✓ |
| Game simulation time | Frame-based, scales with game speed. | |
| Hybrid: wall-clock but pause-aware | Real time but paused frames don't count. Most complex. | |

**User's choice:** Wall-clock real time
**Notes:** Simpler and more predictable for players.

---

### Q8: Default interval and configurable range?

| Option | Description | Selected |
|--------|-------------|----------|
| 5 min default, 1–60 min range | Active without being spammy. Integer field in settings. | ✓ |
| 10 min default, 5–60 min range | More conservative default. | |
| Claude's discretion | Planner picks sensible values. | |

**User's choice:** 5 min default, 1–60 min range
**Notes:** HeartbeatIntervalMinutes setting. Off by default (HB-02).

---

### Q9: First fire delay on load?

| Option | Description | Selected |
|--------|-------------|----------|
| Full interval delay | Timer starts from zero on load. No immediate API call on startup. | ✓ |
| Immediate on load | Fires once immediately, then every N minutes. | |

**User's choice:** Full interval delay
**Notes:** m_LastFireTime initialized to DateTime.UtcNow in OnCreate.

---

### Q10: What happens when heartbeat fires but prior heartbeat is still in-flight?

| Option | Description | Selected |
|--------|-------------|----------|
| Skip that cycle | If m_HeartbeatInFlight is true, skip silently. Reset timer. | ✓ |
| Reset and retry soon | Reschedule shorter retry (e.g. 30s) instead of full interval. | |
| Claude's discretion | Planner decides in-flight guard behavior. | |

**User's choice:** Skip that cycle
**Notes:** Consistent with how chat handles concurrent requests.

---

## Heartbeat Prompt Design

### Q11: System prompt strategy?

| Option | Description | Selected |
|--------|-------------|----------|
| Separate heartbeat prompt, same memory injection | HeartbeatSystemPrompt field in Settings. Still injects _index.md + style-notes.md. | ✓ |
| Prefix appended to main system prompt | Reuses player's SystemPrompt with heartbeat prefix prepended. | |
| Claude's discretion | Planner decides. | |

**User's choice:** Separate heartbeat prompt, same memory injection
**Notes:** HeartbeatSystemPrompt setting with a default focused on silent observation.

---

### Q12: Which tools can the heartbeat AI call?

| Option | Description | Selected |
|--------|-------------|----------|
| All city data + memory tools | Full tool registry. AI can autonomously read/write memory. | ✓ |
| City data only — no memory writes | Safer, prevents autonomous memory modification. | |
| Claude's discretion | Planner decides tool access. | |

**User's choice:** All city data + memory tools
**Notes:** Last-write-wins for memory conflicts — same as existing chat behavior.

---

## Thread-Safe UI Write

### Q13: How does HeartbeatSystem hand its result to CityAgentUISystem?

| Option | Description | Selected |
|--------|-------------|----------|
| Public volatile field on UISystem | HeartbeatSystem.PendingHeartbeatResult polled by CityAgentUISystem.OnUpdate. | ✓ |
| Callback delegate on UISystem | HeartbeatSystem calls a registered Action<string>. | |
| Shared message queue | Static ConcurrentQueue<ChatMessage> drained in OnUpdate. | |

**User's choice:** Public volatile field on UISystem
**Notes:** Minimal new API surface, consistent with existing ClaudeAPISystem.PendingResult pattern.

---

## Error Backoff

### Q14: Backoff strategy on API error?

| Option | Description | Selected |
|--------|-------------|----------|
| Skip N cycles then resume | Set m_BackoffCycles = 3. Decrement each cycle. Resume after 3 skips. | ✓ |
| Double interval temporarily | Cap at max. Revert on success. | |
| Log and continue normally | No backoff. Next cycle fires at normal interval. | |

**User's choice:** Skip N cycles then resume
**Notes:** Errors logged, not shown in panel. 3 skip cycles (hardcoded for Phase 6).

---

## Screenshot Conflict Handling

### Q15: Heartbeat screenshot vs. user-triggered screenshot coexistence?

| Option | Description | Selected |
|--------|-------------|----------|
| Heartbeat waits — skip screenshot if capture in progress | Check m_ScreenshotCapturePending. If true, fire without screenshot that cycle. | ✓ |
| Heartbeat captures independently | Separate file path for heartbeat screenshots. | |
| Claude's discretion | Planner investigates CS2 API limitations. | |

**User's choice:** Heartbeat waits
**Notes:** City data portion still fires — only the screenshot is skipped that cycle.

---

## Memory Persistence

### Q16: Are heartbeat observations persisted to session files?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — save to current session file | Appended to session-NNN.md. Full history replay includes proactive observations. | ✓ |
| No — panel only, not persisted | Disappear on game reload. | |
| Separate heartbeat log file | heartbeat-log.md in memory. Keeps session files clean. | |

**User's choice:** Yes — save to current session file
**Notes:** Silent/empty heartbeat cycles are not persisted.

---

## Claude's Discretion

- Exact HeartbeatSystemPrompt default text
- Whether HeartbeatIncludeScreenshot defaults to true or false
- Exact backoff skip count (3 discussed)
- Settings section order (Heartbeat = 6th section after Web Search)

## Deferred Ideas

- C# pre-filter on city data delta before sending to AI (aggregation threshold)
- Making backoff count configurable in settings
- Separate heartbeat-log.md (keep session files clean)
- Perceptual hash screenshot change detection (skip redundant frames)
- Minimum severity threshold as a C# filter rather than prompt-based
