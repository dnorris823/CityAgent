# Phase 6: Proactive Heartbeat — Research

**Researched:** 2026-03-28
**Domain:** CS2 mod background timer system, C# async patterns, Unity ECS/game thread safety
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** A dedicated `HeartbeatSystem` (`GameSystemBase`) owns the heartbeat request pipeline entirely. It has its own `volatile string? PendingHeartbeatResult` and `bool m_HeartbeatInFlight` flag — independent of `ClaudeAPISystem.PendingResult` and `m_RequestInFlight`. Chat and heartbeat are parallel, non-blocking pipelines.
- **D-02:** `CityAgentUISystem.OnUpdate` polls **both** `ClaudeAPISystem.PendingResult` and `HeartbeatSystem.PendingHeartbeatResult` each frame. Whichever is non-null gets appended to the message history and cleared. Both follow the existing `Interlocked.Exchange` pattern from Phase 1.
- **D-03:** Heartbeat results are pushed into `messagesJson` as normal assistant messages. No new C#↔JS bindings needed. React sees heartbeat messages as regular assistant bubbles — same markdown rendering, same bubble styling.
- **D-04:** Heartbeat messages are styled identically to user-triggered assistant messages — same bubble, same markdown rendering. No visual distinction. The advisor just speaks.
- **D-05:** Silence is prompt-driven. The `HeartbeatSystemPrompt` instructs the AI to return an empty string (or a designated sentinel like `"[silent]"`) when nothing notable warrants attention. `CityAgentUISystem` only appends non-empty, non-sentinel results. No C# pre-filter on city data.
- **D-06:** ECS city data is always included in a heartbeat request (all enabled city data tools). Screenshot is optional — controlled by `HeartbeatIncludeScreenshot` bool in the Heartbeat settings section. When enabled, screenshot is captured and base64-encoded exactly as in chat.
- **D-07:** If `HeartbeatIncludeScreenshot` is true but a user-triggered screenshot capture is already in progress (`m_ScreenshotCapturePending` or equivalent flag is true), the heartbeat fires that cycle **without** the screenshot — no delay, no retry. The city data portion still goes through.
- **D-08:** Timer uses wall-clock real time (`DateTime.UtcNow`). Fires every N real-world minutes regardless of game speed or pause state. `HeartbeatSystem` records `m_LastFireTime` in `OnCreate` and compares against `DateTime.UtcNow` in `OnUpdate`.
- **D-09:** Default interval: **5 minutes**. Configurable range: **1–60 minutes** via `HeartbeatIntervalMinutes` (int) in Settings. Off by default (`HeartbeatEnabled = false`). Both settings take effect without restarting the game (read from `Mod.ActiveSetting` each cycle).
- **D-10:** First fire is after a **full interval delay** from load or from when the player enables heartbeat. `m_LastFireTime` is initialized to `DateTime.UtcNow` in `OnCreate`. No immediate fire on game startup.
- **D-11:** If `m_HeartbeatInFlight` is true when the timer fires (prior heartbeat still processing), skip that cycle silently. Reset `m_LastFireTime` to `DateTime.UtcNow` so the next fire is after another full interval.
- **D-12:** `HeartbeatSystemPrompt` is a separate settings field (distinct from `SystemPrompt`). Default instructs: observe the city silently, surface only what's meaningfully notable, return empty/silent if nothing warrants attention. The same `NarrativeMemorySystem.GetAlwaysInjectedContext()` call prepends `_index.md` + `style-notes.md` to the heartbeat system prompt — same memory injection as regular chat.
- **D-13:** Heartbeat AI has access to the **full tool registry** — all city data tools and all memory tools. It can autonomously read and update narrative memory files during the tool-use loop. Memory file conflicts use last-write-wins (same strategy as existing chat writes).
- **D-14:** `HeartbeatSystem.PendingHeartbeatResult` is a public `volatile string?` field. `CityAgentUISystem` reads it via `Interlocked.Exchange` in `OnUpdate`. No delegate, no queue, no locking — same pattern as `ClaudeAPISystem.PendingResult`. `HeartbeatSystem` must be retrieved via `World.GetOrCreateSystemManaged<HeartbeatSystem>()` in `CityAgentUISystem.OnCreate`.
- **D-15:** On API error, `HeartbeatSystem` sets a `m_BackoffCycles` counter to **3**. Each time the timer would fire, if `m_BackoffCycles > 0`, decrement and skip. After 3 skipped cycles, resume normal interval. Errors are logged via `Mod.Log.Error` but are **not** surfaced in the chat panel (silent backoff).
- **D-16:** Heartbeat observations are appended to the **current session file** (`session-NNN.md`) via `NarrativeMemorySystem.SaveChatSession()` — same as user/assistant messages. A heartbeat cycle where the AI returns silent/empty is not persisted (nothing to save).

