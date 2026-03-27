# Phase 2: Chat UI Polish - Context

**Gathered:** 2026-03-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Polish the existing chat panel UI: add a styled system/notice message role, extend the loading
state with rotating city-flavored status text, add a type-ahead message queue, implement an
immersive empty/welcome state, and fix known `renderMarkdown.ts` edge cases (nested lists,
italic regex, code block language labels, `<thinking>` block stripping).

</domain>

<decisions>
## Implementation Decisions

### System / Notice Messages
- **D-01:** System/notice messages use a **center pill** style — full-width, center-aligned,
  smaller text, muted amber/gray color. Not a conversation bubble. Visually distinct from both
  user and assistant messages.
- **D-02:** System notices are used for **warnings and errors only**: rate-limit notices, fallback
  activations, API errors. Normal actions (chat cleared, screenshot captured) need no notice.
- **D-03:** System notices **persist in chat history** and are cleared on "New Chat" like any
  other message. No ephemeral special-casing.
- **D-04:** System notices are **filtered out of the API payload** — C# strips messages with
  `role == "system"` before building the `/v1/messages` array. Claude never sees them.
- **D-05:** **`[Error]: ...` strings change to `role: "system"`** — currently surfaced as
  assistant bubbles; in this phase they become center-pill notices. C# must write these to history
  with `role: "system"` instead of `role: "assistant"`. This is a behavior change from Phase 1.

### Loading Indicator
- **D-06:** Loading bubble shows **bouncing dots + rotating city-flavored status text** below
  the dots in the same assistant-style bubble. Text cycles every 2–3 seconds, loops on long
  responses. Tone: immersive, CityPlannerPlays narrator vibe (e.g. "Surveying the city...",
  "Consulting the records..."). **Claude's discretion** on the specific 5–6 phrases.
- **D-07:** No cancel button. No elapsed time counter. Clean and minimal.
- **D-08:** Input box is **not disabled while loading** — type-ahead is supported. One queued
  message maximum; send button is grayed while a message is already queued.
- **D-09:** When a message is queued during loading, the input **clears** and the queued text
  appears as a **queued chip** above the input area (same visual pattern as the screenshot chip),
  with an **✕ to discard** the queued message.
- **D-10:** When `PendingResult` clears (C# side), the queued message is **auto-sent
  immediately**. C# needs an `m_QueuedMessage` string field; `CityAgentUISystem.OnUpdate` checks
  it after draining `PendingResult`.

### Empty / Welcome State
- **D-11:** The empty panel (no messages) shows a **randomly selected welcome greeting** from a
  set of 4–5 immersive, city-flavored lines. **Claude's discretion** on the specific copy.
  Examples: "Welcome back, Mayor. The city awaits.", "Your advisor is ready. What would you like
  to know?"
- **D-12:** Welcome greeting is styled as a **centered greeting block** — not an assistant bubble.
  It disappears **instantly** (no fade) when the first message is added to history. The same
  welcome state reappears after "New Chat".
- **D-13:** No separate greeting for "New Chat" vs. first open — same welcome set for both.

### Markdown Edge Cases
- **D-14:** Fix **nested/indented lists** — sub-bullets under a top-level list item must render
  as `<ul>` inside `<li>`, not as flat items or raw text.
- **D-15:** Fix **`_italic_` regex** — replace the lookbehind pattern (`(?<!\w)`) with a
  Coherent GT–safe alternative that doesn't rely on lookbehind support.
- **D-16:** Verify **bold + headers coexisting** — ensure `## Header` and `**bold**` in the same
  response don't interfere. Fix any ordering/precedence bugs found.
- **D-17:** Code blocks show a **language label above the block** (e.g. `python`, `json`) as
  small secondary text. No syntax highlighting — that requires a library which can't be bundled.
- **D-18:** Strip **`<thinking>...</thinking>` blocks** before display. Preferred approach:
  strip in C# before writing to `m_History` (keeps the renderer simple). If that's not feasible
  in scope, strip in `renderMarkdown.ts`.
- **D-19:** Malformed markdown (unclosed `**`, mismatched backticks) handled **best-effort** —
  unmatched markers show as literal text. Current behavior, no change needed.
- **D-20:** User messages stay **plain text only** — no markdown rendering for user input.

### Claude's Discretion
- Center pill exact CSS (border, color values, padding) — should complement existing CS2-theme
  palette (`rgba(60, 100, 150, ...)`, `rgba(12, 22, 36, ...)`)
- Rotating loading status phrases (5–6 city-flavored lines)
- Welcome greeting variants (4–5 lines)
- `m_QueuedMessage` field placement and drain logic in `CityAgentUISystem`
- Whether `<thinking>` stripping happens in C# or `renderMarkdown.ts`

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Requirements & Roadmap
- `.planning/REQUIREMENTS.md` — UI-01 (visual distinction), UI-02 (markdown rendering),
  UI-03 (loading indicator)
- `.planning/ROADMAP.md` — Phase 2 goal and 3 success criteria

### Prior Phase Context
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — D-06 (system message
  role pattern), D-11/D-12 (threading patterns), D-14 (PendingResult drain pattern);
  Phase 1 establishes `role: "system"` notices which Phase 2 must style

### Codebase
- `.planning/codebase/ARCHITECTURE.md` — C#↔React binding contract, CityAgentUISystem
  data flow, thread model

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `UI/src/components/CityAgentPanel.tsx` — Full panel implementation; `LoadingDots` component
  (lines 89–93) already exists. Bubble rendering at lines 318–334. Screenshot chip pattern
  (lines 338–343) is the model for the queued-message chip.
- `UI/src/style.css` — `.ca-bubble--user`, `.ca-bubble--assistant`, `.ca-loading-dots` all
  exist. `.ca-screenshot-chip` is the model for the queued message chip style.
- `UI/src/utils/renderMarkdown.ts` — Complete markdown renderer; `inlinePass()` is where
  italic/bold fixes go; list rendering at lines 273–291; code block at lines 226–236.

### Established Patterns
- Dynamic values (position, dimensions) → inline styles; static styles → `.ca-*` CSS classes
- No CSS Grid (Coherent GT unsupported); all layout via flexbox
- Emoji via Twemoji CDN background-image spans (CSS font emoji not supported in Coherent GT)
- `safeTrigger()` wraps all C#→JS trigger calls to prevent uncaught errors
- Screenshot chip (`ca-screenshot-chip`) sets the visual and behavioral pattern for the
  queued message chip

### Integration Points
- `CityAgentUISystem.OnUpdate` (C#): add `m_QueuedMessage` check after `PendingResult` drain
- `CityAgentUISystem.cs` message history: role `"system"` messages must be rendered as center
  pills in React and filtered before API payload construction
- `ClaudeAPISystem.RunRequestAsync`: filter `role == "system"` from history before building
  the `/v1/messages` array

</code_context>

<specifics>
## Specific Ideas

- Queued message chip: same visual pattern as `ca-screenshot-chip` with "queued:" prefix and ✕
- System notice pill: full-width centered text, slightly smaller than bubble font, muted amber
  border — e.g. `⚠️ Rate limited — retrying with llama3...`
- Welcome greeting: centered block with subdued header color (`#c8d8e8`), italic, appears only
  when `messages.length === 0`
- Loading text position: below the dots, same `ca-bubble--assistant` wrapper, small italic style

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 02-chat-ui-polish*
*Context gathered: 2026-03-26*
