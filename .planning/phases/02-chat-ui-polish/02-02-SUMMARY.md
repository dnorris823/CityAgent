---
phase: 02-chat-ui-polish
plan: 02
subsystem: UI
tags: [react, css, ui-polish, loading-state, message-rendering, queue]
dependency_graph:
  requires: [02-01]
  provides: [system-message-pill, loading-status-text, queued-message-chip, welcome-block]
  affects: [UI/src/components/CityAgentPanel.tsx, UI/src/style.css]
tech_stack:
  added: []
  patterns:
    - "useRef for double-send prevention (pendingQueuedMsg)"
    - "setInterval + clearInterval in useEffect for phrase cycling"
    - "3-way conditional renderer (user / assistant / system)"
key_files:
  created: []
  modified:
    - UI/src/components/CityAgentPanel.tsx
    - UI/src/style.css
decisions:
  - "Store queued message in useRef not useState to prevent double-send on re-render"
  - "LOADING_PHRASES and WELCOME_GREETINGS declared inline in component (no module-scope constant) to keep context local"
metrics:
  duration: ~8 minutes
  completed: 2026-03-29T03:23:50Z
  tasks_completed: 2
  files_modified: 2
---

# Phase 02 Plan 02: Chat UI Polish — 3-way Renderer and Loading UX Summary

React and CSS changes to deliver polished 3-way message rendering, an immersive loading state with rotating status text, a type-ahead message queue chip, and an empty-state welcome greeting.

## What Was Built

**System message notice pills** — `role: "system"` messages no longer render as assistant bubbles. They appear as full-width center-aligned pills styled red (`.ca-notice-pill--error`) when the content starts with `[Error]:`, or amber (`.ca-notice-pill--warning`) for all other system messages. The `[Error]:` prefix is stripped from the display text.

**Loading status text** — The loading bubble now shows bouncing dots plus an italic status phrase below them (`.ca-loading-status`). Six city-flavored phrases cycle every 2.5 seconds via `setInterval` while `isLoading` is true. The interval is cleaned up with `clearInterval` in the effect return to prevent stale `setState` calls. The phrase resets to index 0 when loading ends.

**Type-ahead message queue** — While a request is in flight, the user can type and press Send. The message is stored in `pendingQueuedMsg` (a `useRef`, not state — this prevents the double-send that would occur if state triggered a re-render mid-effect) and displayed as a `.ca-queued-chip` above the input row. The chip shows a truncated preview (40 chars max) and a dismiss button. A second `useEffect` watching `isLoading` fires the queued message as soon as loading transitions to false. `canSend` blocks a second queue while a chip is already visible.

**Welcome greeting** — When `messages.length === 0 && !isLoading`, a `.ca-welcome` block renders inside `.ca-messages` with a randomly selected greeting from a pool of 5. The greeting is chosen once via `useState` initializer (lazy form) so it is stable per component mount. The block disappears instantly when the first message arrives.

## Tasks Completed

| # | Name | Commit | Files |
|---|------|--------|-------|
| 1 | Add CSS classes for all new Phase 2 components | c63ed82 | UI/src/style.css |
| 2 | CityAgentPanel.tsx — 3-way renderer, loading status, queued chip, welcome block | 0615e83 | UI/src/components/CityAgentPanel.tsx |

## Deviations from Plan

None — plan executed exactly as written. Changes 3 and 4 (the two new `useEffect` hooks) were consolidated into a single edit with Change 2 (state declarations) to reduce edit count, but the code produced is identical to the plan spec.

## Known Stubs

None. All features are fully wired:
- System pill: consumes real `role: "system"` messages from the `messagesJson` binding populated by C# (implemented in 02-01)
- Loading status: driven by the live `isLoading` binding
- Queue chip: driven by live `isLoading` + user input
- Welcome block: driven by `messages.length` from the live `messagesJson` binding

## Verification

Full build gate: `npm run build` passed with 0 TypeScript errors. Output: `CityAgent.mjs` 13.8 KiB, `CityAgent.css` 11.9 KiB.

In-game verification checklist (requires full mod deployment):
1. Open panel — welcome greeting visible (one of 5 variants)
2. Send a message — welcome disappears, user bubble right-aligned blue
3. While loading, type and Send — chip appears with preview text, input clears
4. Click chip ✕ — chip gone, no message sent
5. Loading ends with chip present — message auto-sends
6. API error — appears as red center pill, not assistant bubble
7. Loading bubble shows dots + italic rotating phrase changing every ~2.5 s

## Self-Check: PASSED

Files modified:
- FOUND: UI/src/components/CityAgentPanel.tsx
- FOUND: UI/src/style.css

Commits:
- FOUND: c63ed82 (feat(02-02): add CSS...)
- FOUND: 0615e83 (feat(02-02): 3-way renderer...)