### Claude's Discretion
- Exact `HeartbeatSystemPrompt` default text — planner writes a sensible default
- Whether `HeartbeatIncludeScreenshot` defaults to true or false (cost vs. richness tradeoff)
- Number of backoff skip cycles to hardcode (3 is the discussed default, planner confirms)
- Settings section order: Heartbeat is the 6th section after Web Search

### Deferred Ideas (OUT OF SCOPE)
- C# pre-filter on city data delta before sending to AI (aggregation threshold)
- Making backoff count configurable in settings
- Separate heartbeat-log.md (keep session files clean)
- Perceptual hash screenshot change detection
- Minimum severity threshold as a C# filter rather than prompt-based
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HB-01 | Background periodic system checks city data every N minutes and surfaces noteworthy events, anomalies, or suggestions as advisor messages | `HeartbeatSystem` (`GameSystemBase`) with `DateTime.UtcNow` wall-clock timer, async `RunHeartbeatAsync`, drains into `messagesJson` via UISystem poll |
| HB-02 | Heartbeat interval and on/off toggle are configurable in mod settings — off by default | New `kHeartbeatGroup` settings section: `HeartbeatEnabled` (bool, default false), `HeartbeatIntervalMinutes` (int, 1–60, default 5); live-read from `Mod.ActiveSetting` each cycle |
| HB-03 | Heartbeat aggregates multiple issues into a single advisor message per cycle — minimum severity threshold filters minor events | Aggregation is prompt-driven: single API call per cycle returns one message; AI is instructed to consolidate all observations; C# makes no pre-filter decisions |
</phase_requirements>

---

## Summary

Phase 6 adds a background advisor that speaks without being asked. The architecture is a new `HeartbeatSystem` (`GameSystemBase`) that mirrors `ClaudeAPISystem` in structure: a wall-clock timer in `OnUpdate`, an async `RunHeartbeatAsync` task, and a `volatile string? PendingHeartbeatResult` field drained by `CityAgentUISystem.OnUpdate` via `Interlocked.Exchange`. The system is entirely independent of the existing chat pipeline — two parallel `volatile` result fields are polled each frame, and whichever is non-null first gets appended to `messagesJson`.

The implementation is about 90% a structural clone of `ClaudeAPISystem.cs` with three key differences: (1) no user message — the heartbeat prompt is the system prompt; (2) a timer guard in `OnUpdate` instead of a JS-triggered `BeginRequest`; (3) a silence sentinel check that suppresses empty responses before they reach the chat history. Screenshot capture for heartbeats reuses the existing file-path poll pattern from `CityAgentUISystem`, with a conflict-avoidance check against the user-triggered capture flag.

Error backoff is a simple counter: three consecutive API errors set `m_BackoffCycles = 3`, which decrements silently each would-be fire, resuming normal cadence after three skipped cycles. Errors are logged but never shown in the panel — heartbeat is a background companion, not an alert system.

