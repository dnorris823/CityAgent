---
phase: 02-chat-ui-polish
plan: 03
subsystem: ui
tags: [markdown, renderMarkdown, coherent-gt, typescript, css]

# Dependency graph
requires: []
provides:
  - "renderMarkdown.ts: lookbehind-free italic regex safe for Coherent GT's older V8"
  - "renderMarkdown.ts: two-level indent-aware unordered list rendering"
  - "renderMarkdown.ts: fenced code block language label emitted as .ca-code-lang span"
  - "style.css: .ca-code-lang CSS rule with top-rounded corners and dark panel theme"
affects: [CityAgentPanel, chat-ui-polish, renderMarkdown]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Coherent GT-safe regex: use character class boundary groups instead of lookbehind assertions"
    - "var-only declarations in renderMarkdown.ts for older Chromium compatibility"

key-files:
  created: []
  modified:
    - UI/src/utils/renderMarkdown.ts
    - UI/src/style.css

key-decisions:
  - "Italic regex: replaced (?<!\\w)_(...)_(?!\\w) lookbehind with (^|[\\s.,...])-bounded character class — Coherent GT's V8 doesn't support lookbehind assertions"
  - "Nested list: used relative baseIndent comparison (itemIndent > baseIndent) rather than fixed 2-space threshold — Claude uses both 2-space and 4-space indent"
  - "Language label: emitted as block-level <span class=ca-code-lang> before <pre>, not inside pre — allows CSS top-rounded-corner styling to merge visually with code block"

patterns-established:
  - "Coherent GT regex safety: no lookbehind (?<!), no lookahead with complex patterns — use capturing groups and replace functions instead"
  - "renderMarkdown.ts convention: all declarations use var (not const/let) for Coherent GT compatibility"

requirements-completed: [UI-02]

# Metrics
duration: 8min
completed: 2026-03-28
---

# Phase 02 Plan 03: renderMarkdown Rendering Fixes Summary

**Four targeted markdown rendering bug fixes: Coherent GT–safe italic regex, relative-indent nested lists, code block language labels, and bold+heading coexistence verified**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-03-28T09:15Z (approx)
- **Completed:** 2026-03-28
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Replaced `(?<!\w)_..._(?!\w)` lookbehind italic regex with a Coherent GT-compatible character class boundary approach — prevents V8 crash in the embedded browser
- Rewrote unordered list handler to track `baseIndent` from the first list item, making sub-item detection relative rather than fixed-threshold — correctly handles both 2-space and 4-space indent
- Added `<span class="ca-code-lang">` emission before `<pre>` blocks when a language tag is present on the opening fence
- Added `.ca-code-lang` CSS rule: block-level monospace label with top-rounded corners that visually merges with the code block below
- All four `must_haves.truths` from the plan are satisfied; `npm run build` exits 0

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix renderMarkdown.ts — italic regex, nested lists, code language label** - `eee63c7` (fix)
2. **Task 2: Add .ca-code-lang CSS rule to style.css** - `0bd6b3d` (feat)

**Plan metadata:** (docs commit below)

## Files Created/Modified
- `UI/src/utils/renderMarkdown.ts` - Three targeted fixes: italic regex, list nesting, code language label
- `UI/src/style.css` - Added `.ca-code-lang` block-level span styling

## Decisions Made
- Replaced lookbehind with `(^|[\s.,!?;:'"([{])_([^_]+?)_([\s.,!?;:'")\]}]|$)` character class boundary — same word-boundary intent, no lookbehind required
- Nested list uses `itemIndent > baseIndent` (relative) not `itemIndent >= 2` (absolute) — Claude's indentation is inconsistent between responses
- Language label is a `<span>` (not inside `<pre>`) so CSS `border-radius: 3px 3px 0 0` on the label and no top-radius on `<pre>` gives a visually connected block

## Deviations from Plan

None - plan executed exactly as written. All code uses `var` declarations per Coherent GT convention. Processing order in `inlinePass()` unchanged (bold+italic before bold before italic confirms D-16).

## Issues Encountered
None — TypeScript build passed on first attempt for both tasks.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `renderMarkdown.ts` is now correct for Claude's actual output patterns (nested lists, language-tagged code blocks, underscore italics)
- `.ca-code-lang` CSS rule is live in `CityAgent.css` and will be applied in-game once the mod is deployed
- D-14, D-15, D-16, D-17 from the research doc are all addressed

---
*Phase: 02-chat-ui-polish*
*Completed: 2026-03-28*
