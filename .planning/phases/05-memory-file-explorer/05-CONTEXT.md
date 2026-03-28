# Phase 5: Memory File Explorer - Context

**Gathered:** 2026-03-28
**Status:** Ready for planning

<domain>
## Phase Boundary

Add a "Memory" tab to the existing in-game panel so players can browse, read, edit, and delete
the per-city narrative memory files without leaving the game. The Memory tab is a fully React-side
UI feature wired to C# via new bindings and triggers. No new AI tool calls or API changes in this
phase — file I/O goes directly from React triggers → CityAgentUISystem → NarrativeMemorySystem.

</domain>

<decisions>
## Implementation Decisions

### Panel Navigation
- **D-01:** Two tabs in the existing panel header: `Advisor` and `Memory`. Tab bar replaces the
  current `CityAgent AI Advisor` title text. The close (×) button stays in the header at all times.
- **D-02:** Active tab state is managed in **React local state only** — `useState<'advisor' | 'memory'>`.
  No C# binding needed for tab state. C# only gets invoked when specific triggers fire.
- **D-03:** When the Memory tab is active, the **full content area** (chat messages, input bar,
  screenshot chip, send button) is replaced by the file explorer. The Advisor tab input bar is
  completely hidden while Memory is active.
- **D-04:** Switching tabs **resets the Memory tab to the file list view**. No state preservation
  across tab switches — each open of the Memory tab starts fresh at the file list.
- **D-05:** No search or filter functionality in Phase 5. The file count is small enough that a
  scrollable list is sufficient.

### File List — Data Loading
- **D-06:** A new **`refreshMemoryFiles` TriggerBinding** fires when the Memory tab is activated.
  C# calls `NarrativeMemorySystem.ListFiles()` and pushes the result to a new
  **`ValueBinding<string> memoryFilesJson`** binding (default `"[]"`). React reads via `useValue()`.
  This follows the exact same pattern as all existing bindings.
- **D-07:** The `memoryFilesJson` binding is **not kept live**. It is only refreshed on the
  `refreshMemoryFiles` trigger. No auto-push after every file write/delete.

### File List — Structure and Display
- **D-08:** Only **root-level files** are shown (the `{city-slug}/` directory, `*.md`).
  Subdirectories `chat-history/` and `archive/` are excluded from the explorer in Phase 5.
- **D-09:** Files are sorted: **core files first** (alphabetically), then **user/AI-created files**
  (alphabetically). Core files display a lock icon (🔒). `ListFiles()` already returns `is_core`.
- **D-10:** Each file row shows: **filename + file size + relative last modified time**
  (e.g. `_index.md    4.2KB    2d ago`). `ListFiles()` must be extended to include
  `last_modified_unix` (Unix timestamp in seconds, sourced from `File.GetLastWriteTimeUtc()`).
- **D-11:** Relative time formatting is implemented in React (`2d ago`, `just now`, `1h ago`, etc.)
  using `Date.now()` math against the unix timestamp. No external library.

### File View — Navigation
- **D-12:** Clicking a file enters a **file view mode** — the file list is replaced by the file
  content view. A sub-header bar appears below the tab bar:
  - Non-core files: `← filename.md   [Edit] [Delete]`
  - Core files: `← filename.md   [Edit]` (Delete button not shown — lock icon or "Core file" label)
- **D-13:** The `←` back button returns to the file list. No confirmation if unsaved changes exist
  (user explicitly clicks back — discard is intentional).
- **D-14:** File content is **fetched on click** via a new **`readMemoryFile` TriggerBinding<string>**
  (filename arg). C# calls `NarrativeMemorySystem.ReadFile()` and writes the result to
  **`ValueBinding<string> memoryOpResult`** (see D-17). React displays the result.

### Edit / Save
- **D-15:** Clicking Edit in the sub-header swaps the read-only `<div>` or `<pre>` content area
  for a `<textarea>` pre-populated with the file's current content. Sub-header changes to:
  `← filename.md   [Save] [Cancel]`.
- **D-16:** Clicking Cancel exits edit mode immediately, restoring the original content. No
  "Discard changes?" prompt.
- **D-17:** Clicking Save fires a new **`writeMemoryFile` TriggerBinding<string, string>**
  (filename, newContent). C# calls `NarrativeMemorySystem.WriteFile()` and writes `"ok"` or
  `"[Error]: ..."` to **`ValueBinding<string> memoryOpResult`**.
  - On `"ok"` → switch to view mode and display the saved content (React uses what it sent — no
    round-trip re-read). Reset `memoryOpResult` to `""`.
  - On error → stay in edit mode, show red error notice above the textarea. Do not reset
    `memoryOpResult` until the user retries or cancels.
- **D-18:** `memoryOpResult` is a single binding that serves two roles, disambiguated by panel state:
  - In **file-view mode**: contains the file content (from `readMemoryFile` trigger).
  - After a **write or delete operation**: contains `"ok"` or `"[Error]: ..."`.
  React knows which interpretation applies based on what it just triggered.

### Delete Confirmation
- **D-19:** Clicking Delete in the sub-header shows an **inline confirmation** — the sub-header
  replaces to: `Delete filename.md?   [Yes] [Cancel]`. No modal overlay.
- **D-20:** Clicking Yes fires a new **`deleteMemoryFile` TriggerBinding<string>** (filename). C#
  calls `NarrativeMemorySystem.DeleteFile()` and writes `"ok"` or `"[Error]: ..."` to
  `memoryOpResult`.
  - On `"ok"` → navigate back to file list + fire `refreshMemoryFiles` to reload the list.
  - On error → show an inline error in the sub-header area (stay on the file view, no delete).
- **D-21:** Core file protection is already enforced at the C# layer in `NarrativeMemorySystem.DeleteFile()`.
  No additional guard needed in Phase 5 beyond hiding the Delete button for core files in the UI.