**Primary recommendation:** Implement `HeartbeatSystem` as a near-clone of `ClaudeAPISystem` with a timer, add four settings fields, add one poll branch to `CityAgentUISystem.OnUpdate`, and schedule the system in `Mod.cs`. No React changes required.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System.Threading` (`Interlocked`) | .NET Standard 2.1 built-in | Atomic result handoff between thread-pool task and game main thread | The only approved thread-safety pattern in this codebase (D-14 / Phase 1 D-14) |
| `System.DateTime` (`UtcNow`) | .NET Standard 2.1 built-in | Wall-clock timer comparison in `OnUpdate` | Simpler than frame counting; immune to game speed/pause; already familiar pattern |
| `System.Threading.Tasks.Task` | .NET Standard 2.1 built-in | Fire-and-forget async API call from game thread | Existing pattern in `ClaudeAPISystem.RunRequestAsync` |
| `Game.GameSystemBase` | CS2 game DLL | Base class for all non-UI game systems | Required by CS2 mod system to schedule `OnUpdate` in game simulation phase |
| `Colossal.Logging.ILog` / `Mod.Log` | CS2 game DLL | Logging | Single logger for entire mod; all systems use it |
| `Newtonsoft.Json` / `JObject` | Bundled with CS2 | JSON request construction for API payloads | Game-bundled version; already used by `ClaudeAPISystem` |
| `System.Net.Http.HttpClient` | .NET Standard 2.1 built-in | Async HTTP for Anthropic API call | Static `HttpClient` is reused — `HeartbeatSystem` should share the same instance or declare its own `static readonly` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `UnityEngine.ScreenCapture` | Unity 2022.3.7f1 | Screenshot capture for vision-enabled heartbeats | Only when `HeartbeatIncludeScreenshot` is true and no user capture is in progress |
| `System.IO.File` | .NET Standard 2.1 | Read screenshot file from disk (async path) | Screenshot file written by Unity at end of frame; read in background thread |
| `System.Convert.ToBase64String` | .NET Standard 2.1 | Encode PNG bytes for API image content block | Same call chain as existing chat screenshot flow |
| `Game.Settings.ModSetting` | CS2 game DLL | Live-read of all heartbeat settings | Read `Mod.ActiveSetting` every heartbeat cycle — no caching, changes take effect immediately |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `DateTime.UtcNow` comparison | `UnityEngine.Time.realtimeSinceStartup` | Unity time API may not be accessible from `GameSystemBase`; `DateTime.UtcNow` is pure .NET, always safe |
| `volatile string?` + `Interlocked.Exchange` | `System.Collections.Concurrent.ConcurrentQueue<string>` | Queue is overkill for one pending result; existing pattern is simpler and already proven |
| Separate `HttpClient` instance | Sharing `ClaudeAPISystem.s_Http` | Sharing would require exposing a static field or property; separate static instance is cleaner isolation |

**Installation:** No new packages. All dependencies are game-bundled DLLs or .NET Standard 2.1 built-ins.

---

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Mod.cs                          # +1 line: schedule HeartbeatSystem
├── Settings.cs                     # +4 fields: HeartbeatEnabled, HeartbeatIntervalMinutes,
│                                   #   HeartbeatIncludeScreenshot, HeartbeatSystemPrompt
│                                   # +1 settings group: kHeartbeatGroup
│                                   # +locale entries for all new fields
└── Systems/
    ├── CityAgentUISystem.cs        # +1 field: m_HeartbeatSystem reference
    │                               # +1 poll branch in OnUpdate for PendingHeartbeatResult
    │                               # +screenshot conflict flag exposure
    └── HeartbeatSystem.cs          # NEW — ~200 lines, mirrors ClaudeAPISystem structure
```

### Pattern 1: HeartbeatSystem — Timer Guard + Async Dispatch
**What:** `OnUpdate` checks wall-clock elapsed time and backoff state before firing an async request.
**When to use:** Any background periodic task that must not block the game thread.

```csharp
// Source: mirrors ClaudeAPISystem.cs pattern in this codebase
protected override void OnUpdate()
{
    var setting = Mod.ActiveSetting;
    if (setting == null || !setting.HeartbeatEnabled) return;

    // Backoff: skip N cycles after an error
    if (m_BackoffCycles > 0)
    {
        m_BackoffCycles--;
        m_LastFireTime = DateTime.UtcNow; // reset interval from now
        return;
    }

    // In-flight guard: don't stack requests
    if (m_HeartbeatInFlight) return;

    // Timer check: wall-clock elapsed
    double minutesElapsed = (DateTime.UtcNow - m_LastFireTime).TotalMinutes;
    if (minutesElapsed < setting.HeartbeatIntervalMinutes) return;

    m_LastFireTime = DateTime.UtcNow;
    m_HeartbeatInFlight = true;
    PendingHeartbeatResult = null;

    // Screenshot conflict check (D-07)
    string? base64Png = null;
    if (setting.HeartbeatIncludeScreenshot && !m_UISystem.IsScreenshotCapturePending)
        base64Png = CaptureHeartbeatScreenshot(); // or reuse pending screenshot

    _ = RunHeartbeatAsync(base64Png);
}
```

### Pattern 2: PendingHeartbeatResult Drain in CityAgentUISystem
**What:** `OnUpdate` polls two `volatile` fields — chat and heartbeat — and drains whichever is non-null.
**When to use:** Whenever a background async system needs to deliver results to the main game thread.

```csharp
// Source: extends existing PendingResult drain block in CityAgentUISystem.OnUpdate
// Drain chat API result (existing)
string? chatResult = System.Threading.Interlocked.Exchange(ref m_ClaudeAPI.PendingResult, null);
if (chatResult != null && !IsHeartbeatSilent(chatResult))
{
    m_History.Add(new ChatMessage { role = "assistant", content = chatResult });
    PushMessagesBinding();
    m_IsLoading.Update(false);
    PersistChatSession();
}

// Drain heartbeat result (NEW)
string? hbResult = System.Threading.Interlocked.Exchange(ref m_HeartbeatSystem.PendingHeartbeatResult, null);
if (hbResult != null && !IsHeartbeatSilent(hbResult))
{
    m_History.Add(new ChatMessage { role = "assistant", content = hbResult });
    PushMessagesBinding();
    PersistChatSession();
}

// Helper: treat empty string and "[silent]" sentinel as silence
private static bool IsHeartbeatSilent(string result) =>
    string.IsNullOrWhiteSpace(result) ||
    result.Equals("[silent]", StringComparison.OrdinalIgnoreCase);
```

