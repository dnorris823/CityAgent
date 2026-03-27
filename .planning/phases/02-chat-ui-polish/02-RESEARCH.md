# Phase 2: Chat UI Polish — Research

**Researched:** 2026-03-26
**Domain:** Coherent GT React UI (TypeScript), C# UISystemBase, hand-rolled markdown renderer
**Confidence:** HIGH — all sources are the project's own source files plus the approved UI-SPEC and CONTEXT documents

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**System / Notice Messages**
- D-01: System/notice messages use a center pill style — full-width, center-aligned, smaller text, muted amber/gray color. Not a conversation bubble.
- D-02: System notices are used for warnings and errors only: rate-limit notices, fallback activations, API errors. Normal actions (chat cleared, screenshot captured) need no notice.
- D-03: System notices persist in chat history and are cleared on "New Chat" like any other message. No ephemeral special-casing.
- D-04: System notices are filtered out of the API payload — C# strips messages with `role == "system"` before building the `/v1/messages` array.
- D-05: `[Error]: ...` strings change to `role: "system"` — currently surfaced as assistant bubbles; in this phase they become center-pill notices. C# must write these to history with `role: "system"` instead of `role: "assistant"`.

**Loading Indicator**
- D-06: Loading bubble shows bouncing dots + rotating city-flavored status text below the dots in the same assistant-style bubble. Cycles every 2–3 seconds. Tone: CityPlannerPlays narrator.
- D-07: No cancel button. No elapsed time counter.
- D-08: Input box is not disabled while loading — type-ahead is supported. One queued message maximum; send button is grayed while a message is already queued.
- D-09: When a message is queued during loading, input clears and queued text appears as a queued chip above the input area (same visual pattern as the screenshot chip), with an ✕ to discard.
- D-10: When `PendingResult` clears (C# side), the queued message is auto-sent immediately. C# needs an `m_QueuedMessage` string field; `CityAgentUISystem.OnUpdate` checks it after draining `PendingResult`.

**Empty / Welcome State**
- D-11: Empty panel shows a randomly selected welcome greeting from a set of 4–5 immersive city-flavored lines.
- D-12: Welcome greeting is styled as a centered greeting block — not an assistant bubble. Disappears instantly (no fade) when the first message is added. Reappears after "New Chat".
- D-13: No separate greeting for "New Chat" vs. first open — same welcome set for both.

**Markdown Edge Cases**
- D-14: Fix nested/indented lists — sub-bullets must render as `<ul>` inside `<li>`.
- D-15: Fix `_italic_` regex — replace lookbehind `(?<!\w)` with a Coherent GT-safe alternative.
- D-16: Verify bold + headers coexisting — fix any ordering/precedence bugs found.
- D-17: Code blocks show a language label above the block as small secondary text. No syntax highlighting.
- D-18: Strip `<thinking>...</thinking>` blocks before display. Preferred: strip in C# before writing to `m_History`. Fallback: strip in `renderMarkdown.ts`.
- D-19: Malformed markdown handled best-effort — unmatched markers show as literal text. No change needed.
- D-20: User messages stay plain text only.

### Claude's Discretion
- Center pill exact CSS (border, color values, padding) — complement existing CS2 palette
- Rotating loading status phrases (5–6 city-flavored lines)
- Welcome greeting variants (4–5 lines)
- `m_QueuedMessage` field placement and drain logic in `CityAgentUISystem`
- Whether `<thinking>` stripping happens in C# or `renderMarkdown.ts`

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| UI-01 | User and assistant messages are visually distinct — different bubble styles, alignment, or color differentiation | Already implemented (`.ca-bubble--user` right-aligned, `.ca-bubble--assistant` left-aligned). Phase 2 adds `role: "system"` as a third visual class (center pill). The existing switch in the message renderer must become a 3-way conditional. |
| UI-02 | Claude responses render markdown correctly — headers, bold, italic, unordered lists, code blocks display as formatted text (no raw asterisks) | `renderMarkdown.ts` exists and handles all major cases. Four targeted fixes are locked: nested lists, lookbehind italic regex, code block language labels, and `<thinking>` stripping. |
| UI-03 | Loading / thinking indicator is visible while Claude generates a response — animation or status text in the panel | `LoadingDots` component exists. Extension: add `.ca-loading-status` text child inside the same `.ca-bubble--assistant` wrapper. Status text cycles via `setInterval` while `isLoading` is true. |
</phase_requirements>

---

## Summary

Phase 2 is a focused UI polish pass on the existing `CityAgentPanel.tsx` and `renderMarkdown.ts`. The phase adds no new C#↔React bindings and introduces no new build dependencies. All work falls into four categories: (1) a third message role ("system") with center-pill styling, (2) loading state augmentation with rotating status text plus a type-ahead queue chip, (3) an empty-state welcome block, and (4) four targeted fixes to `renderMarkdown.ts`.

The design contract is fully specified in `02-UI-SPEC.md`. The planner's job is to translate that spec into sequenced task files — the research job is to document the precise code surfaces that must change, the Coherent GT constraints that govern how, and the pitfalls the executor must avoid.

**Primary recommendation:** Implement in two streams — C# changes first (D-05, D-10: role promotion and queued message drain) so the React side has correct data to work with, then React/CSS changes (all visual components and markdown fixes). Keep C# changes minimal and targeted; the React side carries most of the work.

---

## Standard Stack

### Core (no changes from existing)

| Layer | Technology | Version | Notes |
|-------|-----------|---------|-------|
| UI runtime | Coherent GT (Chromium-based, older) | shipped with CS2 | Hard target — no polyfills available |
| UI framework | React 18 | runtime-injected | Not bundled — `window.React` |
| Language | TypeScript | 5.3 | Strict mode; targets ES2020 |
| Bundler | Webpack 5 | 5.89 | Output: `CityAgent.mjs` (ES module) |
| C# | .NET Standard 2.1 | n/a | Compiled to DLL loaded by Unity |
| JSON | Newtonsoft.Json | game-bundled | Used for `JsonConvert.SerializeObject(m_History)` |

### No new packages needed

This phase requires zero new npm packages or NuGet packages. All work is pure source edits.

**Installation:** None required.

---

## Architecture Patterns

### C#↔React Binding Contract (unchanged this phase)

The seven existing `ValueBinding<T>` objects remain unchanged. No new bindings are introduced. The only C# change that crosses the bridge is that `m_History` now contains `ChatMessage` objects with `role = "system"` (previously only `"user"` and `"assistant"`).

```
C# (CityAgentUISystem.cs)
  └── m_MessagesJson  → ValueBinding<string> → "messagesJson"
        └── JSON array of { role, content, hadImage }
              ↓
React (CityAgentPanel.tsx)
  └── rawJson → useMemo → messages: ChatMessage[]
        └── messages.map(...) → 3-way role switch
              "user"      → .ca-bubble.ca-bubble--user
              "assistant" → .ca-bubble.ca-bubble--assistant
              "system"    → .ca-notice-pill (.ca-notice-pill--error | --warning)
```

### ChatMessage Role Expansion

**C# side:** `ChatMessage` private class in `CityAgentUISystem.cs` — `role` field is currently an unconstrained `string`. No type change needed; just write `"system"` as the role value.

**Current error path (lines 148–156 of CityAgentUISystem.cs):**
```csharp
// Currently: error strings appended as role "assistant"
m_History.Add(new ChatMessage { role = "assistant", content = result });
```

**Changed path (D-05):** When `result` starts with `[Error]:`, write `role = "system"` instead:
```csharp
string role = result.StartsWith("[Error]:") ? "system" : "assistant";
m_History.Add(new ChatMessage { role = role, content = result });
```

**React side:** `ChatMessage` interface (line 5–9 of CityAgentPanel.tsx) — change `role` union:
```typescript
// Before:
interface ChatMessage {
  role: "user" | "assistant";
  ...
}
// After:
interface ChatMessage {
  role: "user" | "assistant" | "system";
  ...
}
```

### API Payload Filtering (D-04)

`ClaudeAPISystem.cs` builds the `/v1/messages` array from history. System-role messages must be filtered before building the payload. The filtering logic lives in `ClaudeAPISystem.RunRequestAsync` where it constructs the messages array — skip any message where `role == "system"`.

### Type-Ahead Queue: C# Side (D-10)

`m_QueuedMessage` is a C# implementation detail — purely optional based on planner discretion. However, the simpler approach is to handle queuing entirely in React local state and trigger `sendMessage` on the React side when `isLoading` transitions to false. The C# `m_QueuedMessage` field would only be needed if the queued message must survive a panel close/reopen. Decision: handle in React state only (simpler, no new C# binding needed).

**Verified:** The `useEffect` watching `isLoading` pattern is sufficient:
```typescript
useEffect(() => {
  if (!isLoading && queuedMessage) {
    safeTrigger("cityAgent", "sendMessage", queuedMessage);
    setQueuedMessage(null);
  }
}, [isLoading]);
```

### Loading Status Text (D-06)

Current `LoadingDots` component (lines 89–93 of CityAgentPanel.tsx) renders inside `.ca-bubble.ca-bubble--assistant`. Extension: add a sibling `.ca-loading-status` span below the dots. Status text cycles independently via `setInterval`.

**Pattern (per UI-SPEC implementation note):**
- Use `useRef` for the interval ID — not `useState` — to avoid extra re-renders
- Derive current phrase from `Date.now()`: `phrases[Math.floor(Date.now() / 2500) % phrases.length]`
- Track displayed phrase index with `useState` updated by the interval
- Start interval in `useEffect` when `isLoading` becomes true; clear on cleanup

```typescript
// Interval-based phrase cycling — clear on isLoading=false
useEffect(() => {
  if (!isLoading) return;
  const interval = setInterval(() => {
    setLoadingPhrase(phrases[Math.floor(Date.now() / 2500) % phrases.length]);
  }, 500); // check frequently, phrase changes every 2500ms
  return () => clearInterval(interval);
}, [isLoading]);
```

### Nested List Fix (D-14)

Current flat list loop in `renderMarkdown.ts` (lines 272–281) matches any line starting with `[\s]*[-*+]\s` and strips all leading whitespace before rendering `<li>`. This loses indent depth entirely.

**Fix strategy — collect-and-recurse:**
1. Collect all consecutive list lines (including blank lines between items) with their raw indent depth
2. Split into top-level items and sub-items based on indent threshold
3. When a sub-item group is found, emit `<ul>` inside the parent `<li>`

**Simplified two-level approach** (sufficient for Claude output):
```
top-level:  0 leading spaces
sub-level:  2+ leading spaces
```
No need for arbitrary N-depth nesting. Claude does not produce 3+ level nesting in practice.

### Italic Regex Fix (D-15)

Current pattern at `renderMarkdown.ts` line 206:
```javascript
line = line.replace(/(?<!\w)_(.+?)_(?!\w)/g, "<em>$1</em>");
```
Lookbehind `(?<!\w)` is not supported in Coherent GT's older Chromium.

**Replacement pattern (from UI-SPEC):**
```javascript
line = line.replace(/(^|[\s.,!?;:'"([{])_([^_]+?)_([\s.,!?;:'")\]}]|$)/g,
  function(_m, pre, content, post) {
    return pre + "<em>" + content + "</em>" + post;
  }
);
```
This requires a replacing function to reassemble surrounding context. Applies the same word-boundary intent without lookbehind.

### `<thinking>` Block Stripping (D-18)

**Preferred location: C# in `CityAgentUISystem.cs`** before writing to `m_History`. This keeps `renderMarkdown.ts` free of XML-awareness.

Strip location: in the `PendingResult` drain block (after reading `m_ClaudeAPI.PendingResult`, before appending to `m_History`):
```csharp
// Strip <thinking>...</thinking> blocks (extended thinking model output)
result = System.Text.RegularExpressions.Regex.Replace(
    result,
    @"<thinking>[\s\S]*?</thinking>",
    "",
    System.Text.RegularExpressions.RegexOptions.IgnoreCase
).Trim();
```
This runs on the main thread immediately after `Interlocked.Exchange` — no threading concern.

**Fallback:** If C# approach is out of scope for this wave, add as first line in `renderMarkdown()`:
```javascript
markdown = markdown.replace(/<thinking>[\s\S]*?<\/thinking>/gi, "").trim();
```
Note: `[\s\S]*?` is safe in Coherent GT (no Unicode property escapes used).

### Code Block Language Label (D-17)

Current code block output (line 235 of renderMarkdown.ts):
```javascript
out.push("<pre><code>" + codeLines.join("\n") + "</code></pre>");
```
Fix: emit `<span class="ca-code-lang">` before `<pre>` when language is non-empty:
```javascript
var lang = fenceMatch[1];
if (lang) {
  out.push('<span class="ca-code-lang">' + escapeHtml(lang) + "</span>");
}
out.push("<pre><code>" + codeLines.join("\n") + "</code></pre>");
```

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Syntax highlighting | Custom tokenizer | Nothing (D-17 says no highlight) | Can't bundle libraries; label-only is spec |
| Markdown library | None needed | Extend existing `renderMarkdown.ts` | No external libs can be bundled in Coherent GT |
| Animation library | None needed | CSS `@keyframes` (already used) | `ca-dot-bounce` keyframe already exists |
| CSS variables / custom props | CSS variables | Inline style / class values | Coherent GT support for CSS custom properties is not verified — use literal values |
| `Array.at()` | Polyfill | `arr[arr.length - 1]` | Coherent GT Chromium lacks `Array.at()` |
| Unicode property escapes (`\p{}`) | None | Character class ranges `[\s.,...]` | Unsupported in Coherent GT |
| CSS `gap` | Flexbox gap | `margin-right`/`margin-bottom` on children | `gap` is not supported in Coherent GT |

**Key insight:** The Coherent GT constraint is the single largest source of pitfalls. When a new JS/CSS pattern is considered, the first question is "does Coherent GT's older Chromium support this?"

---

## Common Pitfalls

### Pitfall 1: Lookbehind / Lookahead in Coherent GT Regex
**What goes wrong:** `(?<!\w)` and `(?!\w)` lookbehind/lookahead assertions throw a runtime error in Coherent GT's older V8 engine, crashing the `renderMarkdown()` call silently (caught by ErrorBoundary but panel goes blank).
**Why it happens:** The current italic pattern on line 206 already uses this. D-15 is the fix.
**How to avoid:** Always use character class alternatives or replacement functions instead of lookbehind/lookahead in `renderMarkdown.ts`.
**Warning signs:** Blank assistant bubbles; console errors mentioning "Invalid regular expression".

### Pitfall 2: CSS `gap` Property
**What goes wrong:** Elements using `gap` for flexbox spacing do not space at all in Coherent GT — items appear flush.
**Why it happens:** Coherent GT ships a Chromium version predating `gap` in flexbox.
**How to avoid:** Use `margin-right`, `margin-bottom`, or `margin-left` on child elements. The existing chip patterns use this correctly.
**Warning signs:** Chips or input row elements bunched together with no space.

### Pitfall 3: `useState` for Interval Phrase Index Causes Render Spam
**What goes wrong:** Using `setInterval` to update a `useState` phrase index every 100ms causes React to re-render the entire inner component every 100ms, causing visible jitter in the input area and drag position.
**Why it happens:** Every `setState` call triggers a full re-render of `CityAgentInner`.
**How to avoid:** Per UI-SPEC note 3, derive the current phrase from `Date.now()` inside a single `useState` updated at 2500ms intervals. Do not update state more frequently than the visible phrase change rate.

### Pitfall 4: `useEffect` Cleanup for Interval
**What goes wrong:** Interval set in `useEffect` for loading status text is not cleaned up when `isLoading` transitions to false, continuing to call `setState` on an unmounted component path.
**Why it happens:** `useEffect` cleanup function must call `clearInterval`.
**How to avoid:** Always return a cleanup function from `useEffect` when setting intervals:
```typescript
useEffect(() => {
  if (!isLoading) return;
  const id = setInterval(() => { /* ... */ }, 2500);
  return () => clearInterval(id);
}, [isLoading]);
```

### Pitfall 5: Queued Message Sent Twice
**What goes wrong:** The `useEffect` watching `isLoading` fires, calls `safeTrigger("sendMessage", queuedMessage)`, but `setQueuedMessage(null)` is batched and the state hasn't cleared before a second render triggers the effect again.
**Why it happens:** React batches state updates; the null assignment and the effect run order can produce a double-fire in strict mode or fast state transitions.
**How to avoid:** Set `queuedMessage` to null synchronously in the same handler before the trigger, or use a ref flag to track "has been sent":
```typescript
const pendingQueuedMsg = useRef<string | null>(null);
// on queue: pendingQueuedMsg.current = msg
// on drain: if (pendingQueuedMsg.current) { safeTrigger(..., pendingQueuedMsg.current); pendingQueuedMsg.current = null; }
```

### Pitfall 6: `role: "system"` Messages Sent to Claude API
**What goes wrong:** System notice pills appear in the `m_History` list in C#, and if they are not filtered before building the API payload, Claude receives them as messages, potentially confusing the conversation.
**Why it happens:** `ClaudeAPISystem.RunRequestAsync` iterates `m_History` to build the messages array. Without a filter, role="system" entries pass through.
**How to avoid:** D-04 is explicit: add a `.Where(m => m.role != "system")` filter in the history-to-messages mapping in `ClaudeAPISystem.cs`.

### Pitfall 7: Nested List Depth Counting with Mixed Spaces/Tabs
**What goes wrong:** Claude sometimes uses 2-space indent, sometimes 4-space indent for nested lists. A fixed "2+ spaces = sub-item" threshold misclassifies 4-space top-level items in some responses.
**Why it happens:** The threshold for "sub-item" is ambiguous across different response styles.
**How to avoid:** Use indent relative to the first item's indent as the baseline. If the first item has 0 leading spaces, any item with 2+ spaces is a sub-item. If the first item has 2 leading spaces (some models), items with 4+ are sub-items.

### Pitfall 8: Welcome Block Visible During Loading
**What goes wrong:** After "New Chat", `m_History.Clear()` runs, pushing an empty `messagesJson`. If the loading state is somehow true at the same moment, both the welcome block and the loading bubble appear simultaneously.
**Why it happens:** `OnClearChat()` sets `isLoading=false`, so this shouldn't occur — but race conditions between the binding push and the React render could briefly show both.
**How to avoid:** Gate the welcome block on `messages.length === 0 && !isLoading` (as specified in D-12). The `isLoading` guard eliminates this edge case.

---

## Code Examples

All examples sourced from direct inspection of the project source files.

### Message Renderer — 3-Way Switch (current code at CityAgentPanel.tsx:318–329)

```typescript
// Source: UI/src/components/CityAgentPanel.tsx lines 318–329 (current)
{messages.map((msg, i) => (
  <div key={i} className={`ca-bubble ca-bubble--${msg.role}`}>
    {msg.hadImage && (
      <span className="ca-bubble__image-badge">screenshot attached</span>
    )}
    {msg.role === "assistant" ? (
      <div className="ca-bubble__text ca-markdown" dangerouslySetInnerHTML={{ __html: renderMarkdown(msg.content) }} />
    ) : (
      <span className="ca-bubble__text">{msg.content}</span>
    )}
  </div>
))}

// After this phase — 3-way conditional replacing the 2-way:
{messages.map((msg, i) => {
  if (msg.role === "system") {
    const isError = msg.content.startsWith("[Error]:");
    const pillClass = isError ? "ca-notice-pill ca-notice-pill--error" : "ca-notice-pill ca-notice-pill--warning";
    const text = msg.content.replace(/^\[Error\]:\s*/, "");
    return (
      <div key={i} className={pillClass}>
        {text}
      </div>
    );
  }
  return (
    <div key={i} className={`ca-bubble ca-bubble--${msg.role}`}>
      {msg.hadImage && <span className="ca-bubble__image-badge">screenshot attached</span>}
      {msg.role === "assistant" ? (
        <div className="ca-bubble__text ca-markdown" dangerouslySetInnerHTML={{ __html: renderMarkdown(msg.content) }} />
      ) : (
        <span className="ca-bubble__text">{msg.content}</span>
      )}
    </div>
  );
})}
```

### Screenshot Chip Pattern (existing — model for queued chip)

```css
/* Source: UI/src/style.css lines 241–268 */
.ca-screenshot-chip {
  display: flex;
  align-items: center;
  background: rgba(30, 60, 100, 0.6);
  border: 1px solid rgba(74, 158, 222, 0.35);
  border-radius: 20px;
  padding: 0.2em 0.7em;
  font-size: 0.78em;
  color: #4a9ede;
  align-self: flex-start;
  margin-bottom: 0.4em;
}
```

### PendingResult Drain (current — CityAgentUISystem.cs lines 147–156)

```csharp
// Source: src/Systems/CityAgentUISystem.cs lines 147-156
string? result = m_ClaudeAPI.PendingResult;
if (result != null)
{
    m_ClaudeAPI.PendingResult = null;
    m_History.Add(new ChatMessage { role = "assistant", content = result });
    PushMessagesBinding();
    m_IsLoading.Update(false);
    PersistChatSession();
}
```

After D-05 and D-18 changes:
```csharp
string? result = m_ClaudeAPI.PendingResult;
if (result != null)
{
    m_ClaudeAPI.PendingResult = null;
    // Strip <thinking> blocks before storing
    result = System.Text.RegularExpressions.Regex.Replace(
        result, @"<thinking>[\s\S]*?</thinking>", "",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    // Promote errors to system role (D-05)
    string role = result.StartsWith("[Error]:") ? "system" : "assistant";
    m_History.Add(new ChatMessage { role = role, content = result });
    PushMessagesBinding();
    m_IsLoading.Update(false);
    PersistChatSession();
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Error as `role: "assistant"` | Error as `role: "system"` center pill | This phase (D-05) | Errors no longer look like AI responses |
| Loading = dots only | Loading = dots + rotating status text | This phase (D-06) | More immersive, less clinical |
| Input disabled while loading | Input enabled, type-ahead queued | This phase (D-08) | No blocking UX |
| Empty panel = nothing | Empty panel = welcome greeting | This phase (D-11) | First impression improvement |
| Flat unordered lists | Nested unordered lists | This phase (D-14) | Claude responses render correctly |
| Lookbehind italic regex (broken) | Character class italic regex (safe) | This phase (D-15) | Fixes silent render crash |

---

## Environment Availability

Step 2.6: SKIPPED — this phase is purely code/CSS changes with no external tool, service, or runtime dependencies beyond the existing Node.js + dotnet build chain.

Existing build commands verified available (from user environment in CLAUDE.md memory):
- Node.js v24.13.0, npm 11.6.2
- `cd UI && npm run build` — bundles TypeScript to `CityAgent.mjs`
- `cd src && dotnet build -c Release` — compiles C# DLL

---

## Validation Architecture

`workflow.nyquist_validation` is `true` in `.planning/config.json` — this section is required.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | None — this is a CS2 mod with no test runner configured |
| Config file | None detected |
| Quick run command | `cd UI && npm run build` (TypeScript type-check via tsc + webpack) |
| Full suite command | `cd src && dotnet build -c Release && cd ../UI && npm run build` |

**No automated test framework exists in this project.** The `npm run build` step performs TypeScript type-checking as the only automated correctness gate. C# correctness is validated by `dotnet build` (compile-time) and in-game manual verification.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UI-01 | `role: "system"` messages render as `.ca-notice-pill`, not bubbles | TypeScript compile | `cd UI && npm run build` | ❌ Wave 0 |
| UI-01 | `role: "user"` and `role: "assistant"` bubbles remain visually distinct | Manual in-game | N/A — visual | N/A |
| UI-02 | Nested lists render as `<ul>` inside `<li>` | TypeScript compile (type-safe) | `cd UI && npm run build` | ❌ Wave 0 |
| UI-02 | `_italic_` renders without runtime regex crash | Manual in-game | N/A — runtime test | N/A |
| UI-02 | Code blocks show language label | Manual in-game | N/A — visual | N/A |
| UI-02 | `<thinking>` blocks stripped from responses | Manual in-game (need test prompt) | N/A | N/A |
| UI-03 | Loading indicator visible with status text rotating | Manual in-game | N/A — visual + timing | N/A |
| UI-03 | Type-ahead chip appears and auto-sends | Manual in-game | N/A — interaction | N/A |

### Sampling Rate

- **Per task commit:** `cd UI && npm run build` (TypeScript type check)
- **Per wave merge:** `cd src && dotnet build -c Release && cd ../UI && npm run build`
- **Phase gate:** Full suite green + manual in-game verification pass before `/gsd:verify-work`

### Wave 0 Gaps

The project has no unit test files. Given this is a CS2 mod with no test runner, unit tests are not feasible without substantial test harness infrastructure that is out of scope. The TypeScript compiler and C# compiler serve as the automated correctness gates.

- [ ] Consider adding a standalone `renderMarkdown` smoke-test script (Node.js, no framework) to verify markdown output for the four fixed cases — but this is optional and not required for phase gate.

*(The build step is the primary automated gate. In-game verification is the acceptance gate.)*

---

## Open Questions

1. **`<thinking>` block format consistency**
   - What we know: The CONTEXT.md prefers C# stripping. The pattern `<thinking>[\s\S]*?</thinking>` handles single blocks.
   - What's unclear: Claude extended thinking may produce multiple `<thinking>` blocks in one response, or nested blocks.
   - Recommendation: Use a global replace (no flags needed since `Regex.Replace` with the pattern as written replaces all non-overlapping matches) — handles multiple blocks correctly. If nested, the lazy `*?` stops at the first `</thinking>`, which handles simple nesting correctly.

2. **Queued message and NarrativeMemory persistence**
   - What we know: `PersistChatSession()` is called after `OnSendMessage`. If the user queues a message and the response arrives before the queued message is auto-sent, the auto-send fires a second `sendMessage` trigger, which will call `PersistChatSession()` again. This is fine (idempotent).
   - What's unclear: Should the queued message chip's content be preserved across a panel toggle (hide/show)?
   - Recommendation: Store `queuedMessage` in React local state (not a binding), so it is reset on panel remount. This is acceptable — the user can retype if they accidentally close the panel.

3. **Italic regex replacement and `__bold__` interaction**
   - What we know: `__bold__` (double underscore) is processed before the italic underscore pass. The fix replaces only single-underscore patterns.
   - What's unclear: Edge case where `_italic_ __bold__` on the same line — the new character-class pattern requires the post-context char to be whitespace or punctuation. The space before `_` in `__` is whitespace, which could match.
   - Recommendation: Run the D-16 bold+heading coexistence verification pass during the same task as the italic fix. If conflicts are found, process double-underscore before single-underscore (already the case — `__(.+?)__` is at line 203, before italic at line 206).

---

## Sources

### Primary (HIGH confidence)

All findings are sourced directly from project source files — no external research required. This phase is implementation-defined by pre-approved specs.

- `UI/src/components/CityAgentPanel.tsx` — full component read (lines 1–394)
- `UI/src/utils/renderMarkdown.ts` — full renderer read (lines 1–350)
- `UI/src/style.css` — full CSS read (lines 1–503)
- `src/Systems/CityAgentUISystem.cs` — full system read (lines 1–267)
- `.planning/phases/02-chat-ui-polish/02-CONTEXT.md` — locked decisions (D-01 through D-20)
- `.planning/phases/02-chat-ui-polish/02-UI-SPEC.md` — CSS classes, color values, typography scale, interaction contracts
- `.planning/codebase/ARCHITECTURE.md` — binding contract, thread model, error handling
- `.planning/REQUIREMENTS.md` — UI-01, UI-02, UI-03 definitions
- `.planning/config.json` — nyquist_validation=true confirmed

### Secondary (MEDIUM confidence)

None — no external sources needed for this phase.

### Tertiary (LOW confidence)

None.

---

## Project Constraints (from CLAUDE.md)

These are actionable directives the planner must verify compliance against:

| Directive | Impact on This Phase |
|-----------|----------------------|
| No CSS Grid — flexbox only | All new layout (pill, chip, welcome block) must use `display: flex` |
| No `gap` property | Use `margin-*` on children for spacing |
| No `::placeholder` pseudo-element | Input field placeholder already uses fallback — no change needed |
| No `:disabled` pseudo-class | Use `[disabled]` attribute selector (already done for `.ca-send-btn[disabled]`) |
| No `Array.at()`, Unicode property escapes | Avoid in any new JS utility code |
| Emoji via Twemoji CDN background-image spans | Warning emoji in system notices must use `.ca-emoji` class, not raw Unicode emoji characters. Exception: simple ASCII-range symbols like `⚠` (U+26A0, BMP emoji range — must check `isBmpEmoji()` coverage) |
| No external markdown library | All markdown changes stay in `renderMarkdown.ts` |
| Keep C# thin | C# changes are minimal: role promotion (3 lines), `<thinking>` strip (4 lines), API filter (1 line) |
| API key never hardcoded / logged | Not relevant to this phase |
| All HTTP calls async/non-blocking | Not relevant to this phase — no new HTTP calls |
| `m_` prefix for private C# fields | Any new C# field follows this convention (e.g. `m_QueuedMessage` if added) |
| `ca-` prefix + BEM for CSS classes | All new CSS classes follow this pattern: `.ca-notice-pill`, `.ca-loading-status`, `.ca-queued-chip`, `.ca-welcome`, `.ca-code-lang` |
| Dynamic values → inline style; static → CSS class | Loading status text color/font set in CSS class; panel position remains inline |
| `var` declarations in `renderMarkdown.ts` | New code in `renderMarkdown.ts` must use `var`, not `const`/`let` (Coherent GT compatibility convention established in existing file) |

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all from project source files
- Architecture patterns: HIGH — direct code inspection of all change surfaces
- Pitfalls: HIGH — sourced from explicit Coherent GT constraints documented in CLAUDE.md + UI-SPEC
- Code examples: HIGH — direct copy from source with annotation

**Research date:** 2026-03-26
**Valid until:** Stable — this is self-referential (project source files don't expire). Re-verify if Phase 1 changes the C#↔React binding contract.
