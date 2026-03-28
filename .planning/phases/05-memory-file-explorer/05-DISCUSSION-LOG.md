# Phase 5: Memory File Explorer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-28
**Phase:** 05-memory-file-explorer
**Areas discussed:** Panel navigation, File tree structure, Edit / save experience, Delete confirmation flow

---

## Panel Navigation

| Option | Description | Selected |
|--------|-------------|----------|
| Tab bar in panel header | Two tabs (`Advisor`/`Memory`) replacing the title text | ✓ |
| Icon button in header actions | Folder icon in existing action button row | |
| Two-pane split view | File tree left, content right — always visible | |

**User's choice:** Tab bar in panel header

---

| Option | Description | Selected |
|--------|-------------|----------|
| Replace: file list → file view | Click file to enter view mode; back button returns to list | ✓ |
| Always-split inside Memory tab | File list left, content right within Memory tab | |

**User's choice:** Replace (file list → file view)

---

| Option | Description | Selected |
|--------|-------------|----------|
| Trigger on tab open | JS triggers `refreshMemoryFiles`; C# calls ListFiles and updates binding | ✓ |
| Binding always live | C# pushes updates after every write/delete | |
| Trigger + refresh button | Trigger on open plus explicit manual refresh button | |

**User's choice:** Trigger on tab open

---

| Option | Description | Selected |
|--------|-------------|----------|
| Buttons in a sub-header bar | `← filename  [Edit] [Delete]` bar below tab | ✓ |
| Inline action row inside scroll area | Buttons at top of content area | |
| Context menu on file list item | Right-click or ⋯ button on list row | |

**User's choice:** Sub-header bar

---

| Option | Description | Selected |
|--------|-------------|----------|
| Reset to file list | Each open of Memory tab starts at file list | ✓ |
| Preserve last-viewed file | Remember open file across tab switches | |

**User's choice:** Reset to file list

---

| Option | Description | Selected |
|--------|-------------|----------|
| No — Memory tab replaces full content area | Input bar hidden when Memory tab active | ✓ |
| Yes — input bar stays at bottom | Chat input accessible from Memory tab | |

**User's choice:** Full content area replacement

---

| Option | Description | Selected |
|--------|-------------|----------|
| React local state only | `useState<'advisor' \| 'memory'>` — no C# binding | ✓ |
| C# binding for active tab | `ValueBinding<string> activeTab` | |

**User's choice:** React local state only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Edit shown, Delete hidden (core files) | Core files editable but not deletable | ✓ |
| Both shown, Delete disabled with tooltip | Delete grayed out for core files | |
| Read-only — neither Edit nor Delete | Core files view-only | |

**User's choice:** Edit shown, Delete hidden for core files

---

| Option | Description | Selected |
|--------|-------------|----------|
| No — just the list | No search; small file count makes it unnecessary | ✓ |
| Simple text filter | Filter input narrows visible files by name | |

**User's choice:** No search

---

## File Tree Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Root files only | 10 core + user/AI files; no subdirectory exposure | ✓ |
| Root files + collapsible chat-history/ | Expandable session file list | |
| Full tree — root + chat-history/ + archive/ | All levels accessible | |

**User's choice:** Root files only

---

| Option | Description | Selected |
|--------|-------------|----------|
| Core files first, then alphabetical | Protected files grouped at top with lock icon | ✓ |
| Purely alphabetical | All A–Z with lock icon on core files | |
| Most recently modified first | Files sorted by mtime | |

**User's choice:** Core files first, then alphabetical

---

| Option | Description | Selected |
|--------|-------------|----------|
| Filename + file size | `_index.md  4.2KB` | |
| Filename only | Name only, cleanest | |
| Filename + size + last modified | `_index.md  4.2KB  2d ago` | ✓ |

**User's choice:** Filename + size + last modified
**Notes:** Requires `last_modified_unix` added to `ListFiles()` output.

---

| Option | Description | Selected |
|--------|-------------|----------|
| Relative time — `2d ago`, `just now` | Human-readable, React-computed | ✓ |
| Absolute date — `Mar 26` | More precise for older files | |
| Both — relative + tooltip with full date | Hover tooltip with full timestamp | |

**User's choice:** Relative time

---

| Option | Description | Selected |
|--------|-------------|----------|
| Unix timestamp in seconds | `last_modified_unix: 1711468800` | ✓ |
| ISO 8601 string | `last_modified: "2026-03-26T14:00:00Z"` | |