**Note:** `PendingResult` on `ClaudeAPISystem` is currently assigned directly (`m_ClaudeAPI.PendingResult = null`) rather than via `Interlocked.Exchange`. Phase 1 (D-14) is supposed to fix this. The planner must ensure the `Interlocked.Exchange` pattern is used consistently for both fields in Phase 6, regardless of whether Phase 1 has been executed yet. If Phase 1 has not run, the drain logic for `ClaudeAPISystem.PendingResult` must also be updated as part of Phase 6 to maintain consistency — or explicitly confirm Phase 1 landed first.

### Pattern 3: Backoff Counter
**What:** Increment `m_BackoffCycles` on error; decrement and skip each subsequent `OnUpdate` fire; resume when zero.
**When to use:** Transient error protection for any periodic system.

```csharp
// In RunHeartbeatAsync catch block:
catch (Exception ex)
{
    Mod.Log.Error($"[HeartbeatSystem] RunHeartbeatAsync error: {ex}");
    m_BackoffCycles = 3; // D-15: 3 skip cycles on error; not surfaced to panel
}
finally
{
    m_HeartbeatInFlight = false;
}
```

### Pattern 4: Settings Group Registration
**What:** New `kHeartbeatGroup` constant + `[SettingsUISection(kSection, kHeartbeatGroup)]` attributes on heartbeat fields.
**When to use:** Any new logical group of settings fields.

```csharp
// Source: Settings.cs existing pattern
public const string kHeartbeatGroup = "Heartbeat";

// In [SettingsUIGroupOrder]: add kHeartbeatGroup after Web Search group
// In LocaleEN.ReadEntries: add locale keys for all new fields

[SettingsUISection(kSection, kHeartbeatGroup)]
public bool HeartbeatEnabled { get; set; } = false;

[SettingsUISection(kSection, kHeartbeatGroup)]
[SettingsUISlider(min = 1, max = 60, step = 1)]
public int HeartbeatIntervalMinutes { get; set; } = 5;

[SettingsUISection(kSection, kHeartbeatGroup)]
public bool HeartbeatIncludeScreenshot { get; set; } = false; // Claude's discretion: default false (cost)

[SettingsUISection(kSection, kHeartbeatGroup)]
[SettingsUITextInput]
public string HeartbeatSystemPrompt { get; set; } = DefaultHeartbeatSystemPrompt;
```

### Pattern 5: HeartbeatSystem Registration in Mod.cs
**What:** Schedule `HeartbeatSystem` in `OnLoad` via `UpdateSystem`, mirroring `ClaudeAPISystem`.
**When to use:** Every new `GameSystemBase` subclass.

```csharp
// Source: Mod.cs existing pattern
updateSystem.UpdateAt<Systems.HeartbeatSystem>(SystemUpdatePhase.GameSimulation);
```

### Pattern 6: Screenshot Conflict Avoidance (D-07)
**What:** `CityAgentUISystem` exposes a read-only flag `IsScreenshotCapturePending` that `HeartbeatSystem` checks before initiating its own capture.
**When to use:** Any system that needs to share the screenshot pipeline.

```csharp
// In CityAgentUISystem — expose the existing m_ScreenshotWaitFrames state:
public bool IsScreenshotCapturePending => m_ScreenshotWaitFrames >= 0;
```

**Important:** `HeartbeatSystem` reads this flag on the game main thread (inside `OnUpdate`). The flag itself lives on `CityAgentUISystem`, which also runs on the main thread (`SystemUpdatePhase.UIUpdate`). This is safe — no cross-thread read concern here.

