# Phase 2: Chat UI Polish - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-26
**Phase:** 02-chat-ui-polish
**Areas discussed:** System notices, Loading indicator, Empty state, Markdown edge cases

---

## System Notices

| Option | Description | Selected |
|--------|-------------|----------|
| Subtle center pill | Full-width, center-aligned, smaller text, muted color | ✓ |
| Error-style assistant bubble | Reuse assistant bubble shape with red/amber tint | |
| Dismissible toast at top | Temporary overlay, auto-dismisses | |

**User's choice:** Subtle center pill
**Notes:** Visually distinct from conversation bubbles; looks like a system status line

---

| Option | Description | Selected |
|--------|-------------|----------|
| Warnings/errors only | Rate-limit, fallback, API errors only | ✓ |
| All notable events | Including chat cleared, screenshot captured, etc. | |

**User's choice:** Warnings/errors only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Persist in history | Cleared on New Chat like any other message | ✓ |
| Ephemeral only | Cleared on chat clear, requires tracking ephemeral state | |

**User's choice:** Persist in history

---

| Option | Description | Selected |
|--------|-------------|----------|
| Filtered out from API | C# strips system role before building payload | ✓ |
| Included as context | Claude sees rate-limit warnings in history | |

**User's choice:** Filtered out

---

| Option | Description | Selected |
|--------|-------------|----------|
| Center pill for errors too | All system-generated notices use pill style | ✓ |
| Keep errors as assistant bubbles | [Error]: strings stay as assistant messages | |

**User's choice:** Center pill for errors — C# must write [Error]: messages with role "system"

---

## Loading Indicator

| Option | Description | Selected |
|--------|-------------|----------|
| No label — dots only | Minimal animation only | |
| "Thinking..." text | Small label beside dots | |
| Rotating status text | Cycles through city-flavored lines | ✓ |

**User's choice:** Rotating status text (immersive, city-flavored)

---

| Option | Description | Selected |
|--------|-------------|----------|
| No cancel button | Simple — errors surface naturally | ✓ |
| Cancel after timeout | After N seconds, cancel button appears | |

**User's choice:** No cancel button

---

| Option | Description | Selected |
|--------|-------------|----------|
| Disabled while loading | Current behavior | |
| Allow type-ahead | Input accepts text, one queued message | ✓ |

**User's choice:** Type-ahead with one-message queue

---

| Option | Description | Selected |
|--------|-------------|----------|
| Queue and auto-send after response | m_QueuedMessage, fires when PendingResult clears | ✓ |
| Show queued message in UI, send after | Same + queued indicator | |
| Reject mid-flight sends | Input only, no actual send | |

**User's choice:** Queue and auto-send

---

| Option | Description | Selected |
|--------|-------------|----------|
| One queued message max | Simple, prevents flooding | ✓ |
| Unlimited queue | Full FIFO | |

**User's choice:** One queued message max

---

| Option | Description | Selected |
|--------|-------------|----------|
| Stays in input box, send button grayed | Can edit before it sends | |
| Queued chip above input | Chip with ✕ to discard | ✓ |

**User's choice:** Queued chip above input

---

| Option | Description | Selected |
|--------|-------------|----------|
| Text below the dots in same bubble | Dots + rotating text in one animated element | ✓ |
| Text replaces the dots | Just rotating text, no dots | |
| Text in the panel header | Disconnected from conversation | |

**User's choice:** Text below dots in same bubble

---

| Option | Description | Selected |
|--------|-------------|----------|
| Disabled while loading | Current behavior | |
| Allow type-ahead | ✓ (already selected above) | ✓ |

**User's choice:** Allow type-ahead (confirmed again)

---

| Option | Description | Selected |
|--------|-------------|----------|
| Yes — with ✕ to discard | Same UX as screenshot chip | ✓ |
| No — queued messages always send | Simpler, less control | |

**User's choice:** Queued chip has ✕ to discard

---

| Option | Description | Selected |
|--------|-------------|----------|
| Claude's discretion | Planner picks phrases | ✓ |
| User provides them | Specific phrases locked in | |

**User's choice:** Claude's discretion for rotating status phrases

---

| Option | Description | Selected |
|--------|-------------|----------|
| No elapsed counter | Keep clean | ✓ |
| Elapsed counter after 10s | Show timer for slow responses | |

**User's choice:** No elapsed counter

---

## Empty State

| Option | Description | Selected |
|--------|-------------|----------|
| Welcome message from advisor | Narrative greeting, sets tone | ✓ |
| Prompt suggestions (clickable) | 2–3 starter questions | |
| Blank — just the input box | Minimal | |

**User's choice:** Welcome message from advisor

---

| Option | Description | Selected |
|--------|-------------|----------|
| Static message | Same every session | |
| Randomly picked from a set | Cycles through 4–5 welcome lines | ✓ |

**User's choice:** Randomly picked from a set

---

| Option | Description | Selected |
|--------|-------------|----------|
| Distinct centered greeting | Not a bubble — disappears on first message | ✓ |
| Assistant bubble style | Looks like Claude sent a message | |

**User's choice:** Distinct centered greeting

---

| Option | Description | Selected |
|--------|-------------|----------|
| Disappear instantly | No animation | ✓ |
| Fade out | CSS opacity transition | |

**User's choice:** Disappear instantly

---

| Option | Description | Selected |
|--------|-------------|----------|
| Same welcome message | New Chat re-shows same welcome set | ✓ |
| Different new-chat greeting | Shorter "Ready for next question" variant | |

**User's choice:** Same welcome for both first open and New Chat

---

## Markdown Edge Cases

| Option | Description | Selected |
|--------|-------------|----------|
| Nested/indented lists | Sub-bullets under top-level items | ✓ |
| Italic text (_word_) | Fix lookbehind regex for Coherent GT | ✓ |
| Bold + headers in same response | Ensure ## and ** don't interfere | ✓ |
| Code blocks with language hints | Language label above block | ✓ |

**User's choice:** All four

---

| Option | Description | Selected |
|--------|-------------|----------|
| Language label only, no highlighting | Small label above block | ✓ |
| Drop the language hint | Keep current (discard hint) | |
| Basic token coloring | Hand-rolled regex coloring | |

**User's choice:** Language label only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Strip <thinking> blocks silently | In C# before storing, or in renderMarkdown | ✓ |
| Show as collapsible block | details/summary — Coherent GT uncertain | |
| Show as blockquote style | Thinking exposed in chat | |

**User's choice:** Strip silently (preferred: in C# before storing to history)

---

| Option | Description | Selected |
|--------|-------------|----------|
| Best-effort — show what renders | Current behavior, no change | ✓ |
| Sanitize aggressively | Pre-process to close tags | |
| Show raw on parse failure | try/catch fallback to plain text | |

**User's choice:** Best-effort (current behavior)

---

| Option | Description | Selected |
|--------|-------------|----------|
| No — user messages stay plain | No markdown for user input | ✓ |
| Minimal rendering | Bold/italic for user messages | |

**User's choice:** User messages stay plain text

---

## Claude's Discretion

- Center pill exact CSS styling
- Rotating loading status phrases (5–6 city-flavored lines)
- Welcome greeting variants (4–5 lines)
- m_QueuedMessage placement and drain logic
- Whether `<thinking>` stripping happens in C# or renderMarkdown.ts

## Deferred Ideas

None — discussion stayed within phase scope.