**User's choice:** Unix timestamp in seconds

---

| Option | Description | Selected |
|--------|-------------|----------|
| New ValueBinding<string> memoryFilesJson | Dedicated binding for file list JSON | ✓ |
| PendingResult-style volatile field | Reuse drain pattern | |

**User's choice:** New `memoryFilesJson` ValueBinding

---

## Edit / Save Experience

| Option | Description | Selected |
|--------|-------------|----------|
| Textarea replaces read-only view | Content area swaps; sub-header shows Save/Cancel | ✓ |
| Edit mode overlaid on top | Textarea slides over existing view with animation | |

**User's choice:** Textarea replaces read-only view

---

| Option | Description | Selected |
|--------|-------------|----------|
| Trigger to C# — writeMemoryFile | `writeMemoryFile(filename, content)` trigger → memoryOpResult | ✓ |
| Trigger to C# — send as agent tool call | Route through Claude API write_memory_file tool | |

**User's choice:** Direct `writeMemoryFile` trigger

---

| Option | Description | Selected |
|--------|-------------|----------|
| Success: view mode. Failure: stay in edit + error | Non-destructive failure handling | ✓ |
| Optimistic: return to view immediately | No error handling | |

**User's choice:** Success returns to view; failure stays in edit with error message

---

| Option | Description | Selected |
|--------|-------------|----------|
| New ValueBinding<string> memoryOpResult | Single binding for read content + op results | ✓ |
| Piggyback on memoryFilesJson | Infer success from binding change | |
| Volatile PendingMemoryResult field | New drain loop | |

**User's choice:** New `memoryOpResult` ValueBinding

---

| Option | Description | Selected |
|--------|-------------|----------|
| React displays the content it just saved | No round-trip re-read on success | ✓ |
| C# re-reads and sends back via memoryOpResult | Round-trip guarantees disk match | |

**User's choice:** React displays saved content directly

---

| Option | Description | Selected |
|--------|-------------|----------|
| Return to view mode immediately, discard changes | No prompt on Cancel | ✓ |
| Prompt if unsaved changes exist | "Discard changes?" inline prompt | |

**User's choice:** Immediate discard on Cancel

---

| Option | Description | Selected |
|--------|-------------|----------|
| Trigger readMemoryFile(filename) → memoryOpResult | Read result goes to shared binding | ✓ |
| Separate memoryFileContent ValueBinding<string> | Dedicated binding for file content | |

**User's choice:** `readMemoryFile` trigger → `memoryOpResult` (shared binding)

---

## Delete Confirmation Flow

| Option | Description | Selected |
|--------|-------------|----------|
| Inline confirmation in sub-header | `Delete lore.md?  [Yes] [Cancel]` replaces action bar | ✓ |
| Delete immediately, no confirmation | Instant delete on click | |
| Modal overlay confirmation | Centered modal dialog | |

**User's choice:** Inline sub-header confirmation

---

| Option | Description | Selected |
|--------|-------------|----------|
| Back to the file list | Auto-navigate + refresh list on success | ✓ |
| Back to the file list with a success notice | Same + brief center-pill `lore.md deleted.` notice | |

**User's choice:** Back to file list (no notice)

---

| Option | Description | Selected |
|--------|-------------|----------|
| No — protected in NarrativeMemorySystem.DeleteFile() (already) | Existing C# guard is sufficient | ✓ |
| Add an extra guard in the UI too | Defense in depth (already covered by hiding Delete button) | |

**User's choice:** Existing C# protection is sufficient; UI hides Delete button for core files

---

| Option | Description | Selected |
|--------|-------------|----------|
| Trigger to C# — deleteMemoryFile → memoryOpResult | Wait for C# result before navigating | ✓ |
| Trigger + optimistic navigation | Return to list immediately without waiting | |

**User's choice:** Wait for C# result; navigate on `"ok"`, show error on failure

---

## Claude's Discretion

- Exact CSS class names for tab bar, sub-header, file list rows, file view area
- Lock icon implementation (emoji, text label, or CSS icon)
- Error display styling for write/delete failures
- `memoryOpResult` reset timing/strategy
- Whether file writes go async (Phase 1 pattern) or synchronous main-thread (fine for small files)

## Deferred Ideas

- Subdirectory browsing (chat-history/, archive/)
- File list search/filter