### Anti-Patterns to Avoid
- **Polling `Mod.ActiveSetting` and caching it:** Settings must be live-read each cycle per D-09. Do not cache `HeartbeatEnabled` or `HeartbeatIntervalMinutes` in `OnCreate`.
- **Resetting `m_LastFireTime` on settings change detection:** The design (D-09) does not require a special reset when settings change. `OnUpdate` reads settings live — if the player disables heartbeat, the `if (!setting.HeartbeatEnabled) return` guard handles it immediately.
- **Surfacing heartbeat errors to the panel:** D-15 is explicit — errors are logged, not shown. Do not write heartbeat errors to `PendingHeartbeatResult`.
- **Firing heartbeat on first `OnUpdate`:** D-10 requires `m_LastFireTime = DateTime.UtcNow` in `OnCreate` so the first fire is after a full interval.
- **Using `m_IsLoading` for heartbeat:** The loading indicator is for user-triggered chat. Heartbeat is invisible in progress — no binding change, no spinner.
- **Sharing `ClaudeAPISystem.m_ToolRegistry`:** `HeartbeatSystem` constructs its own `CityToolRegistry` instance with the same tools registered. This keeps the systems independent and avoids any future cross-system state contamination.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Thread-safe result handoff | A lock, mutex, or `ConcurrentQueue` | `volatile string?` + `Interlocked.Exchange` | Proven pattern in this codebase; zero overhead; prevents tearing on 64-bit strings |
| Wall-clock timer | A Unity coroutine, `UnityEngine.Time`, or frame counter | `DateTime.UtcNow` comparison | `GameSystemBase` has no Unity coroutine support; `DateTime.UtcNow` is pure .NET and always correct |
| Aggregating city issues into one message | C# pre-filter, delta calculations, severity scoring | Prompt-driven single API call | D-05 and D-13 are explicit; Claude is better at deciding what's notable than C# threshold logic; pre-filtering is deferred |
| Custom HTTP client | New `HttpClient` instance per request | `static readonly HttpClient s_Http` (same pattern as `ClaudeAPISystem`) | `HttpClient` is designed to be shared; creating per-request instances causes socket exhaustion |
| Settings persistence | Custom file I/O | `ModSetting` / `Colossal.IO.AssetDatabase` | CS2's built-in settings system handles serialization, UI rendering, and persistence |

**Key insight:** `HeartbeatSystem` adds no novel C# primitives. Every pattern it needs already exists verbatim in `ClaudeAPISystem.cs` or `CityAgentUISystem.cs`. The implementation risk is integration correctness, not algorithmic complexity.

---

## Common Pitfalls

### Pitfall 1: `m_HeartbeatInFlight` Is Not `volatile`
**What goes wrong:** `m_HeartbeatInFlight` is set `true` on the game thread in `OnUpdate`, then reset `false` in the `finally` block of `RunHeartbeatAsync` on a thread-pool thread. Without `volatile`, the game thread may read a stale cached value and fire a second overlapping request.
**Why it happens:** C# compiler and JIT can cache non-volatile bool reads in registers.
**How to avoid:** Declare `private volatile bool m_HeartbeatInFlight = false;` — same as the Phase 1 fix applied to `m_RequestInFlight` in `ClaudeAPISystem`.
**Warning signs:** Two heartbeat requests appearing close together in the log.

### Pitfall 2: `PendingHeartbeatResult` Is a Field, Not a Property
**What goes wrong:** `Interlocked.Exchange` requires a `ref` to the field. Properties cannot be passed by `ref`. If `PendingHeartbeatResult` is accidentally declared as a property (`{ get; set; }`), the drain in `CityAgentUISystem` will not compile.
**Why it happens:** CS conventions use properties for public state; `volatile` fields with `Interlocked` are the exception.
**How to avoid:** Declare as a public field: `public volatile string? PendingHeartbeatResult = null;` — mirror exactly how `ClaudeAPISystem.PendingResult` is declared.

### Pitfall 3: `DateTime.UtcNow` Subtraction Returns `TimeSpan`, Not `double`
**What goes wrong:** `(DateTime.UtcNow - m_LastFireTime)` returns a `TimeSpan`. Comparing directly to an `int` (minutes) will not compile or will produce a wrong comparison.
**Why it happens:** First-time use of `DateTime` arithmetic.
**How to avoid:** Use `.TotalMinutes`: `(DateTime.UtcNow - m_LastFireTime).TotalMinutes >= setting.HeartbeatIntervalMinutes`.

### Pitfall 4: Screenshot Capture Races With Heartbeat
**What goes wrong:** If `HeartbeatSystem` calls `ScreenCapture.CaptureScreenshot()` while `CityAgentUISystem` has an active screenshot poll in progress (`m_ScreenshotWaitFrames >= 0`), both systems write/read the same temp file, corrupting both captures.
**Why it happens:** `ScreenCapture.CaptureScreenshot` writes to a fixed path; the existing code uses `Application.temporaryCachePath + "cityagent_screenshot.png"`.
**How to avoid:** (1) Check `IsScreenshotCapturePending` before capturing (D-07 — skips screenshot that cycle, not the whole heartbeat). (2) Use a separate temp file path for heartbeat screenshots (e.g., `cityagent_heartbeat_screenshot.png`) to eliminate the file conflict entirely even if the flag check has a race.