### New C# ↔ React Binding Contract (Phase 5 additions)

| Direction | Name | Type | Meaning |
|-----------|------|------|---------|
| C# → JS | `memoryFilesJson` | string | JSON array of `{name, size_kb, is_core, last_modified_unix}` |
| C# → JS | `memoryOpResult` | string | File content (after readMemoryFile) or `"ok"`/`"[Error]: ..."` |
| JS → C# | `refreshMemoryFiles` | trigger | Reload and push memoryFilesJson |
| JS → C# | `readMemoryFile` | trigger(string) | Filename to read; result goes to memoryOpResult |
| JS → C# | `writeMemoryFile` | trigger(string, string) | Filename + new content; result in memoryOpResult |
| JS → C# | `deleteMemoryFile` | trigger(string) | Filename to delete; result in memoryOpResult |

### NarrativeMemorySystem.ListFiles() Changes
- **D-22:** Extend the JSON each entry returns to include `last_modified_unix` (long, Unix seconds):
  `File.GetLastWriteTimeUtc(filePath)` converted via `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`.

### Claude's Discretion
- Exact CSS class names for the tab bar, sub-header, file list rows, and file view — follow the
  `ca-` BEM convention established in Phase 2.
- Exact lock icon representation (could be 🔒 emoji via Twemoji, or a text `[core]` label, or a
  CSS icon) — pick what works reliably in Coherent GT.
- Error display styling for write/delete failures in the sub-header area — follow Phase 2 system
  notice patterns where applicable.
- `memoryOpResult` reset strategy — when/how C# resets the binding to `""` after a result is
  consumed (e.g., reset on next trigger fire is sufficient).
- Whether `writeMemoryFile` writes synchronously on the main thread (fine for small files) or
  spawns a background task — apply the async pattern from Phase 1 if appropriate.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Existing Binding Patterns
- `src/Systems/CityAgentUISystem.cs` — all existing ValueBinding/TriggerBinding patterns;
  Phase 5 adds 2 new bindings and 4 new triggers in this same file
- `UI/src/components/CityAgentPanel.tsx` — existing panel structure; Memory tab and explorer
  views are added here (or extracted to a child component)

### Memory System API
- `src/Systems/NarrativeMemorySystem.cs` — `ListFiles()` (needs last_modified_unix added),
  `ReadFile()`, `WriteFile()`, `DeleteFile()`, `CoreFiles` HashSet (for is_core)

### Requirements
- `MEM-01`, `MEM-02`, `MEM-03`, `MEM-04` in `.planning/REQUIREMENTS.md`

### Prior Phase Conventions
- `.planning/phases/02-chat-ui-polish/02-CONTEXT.md` — system/notice message styling (center pill),
  CSS `ca-` BEM conventions, sub-header patterns established in Phase 2
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — binding pattern, thread model

### CSS Conventions
- `UI/src/style.css` — all existing `ca-` styles; Phase 5 adds new selectors here

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `NarrativeMemorySystem.ListFiles()` — already returns `name`, `is_core`, `size`; needs
  `last_modified_unix` added (one field addition)
- `NarrativeMemorySystem.ReadFile(filename)` — ready to use; returns content string or `"[Error]: ..."`
- `NarrativeMemorySystem.WriteFile(filename, content)` — ready; returns `"ok"` or error string
- `NarrativeMemorySystem.DeleteFile(filename)` — ready; already enforces CoreFiles protection
- Existing `ValueBinding<string>` pattern (e.g., `m_MessagesJson`) — direct model for `memoryFilesJson`
  and `memoryOpResult`
- Existing `TriggerBinding<string>` pattern (e.g., `sendMessage`) — direct model for `readMemoryFile`,
  `deleteMemoryFile`
- Phase 2 center-pill notice style — candidate for inline error display in file view

### Established Patterns
- All bindings registered in `CityAgentUISystem.OnCreate()` via `AddBinding()`
- All state crosses C#↔JS as JSON strings or primitives (never complex objects directly)
- React reads with `useValue(bindValue<T>("cityAgent", "bindingName"))`
- Two-layer component: outer `CityAgentPanel` (binding init, error boundary) → inner `CityAgentInner`
  (all hooks + business logic) — Memory explorer lives inside `CityAgentInner`

### Integration Points
- `CityAgentUISystem.OnCreate()` — register 2 new bindings + 4 new triggers
- `CityAgentUISystem.OnUpdate()` — drain `memoryOpResult` if using a volatile intermediate
  (or write synchronously from trigger handlers for small file ops)
- `CityAgentInner` component — add tab state, Memory tab render, file list, file view, edit mode

</code_context>

<specifics>
## Specific Ideas

- Tab bar appearance: `[ Advisor ] [ Memory ]   ×` in the header — the existing header drag
  behavior must still work (the tab bar should be outside the draggable zone, or the header
  drag only activates on the non-tab portion)
- Sub-header file view bar: `← lore.md   [Edit] [Delete]` (or `[Edit]` + lock for core files)
- Inline delete confirmation in sub-header: `Delete lore.md?  [Yes] [Cancel]`
- Relative time: `just now`, `Xm ago`, `Xh ago`, `Xd ago`, `Xw ago` — implemented with simple
  `Date.now()` arithmetic in a pure utility function

</specifics>

<deferred>
## Deferred Ideas

- Subdirectory browsing (`chat-history/`, `archive/`) — scoped out of Phase 5; could be Phase 6.5
  or a backlog item
- File list search/filter — scoped out; low value given small file count
- `memoryOpResult` reset-on-consumption protocol — Claude's discretion; no specific user requirement

</deferred>

---

*Phase: 05-memory-file-explorer*
*Context gathered: 2026-03-28*