### Pitfall 5: Sentinel Check Must Handle Both `""` and `"[silent]"`
**What goes wrong:** The heartbeat system prompt instructs the AI to return `"[silent]"` or an empty string. If `CityAgentUISystem` only checks for `null` (the existing `PendingResult` check does `if (result != null)`), a `"[silent]"` string will appear as an empty assistant bubble in the chat.
**Why it happens:** The existing drain loop was designed for chat where all non-null results are meaningful.
**How to avoid:** Add `IsHeartbeatSilent()` helper (see Pattern 2 above) used specifically for the heartbeat drain branch. Chat drain remains unchanged.

### Pitfall 6: `HeartbeatSystem` Retrieval in `CityAgentUISystem.OnCreate` Timing
**What goes wrong:** `World.GetOrCreateSystemManaged<HeartbeatSystem>()` in `CityAgentUISystem.OnCreate` only works if `HeartbeatSystem` is already registered. In CS2's system scheduling, order depends on `UpdateAt` call order in `Mod.OnLoad`. If `CityAgentUISystem` is scheduled before `HeartbeatSystem`, the `GetOrCreateSystemManaged` call will create a new unscheduled instance rather than the registered one.
**Why it happens:** CS2 creates systems lazily; `GetOrCreateSystemManaged` does not fail but returns a fresh instance if the target isn't registered yet.
**How to avoid:** In `Mod.OnLoad`, schedule `HeartbeatSystem` with `UpdateAt` **before** `CityAgentUISystem` — or confirm that `GetOrCreateSystemManaged` returns the same singleton regardless of scheduling order (which is the expected behavior in Unity DOTS). The existing pattern for `ClaudeAPISystem` and `CityDataSystem` retrieval from `CityAgentUISystem.OnCreate` has not caused issues, suggesting `GetOrCreateSystemManaged` is safe regardless of `UpdateAt` order. Verify this assumption holds for `HeartbeatSystem`.

### Pitfall 7: Backoff Counter Reset Timing
**What goes wrong:** If `m_BackoffCycles` is decremented but `m_LastFireTime` is not reset, the timer could fire immediately after backoff expires (if the interval elapsed during the backoff period). This causes an extra unexpected API call right when the system "comes back online."
**Why it happens:** The backoff check returns early before the timer comparison.
**How to avoid:** Inside the backoff decrement branch, always reset `m_LastFireTime = DateTime.UtcNow` (see Pattern 1 above). This ensures the next fire is a full interval after backoff ends, not a zero-delay catch-up.

---

## Code Examples

Verified patterns from existing codebase (HIGH confidence — direct code reads):

### HeartbeatSystem Field Declarations
```csharp
// Source: mirrors ClaudeAPISystem.cs (read directly)
// Namespace: CityAgent.Systems
public partial class HeartbeatSystem : GameSystemBase
{
    private static readonly HttpClient s_Http = new HttpClient();

    private CityDataSystem        m_CityDataSystem  = null!;
    private NarrativeMemorySystem m_NarrativeMemory = null!;
    private CityToolRegistry      m_ToolRegistry    = null!;
    private CityAgentUISystem     m_UISystem        = null!; // for IsScreenshotCapturePending

    public volatile string? PendingHeartbeatResult = null;
    private volatile bool m_HeartbeatInFlight = false;
    private int m_BackoffCycles = 0;
    private DateTime m_LastFireTime;
}
```

### OnCreate Initialization
```csharp
// Source: mirrors ClaudeAPISystem.OnCreate
protected override void OnCreate()
{
    base.OnCreate();
    Mod.Log.Info($"{nameof(HeartbeatSystem)}.{nameof(OnCreate)}");

    m_CityDataSystem  = World.GetOrCreateSystemManaged<CityDataSystem>();
    m_NarrativeMemory = World.GetOrCreateSystemManaged<NarrativeMemorySystem>();
    m_UISystem        = World.GetOrCreateSystemManaged<CityAgentUISystem>();

    m_ToolRegistry = new CityToolRegistry();
    // Register all 10 tools (same set as ClaudeAPISystem — D-13)
    m_ToolRegistry.Register(new GetPopulationTool(m_CityDataSystem));
    // ... (all other tools)

    m_LastFireTime = DateTime.UtcNow; // D-10: no immediate fire on startup

    Mod.Log.Info($"[HeartbeatSystem] Initialized. First fire in {Mod.ActiveSetting?.HeartbeatIntervalMinutes ?? 5} min.");
}
```

### Settings Fields to Add to Settings.cs
```csharp
// Source: Settings.cs existing field pattern (read directly)
public const string kHeartbeatGroup = "Heartbeat";

// Add kHeartbeatGroup to [SettingsUIGroupOrder] attribute on Settings class
// Add kHeartbeatGroup to [SettingsUIShowGroupName] attribute on Settings class

private const string DefaultHeartbeatSystemPrompt =
    "You are CityAgent, observing this city silently in the background. " +
    "Analyze the current city data and — if something is genuinely noteworthy — " +
    "surface one brief observation in the style of CityPlannerPlays: engaging, specific, and actionable. " +
    "Focus on meaningful trends, emerging problems, or opportunities the player might not have noticed. " +
    "If nothing warrants attention right now, respond with exactly: [silent]";

[SettingsUISection(kSection, kHeartbeatGroup)]
public bool HeartbeatEnabled { get; set; } = false;

[SettingsUISection(kSection, kHeartbeatGroup)]
[SettingsUISlider(min = 1, max = 60, step = 1)]
public int HeartbeatIntervalMinutes { get; set; } = 5;

[SettingsUISection(kSection, kHeartbeatGroup)]
public bool HeartbeatIncludeScreenshot { get; set; } = false;

[SettingsUISection(kSection, kHeartbeatGroup)]
[SettingsUITextInput]
public string HeartbeatSystemPrompt { get; set; } = DefaultHeartbeatSystemPrompt;
```

### CityAgentUISystem Changes
```csharp
// Source: CityAgentUISystem.cs existing structure (read directly)

// In OnCreate — add after m_ClaudeAPI retrieval:
m_HeartbeatSystem = World.GetOrCreateSystemManaged<HeartbeatSystem>();

// New public property exposing screenshot state for HeartbeatSystem:
public bool IsScreenshotCapturePending => m_ScreenshotWaitFrames >= 0;

// In OnUpdate — add alongside existing chat drain (step 3):
string? hbResult = System.Threading.Interlocked.Exchange(
    ref m_HeartbeatSystem.PendingHeartbeatResult, null);
if (hbResult != null && !IsHeartbeatSilent(hbResult))
{
    m_History.Add(new ChatMessage { role = "assistant", content = hbResult });
    PushMessagesBinding();
    PersistChatSession();
}

// Helper:
private static bool IsHeartbeatSilent(string s) =>
    string.IsNullOrWhiteSpace(s) ||
    s.Equals("[silent]", StringComparison.OrdinalIgnoreCase);
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single API pipeline (chat only) | Dual pipeline: chat + heartbeat, each with own `volatile` result field | Phase 6 | Two background tasks can process concurrently; UISystem drains both each frame |
| All advisor output is user-triggered | Proactive advisor output via wall-clock timer | Phase 6 | First ambient intelligence feature; companion feel without player action |

**Deprecated/outdated in this codebase context:**
- `m_ClaudeAPI.PendingResult = null` (direct null assignment): This should be replaced with `Interlocked.Exchange` per Phase 1 D-14. Phase 6 must either confirm Phase 1 landed this fix, or apply it as part of the UISystem update in Phase 6.

---

## Open Questions

1. **Has Phase 1's `Interlocked.Exchange` fix landed for `ClaudeAPISystem.PendingResult`?**
   - What we know: Phase 1 D-14 specifies this fix; current `CityAgentUISystem.OnUpdate` (line 148–156) uses direct `m_ClaudeAPI.PendingResult = null` without `Interlocked.Exchange`.
   - What's unclear: Plans are listed as "Planning complete" but execution status is 0/4 (not yet executed per STATE.md).
   - Recommendation: Phase 6 plans must explicitly include updating the `ClaudeAPISystem.PendingResult` drain to use `Interlocked.Exchange` if Phase 1 has not yet executed. The planner should note this dependency and ensure Phase 6 doesn't assume Phase 1 has run.

2. **Does `World.GetOrCreateSystemManaged<T>()` return the scheduled singleton safely from `OnCreate`?**
   - What we know: The same pattern is used in `CityAgentUISystem.OnCreate` for `CityDataSystem` and `ClaudeAPISystem` without issues (inferred from working codebase).
   - What's unclear: Whether retrieving `CityAgentUISystem` from `HeartbeatSystem.OnCreate` (the inverse direction) has any ordering concerns in CS2's DOTS world.
   - Recommendation: Mirror the existing pattern; if a null reference occurs at runtime, fall back to retrieving the reference lazily in `OnUpdate` with a null check guard.

3. **What happens to `HeartbeatSystem.m_HeartbeatInFlight` if the game is saved and reloaded mid-request?**
   - What we know: `GameSystemBase` systems are destroyed and recreated on scene load. `OnCreate` reinitializes all fields. Any in-flight async task from the previous `GameSystemBase` instance writes to a field on a destroyed object.
   - What's unclear: Whether the async task can write to a `volatile` field on a destroyed system without crashing.
   - Recommendation: Consistent with how `ClaudeAPISystem` handles this — the existing codebase accepts this risk. No special handling needed for Phase 6.

---

## Environment Availability

Step 2.6: SKIPPED — Phase 6 is a pure C# code addition. No external tools, CLIs, or services beyond what prior phases have already established (Anthropic API, optional screenshot). No new external dependencies introduced.

---

## Validation Architecture

Nyquist validation is enabled (`workflow.nyquist_validation: true` in `.planning/config.json`).

### Test Framework
| Property | Value |
|----------|-------|
| Framework | Manual in-game verification (no automated test runner — CS2 mod context requires game process) |
| Config file | None — no test framework installed |
| Quick run command | `dotnet build -c Release` (compile check) |
| Full suite command | Build + deploy + launch CS2 + verify in-game |

**Note:** This codebase has no unit test framework. All behavioral validation is in-game. The Nyquist sampling strategy here is build-compile verification at the task level and in-game behavioral verification at the wave/phase level.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HB-01 | `HeartbeatSystem.OnUpdate` fires async request after interval elapses; result appears in chat panel without player action | In-game smoke | Build + deploy + wait interval in CS2 | ❌ Wave 0 |
| HB-01 | `m_HeartbeatInFlight` guard prevents concurrent heartbeat requests | Manual log inspection | Check CS2 log for double-fire messages | ❌ Wave 0 |
| HB-01 | AI returns `[silent]` when nothing notable; no empty bubble appears in panel | In-game smoke | Build + deploy + observe panel | ❌ Wave 0 |
| HB-02 | `HeartbeatEnabled = false` (default): no heartbeat fires on game load | In-game smoke | Launch CS2, wait > interval, confirm no advisor messages | ❌ Wave 0 |
| HB-02 | Enable heartbeat in settings at runtime: first fire after full interval, no restart | In-game smoke | Enable via settings menu, wait interval | ❌ Wave 0 |
| HB-02 | Change interval at runtime: new interval takes effect on next cycle | In-game smoke | Change interval while running | ❌ Wave 0 |
| HB-03 | Heartbeat produces one advisor message per cycle even with multiple city issues | In-game smoke | Inspect panel during a cycle with active issues | ❌ Wave 0 |
| HB-03 | API error triggers 3-cycle backoff: no log flood, no panel message, resumes after 3 skips | In-game smoke / log inspection | Temporarily break API key, observe log + panel | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet build -c Release` (compile gate)
- **Per wave merge:** Build + deploy + minimal in-game smoke (panel shows heartbeat message after interval)
- **Phase gate:** Full behavioral checklist above green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] No automated test infrastructure for CS2 mod — all validation is manual in-game
- [ ] Build command: `cd /c/Coding\ Projects/CityAgent/Working/CityAgent/src && dotnet build -c Release`
- [ ] Behavioral verification requires CS2 to be installed, running, and the city loaded

---

## Sources

### Primary (HIGH confidence)
- Direct code read: `src/Systems/ClaudeAPISystem.cs` — template for HeartbeatSystem structure
- Direct code read: `src/Systems/CityAgentUISystem.cs` — drain pattern, screenshot flag, integration points
- Direct code read: `src/Systems/Tools/CityToolRegistry.cs` — registry API, `Register`, `Dispatch`, `GetToolsJson`
- Direct code read: `src/Settings.cs` — field patterns, group constants, locale structure
- Direct code read: `src/Mod.cs` — `UpdateAt` scheduling pattern
- Direct code read: `src/Systems/NarrativeMemorySystem.cs` — `IsInitialized`, `GetAlwaysInjectedContext`, `SaveChatSession`
- `.planning/phases/06-proactive-heartbeat/06-CONTEXT.md` — all 16 locked decisions
- `.planning/REQUIREMENTS.md` — HB-01, HB-02, HB-03 requirement text
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — D-14 (Interlocked.Exchange), D-11/D-12 (async), threading model

### Secondary (MEDIUM confidence)
- `.planning/phases/03-extended-city-data-tools/03-CONTEXT.md` — D-08/D-09 tool toggles heartbeat must respect
- `.planning/phases/04-web-search-tool/04-CONTEXT.md` — settings section ordering
- CLAUDE.md architecture and conventions sections — naming conventions, thread model

### Tertiary (LOW confidence)
- None — all claims are grounded in direct code reads or CONTEXT.md locked decisions

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all libraries are .NET Standard 2.1 built-ins or game-bundled DLLs already in use
- Architecture: HIGH — `HeartbeatSystem` is a structural clone of `ClaudeAPISystem`; all patterns verified by direct code read
- Pitfalls: HIGH — derived from the actual code that will be modified; no hypothetical scenarios
- Integration points: HIGH — `CityAgentUISystem.OnUpdate` and `Mod.cs` read directly; change surface is small and explicit

**Research date:** 2026-03-28
**Valid until:** Indefinite for this codebase (no external dependencies changing); re-research if CS2 DOTS API changes
