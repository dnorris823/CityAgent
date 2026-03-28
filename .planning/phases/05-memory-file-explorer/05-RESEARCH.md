# Phase 5: Memory File Explorer — Research

**Researched:** 2026-03-28
**Domain:** CS2 mod UI — React panel extension, C# binding additions, file I/O wiring
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D-01:** Two tabs in the existing panel header: `Advisor` and `Memory`. Tab bar replaces the
current `CityAgent AI Advisor` title text. The close (×) button stays in the header at all times.

**D-02:** Active tab state is managed in React local state only — `useState<'advisor' | 'memory'>`.
No C# binding needed for tab state. C# only gets invoked when specific triggers fire.

**D-03:** When the Memory tab is active, the full content area (chat messages, input bar,
screenshot chip, send button) is replaced by the file explorer. The Advisor tab input bar is
completely hidden while Memory is active.

**D-04:** Switching tabs resets the Memory tab to the file list view. No state preservation
across tab switches — each open of the Memory tab starts fresh at the file list.

**D-05:** No search or filter functionality in Phase 5.

**D-06:** A new `refreshMemoryFiles` TriggerBinding fires when the Memory tab is activated. C#
calls `NarrativeMemorySystem.ListFiles()` and pushes the result to a new
`ValueBinding<string> memoryFilesJson` binding (default `"[]"`). React reads via `useValue()`.

**D-07:** `memoryFilesJson` binding is NOT kept live. Only refreshed on the `refreshMemoryFiles` trigger.

**D-08:** Only root-level files shown (the `{city-slug}/` directory, `*.md`). Subdirectories
`chat-history/` and `archive/` are excluded.

**D-09:** Files sorted: core files first (alphabetically), then user/AI-created files (alphabetically).
Core files display a lock icon (🔒). `ListFiles()` already returns `is_core`.

**D-10:** Each file row shows: filename + file size + relative last modified time
(e.g. `_index.md    4.2KB    2d ago`). `ListFiles()` must be extended to include
`last_modified_unix` (Unix timestamp in seconds, sourced from `File.GetLastWriteTimeUtc()`).

**D-11:** Relative time formatting implemented in React using `Date.now()` math. No external library.

**D-12:** Clicking a file enters file view mode. Sub-header bar appears below the tab bar:
- Non-core files: `← filename.md   [Edit] [Delete]`
- Core files: `← filename.md   [Edit]` (Delete button not shown)

**D-13:** The `←` back button returns to the file list. No unsaved-changes confirmation.

**D-14:** File content fetched on click via a new `readMemoryFile` TriggerBinding<string>`.
Result written to `ValueBinding<string> memoryOpResult`.

**D-15:** Clicking Edit swaps read-only display for a `<textarea>` pre-populated with content.
Sub-header changes to: `← filename.md   [Save] [Cancel]`.

**D-16:** Clicking Cancel exits edit mode immediately. No prompt.

**D-17:** Clicking Save fires `writeMemoryFile` TriggerBinding<string, string>` (filename,
newContent). C# writes `"ok"` or `"[Error]: ..."` to `memoryOpResult`.
- On `"ok"` → switch to view mode. No round-trip re-read (React uses what it sent). Reset `memoryOpResult` to `""`.
- On error → stay in edit mode, show red error notice above textarea.

**D-18:** `memoryOpResult` serves two roles disambiguated by React panel state:
- In file-view mode: contains file content (from `readMemoryFile`).
- After write/delete: contains `"ok"` or `"[Error]: ..."`.

**D-19:** Clicking Delete shows inline confirmation in sub-header:
`Delete filename.md?   [Yes] [Cancel]`. No modal overlay.

**D-20:** Clicking Yes fires `deleteMemoryFile` TriggerBinding<string>`. C# writes `"ok"` or
`"[Error]: ..."` to `memoryOpResult`.
- On `"ok"` → navigate back to file list + fire `refreshMemoryFiles`.
- On error → show inline error in sub-header area.

**D-21:** Core file protection already enforced at C# layer in `NarrativeMemorySystem.DeleteFile()`.
Phase 5 adds UI guard only (hide Delete button for core files).

**D-22:** Extend `ListFiles()` JSON to include `last_modified_unix` (long, Unix seconds) from
`File.GetLastWriteTimeUtc()` converted via `DateTimeOffset`.

### New C# Binding Contract (Phase 5 Additions)

| Direction | Name | Type | Meaning |
|-----------|------|------|---------|
| C# → JS | `memoryFilesJson` | string | JSON array of `{name, size_kb, is_core, last_modified_unix}` |
| C# → JS | `memoryOpResult` | string | File content or `"ok"`/`"[Error]: ..."` |
| JS → C# | `refreshMemoryFiles` | trigger | Reload and push memoryFilesJson |
| JS → C# | `readMemoryFile` | trigger(string) | Filename to read; result → memoryOpResult |
| JS → C# | `writeMemoryFile` | trigger(string, string) | Filename + new content; result → memoryOpResult |
| JS → C# | `deleteMemoryFile` | trigger(string) | Filename to delete; result → memoryOpResult |

### Claude's Discretion
- Exact CSS class names for tab bar, sub-header, file list rows, file view (follow `ca-` BEM convention)
- Lock icon representation (🔒 emoji via Twemoji, or text `[core]` label — pick what works reliably in Coherent GT)
- Error display styling for write/delete failures
- `memoryOpResult` reset strategy — reset on next trigger fire is sufficient
- Whether `writeMemoryFile` writes synchronously or spawns a background task

### Deferred Ideas (OUT OF SCOPE)
- Subdirectory browsing (`chat-history/`, `archive/`)
- File list search/filter
- `memoryOpResult` reset-on-consumption protocol (Claude's discretion)
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| MEM-01 | In-panel file tree view displays all per-city narrative memory files organized by directory | `NarrativeMemorySystem.ListFiles()` returns root-level `*.md` files with metadata; React renders sorted list using `memoryFilesJson` binding |
| MEM-02 | User can click any file to view its full contents in the panel | `NarrativeMemorySystem.ReadFile()` returns content string; `readMemoryFile` trigger → `memoryOpResult` binding covers this |
| MEM-03 | User can edit file contents directly in the panel and save changes back to disk | `NarrativeMemorySystem.WriteFile()` is ready; `writeMemoryFile TriggerBinding<string,string>` confirmed available; `<textarea>` works in Coherent GT |
| MEM-04 | User can delete non-protected memory files (protected core files are read-only) | `NarrativeMemorySystem.DeleteFile()` already enforces `CoreFiles` protection; `deleteMemoryFile TriggerBinding<string>` covers this |
</phase_requirements>

---

## Summary

Phase 5 is a UI extension — no new AI API surface, no new ECS queries. All four requirements
are satisfied by wiring existing `NarrativeMemorySystem` methods to new C# bindings/triggers and
building a multi-view React component inside `CityAgentInner`.

The C# side requires: (1) adding `last_modified_unix` to `ListFiles()` output, (2) adding two
new `ValueBinding<string>` fields to `CityAgentUISystem`, and (3) registering four new
`TriggerBinding` variants in `OnCreate()`. The binding types (`TriggerBinding<T1, T2>`) are
confirmed present in `Colossal.UI.Binding.dll`.

The React side requires: (1) adding two new `bindValue` / `useValue` calls in `ensureBindings()` /
`CityAgentInner`, (2) a tab bar replacing the current title span in the header, and (3) conditional
rendering of the memory explorer (file list, file view, edit mode) vs. the existing chat content.

The thread-safety question for file writes is LOW risk: `WriteFile` and `DeleteFile` are small
synchronous file operations called from the trigger handler on the game's UI thread — the same
thread that handles all other binding updates. No background task is required for Phase 5.

**Primary recommendation:** Follow the exact patterns from existing bindings and triggers in
`CityAgentUISystem.cs`; no new patterns are needed.

---

## Standard Stack

Phase 5 introduces no new libraries. All dependencies are already present.

### Core (all already in project)
| Component | Version / Source | Purpose |
|-----------|-----------------|---------|
| `Colossal.UI.Binding` | Game DLL (CS2 Managed) | `ValueBinding<string>`, `TriggerBinding<T>`, `TriggerBinding<T1,T2>` |
| `Newtonsoft.Json` | Game DLL (CS2 Managed) | JSON serialization for `ListFiles()` output |
| React 18 | Runtime-injected by Coherent GT | Component model, hooks |
| TypeScript 5.3 | UI devDependency | Type checking |
| `cs2/api` | Game runtime injection | `bindValue`, `useValue`, `trigger` |

### Confirmed Available (from DLL inspection)
`TriggerBinding<T1, T2>` constructor signature (verified against live DLL):
```
Void .ctor(string group, string name, Action<T1,T2> callback,
           IReader<T1> reader1, IReader<T2> reader2)
```

The two-argument generic form exists alongside `TriggerBinding`, `TriggerBinding<T>`,
`TriggerBinding<T1,T2,T3>`, and `TriggerBinding<T1,T2,T3,T4>`.

The JS-side `trigger(group, name, ...args)` already uses rest params — confirmed in
`UI/types/cs2-api.d.ts` line 14. Passing two string args works without any type change.

---

## Architecture Patterns

### Recommended Project Structure (no new files required)

Phase 5 modifies two existing files and optionally extracts a child component:

```
src/Systems/
├── CityAgentUISystem.cs    ← add 2 ValueBindings + 4 TriggerBindings + 4 handler methods
└── NarrativeMemorySystem.cs ← change ListFiles() output shape only (add last_modified_unix)

UI/src/
├── components/
│   ├── CityAgentPanel.tsx   ← add tab bar, Memory tab rendering, new useValue calls
│   └── MemoryExplorer.tsx   ← OPTIONAL: extract memory explorer sub-component from CityAgentInner
└── style.css                ← add new ca-* selectors
```

Extracting `MemoryExplorer.tsx` is recommended to keep `CityAgentPanel.tsx` readable, but it is
not required — the planner may choose either approach.

### Pattern 1: ValueBinding Registration (existing — follow exactly)

```csharp
// In CityAgentUISystem.OnCreate():
private ValueBinding<string> m_MemoryFilesJson  = null!;
private ValueBinding<string> m_MemoryOpResult   = null!;

// Registration:
m_MemoryFilesJson = new ValueBinding<string>(kGroup, "memoryFilesJson", "[]");
m_MemoryOpResult  = new ValueBinding<string>(kGroup, "memoryOpResult",  "");
AddBinding(m_MemoryFilesJson);
AddBinding(m_MemoryOpResult);
```

Source: `CityAgentUISystem.cs` lines 43–59 (existing pattern).

### Pattern 2: TriggerBinding Registration (existing — follow exactly)

```csharp
// No-arg trigger (refreshMemoryFiles):
AddBinding(new TriggerBinding(kGroup, "refreshMemoryFiles", OnRefreshMemoryFiles));

// Single-string trigger (readMemoryFile, deleteMemoryFile):
AddBinding(new TriggerBinding<string>(kGroup, "readMemoryFile",   OnReadMemoryFile));
AddBinding(new TriggerBinding<string>(kGroup, "deleteMemoryFile", OnDeleteMemoryFile));

// Two-string trigger (writeMemoryFile) — confirmed available:
AddBinding(new TriggerBinding<string, string>(kGroup, "writeMemoryFile", OnWriteMemoryFile));
```

Source: `CityAgentUISystem.cs` lines 61–65; DLL inspection confirms `TriggerBinding<T1,T2>`.

### Pattern 3: Trigger Handler Methods (existing convention)

```csharp
private void OnRefreshMemoryFiles()
{
    if (!m_NarrativeMemory.IsInitialized) return;
    string json = m_NarrativeMemory.ListFiles();
    m_MemoryFilesJson.Update(json);
}

private void OnReadMemoryFile(string filename)
{
    if (!m_NarrativeMemory.IsInitialized)
    {
        m_MemoryOpResult.Update("[Error]: Memory system not initialized.");
        return;
    }
    string content = m_NarrativeMemory.ReadFile(filename);
    m_MemoryOpResult.Update(content);
}

private void OnWriteMemoryFile(string filename, string content)
{
    if (!m_NarrativeMemory.IsInitialized)
    {
        m_MemoryOpResult.Update("[Error]: Memory system not initialized.");
        return;
    }
    string result = m_NarrativeMemory.WriteFile(filename, content);
    // WriteFile returns "Successfully wrote N characters to filename." on success
    // or "[Error]: ..." on failure.
    // Normalize to "ok" for the React layer:
    m_MemoryOpResult.Update(result.StartsWith("[Error]") ? result : "ok");
}

private void OnDeleteMemoryFile(string filename)
{
    if (!m_NarrativeMemory.IsInitialized)
    {
        m_MemoryOpResult.Update("[Error]: Memory system not initialized.");
        return;
    }
    string result = m_NarrativeMemory.DeleteFile(filename);
    m_MemoryOpResult.Update(result.StartsWith("[Error]") ? result : "ok");
}
```

### Pattern 4: React Binding Initialization (existing — add to ensureBindings())

```typescript
let memoryFilesJson$: any = null;
let memoryOpResult$:  any = null;

function ensureBindings() {
  // ... existing bindings ...
  memoryFilesJson$ = bindValue<string>("cityAgent", "memoryFilesJson");
  memoryOpResult$  = bindValue<string>("cityAgent", "memoryOpResult");
}
```

### Pattern 5: Tab State and Content Switching (new — inside CityAgentInner)

```typescript
const [activeTab, setActiveTab] = useState<'advisor' | 'memory'>('advisor');
const memoryFilesJson = useValue(memoryFilesJson$) as string || "[]";
const memoryOpResult  = useValue(memoryOpResult$)  as string || "";

// Tab switch handler — reset memory sub-state on every Memory tab open
const handleMemoryTab = () => {
  setActiveTab('memory');
  // Reset sub-state here (file view, edit mode) if state lives in parent
  safeTrigger("cityAgent", "refreshMemoryFiles");
};
```

### Pattern 6: `ListFiles()` Extension — Adding `last_modified_unix`

Current `ListFiles()` output (lines 443–452 of `NarrativeMemorySystem.cs`):
```json
{
  "filename": "_index.md",
  "size_bytes": 1234,
  "last_modified": "2026-03-28T10:00:00.0000000Z",
  "is_core": true
}
```

Required change — add one field, rename/keep existing fields:
```csharp
files.Add(new
{
    name              = name,           // renamed from "filename" per D-10 (use "name")
    size_bytes        = info.Length,
    last_modified_unix = new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds(),
    is_core           = CoreFiles.Contains(name)
});
```

**Note:** The CONTEXT.md binding contract shows the field as `size_kb`, not `size_bytes`. The
current code stores `size_bytes` (raw long). The planner must choose: either rename the field and
divide by 1024.0 in C#, or keep `size_bytes` and format `X.Xkb` in React. Both work; the field
name in the JSON must match what React reads.

**Note on subdirectory exclusion (D-08):** `Directory.GetFiles(m_CityDir, "*.md")` already
returns only root-level files — subdirectory files are excluded by `GetFiles()` without
`SearchOption.AllDirectories`. No additional filter needed for D-08.

### Pattern 7: Relative Time in React (no library)

```typescript
function relativeTime(unixSeconds: number): string {
  var diffMs  = Date.now() - unixSeconds * 1000;
  var diffSec = Math.floor(diffMs / 1000);
  if (diffSec < 60)  return 'just now';
  var diffMin = Math.floor(diffSec / 60);
  if (diffMin < 60)  return diffMin + 'm ago';
  var diffHr  = Math.floor(diffMin / 60);
  if (diffHr  < 24)  return diffHr  + 'h ago';
  var diffDay = Math.floor(diffHr  / 24);
  if (diffDay < 7)   return diffDay + 'd ago';
  return Math.floor(diffDay / 7) + 'w ago';
}
```

Note: uses `var` (not `const`/`let`) consistent with the `renderMarkdown.ts` convention for
Coherent GT compatibility (see `UI/src/utils/renderMarkdown.ts` convention note in CLAUDE.md).

### Header Structure After Phase 5

Current header (lines 305–315 of `CityAgentPanel.tsx`):
```tsx
<header className="ca-panel__header" onMouseDown={handleHeaderMouseDown}>
  <span className="ca-panel__header-title">CityAgent AI Advisor</span>
  <div className="ca-panel__header-actions" onMouseDown={stopDragPropagation}>
    <button className="ca-btn-icon ca-btn-new-chat" ...>+ New Chat</button>
    <button className="ca-btn-icon" ...>✕</button>
  </div>
</header>
```

Phase 5 replaces `<span className="ca-panel__header-title">` with a tab bar:
```tsx
<header className="ca-panel__header" onMouseDown={handleHeaderMouseDown}>
  <div className="ca-tab-bar" onMouseDown={stopDragPropagation}>
    <button className={`ca-tab ${activeTab === 'advisor' ? 'ca-tab--active' : ''}`}
            onClick={() => setActiveTab('advisor')}>Advisor</button>
    <button className={`ca-tab ${activeTab === 'memory' ? 'ca-tab--active' : ''}`}
            onClick={handleMemoryTab}>Memory</button>
  </div>
  <div className="ca-panel__header-actions" onMouseDown={stopDragPropagation}>
    {activeTab === 'advisor' && (
      <button className="ca-btn-icon ca-btn-new-chat" ...>+ New Chat</button>
    )}
    <button className="ca-btn-icon" ...>✕</button>
  </div>
</header>
```

The drag behavior must not activate when clicking tabs. The `stopDragPropagation` pattern
(already used on `ca-panel__header-actions`) must also be applied to `ca-tab-bar`.

### Anti-Patterns to Avoid

- **Don't use `OnUpdate()` drain for memory ops.** Unlike `ClaudeAPISystem.PendingResult`
  (written from a thread-pool thread), all four new trigger handlers run synchronously on the
  game's UI thread. There is no cross-thread write. `m_MemoryOpResult.Update(...)` called
  directly inside the handler is correct and sufficient — no volatile field, no drain loop needed.
- **Don't pass complex objects across the C#↔JS bridge.** File list must be serialized to a JSON
  string and passed as `ValueBinding<string>`. File content is already a string.
- **Don't use CSS Grid.** All layout in memory explorer must be flexbox. (Coherent GT constraint
  from REQUIREMENTS.md Out of Scope table.)
- **Don't use `Array.at()`** or other modern JS features absent from Coherent GT's older Chromium.
- **Don't add `::placeholder` or `:disabled` pseudo-selectors.** Use `[disabled]` attribute
  selector (established CSS convention from `style.css`).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Core file protection | Custom guard in React | `NarrativeMemorySystem.CoreFiles` (already enforced in `DeleteFile()`) | Already implemented; UI just hides the button |
| File metadata | Custom file scanner | `NarrativeMemorySystem.ListFiles()` (extend with `last_modified_unix`) | Already returns name, size, is_core |
| File read/write/delete | Direct file I/O in C# trigger handler | `NarrativeMemorySystem` methods | Path validation (`ValidateFilename`), error handling already in place |
| JSON parse for file list | Custom parser | `JSON.parse()` in React (same pattern as `messagesJson`) | Established binding pattern |
| Markdown display of file content | New renderer | Existing `renderMarkdown()` already available if needed; or `<pre>` for raw display | File content is raw markdown — `<pre>` suffices for a viewer |

---

## NarrativeMemorySystem API Reference

### `ListFiles()` — Current Signature
```csharp
public string ListFiles()
```
- Returns: JSON string (array of objects) or `"[Error]: Memory system not initialized."` if uninitialized
- Current fields per entry: `filename`, `size_bytes` (long), `last_modified` (ISO 8601 string), `is_core` (bool)
- Excludes subdirectory files: `Directory.GetFiles(m_CityDir, "*.md")` is flat by default
- Does NOT currently include `last_modified_unix` — **must be added** (D-22)

### `ReadFile(string filename)` — Signature
```csharp
public string ReadFile(string filename)
```
- Returns: File contents as a string on success
- Returns: `"[Error]: Memory system not initialized."` if uninitialized
- Returns: `"[Error]: Filename cannot be empty."` if filename is blank
- Returns: `"[Error]: Invalid filename. Path traversal is not allowed."` if `..`, `/`, or `\` in name
- Returns: `"[Error]: File 'filename' does not exist."` if file not found
- Does NOT throw — always returns a string

### `WriteFile(string filename, string content)` — Signature
```csharp
public string WriteFile(string filename, string content)
```
- Returns: `"Successfully wrote N characters to filename."` on success
- Returns: `"[Error]: Memory system not initialized."` if uninitialized
- Returns: `"[Error]: Filename cannot be empty."` or path traversal error (same as ReadFile)
- Returns: `"[Error]: File 'filename' does not exist. Use create_memory_file to create new files."` if file missing
- Does NOT create new files — file must already exist
- Does NOT throw — always returns a string
- **Thread safety:** Runs synchronously on caller's thread (trigger handler = UI thread). Safe.

### `DeleteFile(string filename)` — Signature
```csharp
public string DeleteFile(string filename)
```
- Returns: `"Successfully deleted filename."` on success
- Returns: `"[Error]: 'filename' is a core memory file and cannot be deleted."` for core files
- Returns: `"[Error]: File 'filename' does not exist."` if file not found
- Returns: `"[Error]: Memory system not initialized."` if uninitialized
- Returns: path traversal errors (same as ReadFile)
- Does NOT throw — always returns a string
- `CoreFiles` HashSet (case-insensitive): `_index.md`, `characters.md`, `districts.md`, `city-plan.md`,
  `narrative-log.md`, `challenges.md`, `milestones.md`, `style-notes.md`, `economy.md`, `lore.md`

### Success String Normalization
`WriteFile` and `DeleteFile` return verbose success strings, not `"ok"`. The C# trigger handlers
must normalize these to `"ok"` before pushing to `m_MemoryOpResult`, to keep the React-side
interpretation simple (React only needs to distinguish `"ok"` from `"[Error]: ..."`).

---

## Thread Model for New Triggers

**Question from scope:** Does the `memoryOpResult` drain pattern require `OnUpdate()` polling,
or can trigger handlers write synchronously?

**Answer (HIGH confidence):** Synchronous writes from trigger handlers are correct.

Trigger handlers in `UISystemBase` run on the game's UI thread (the same thread as `OnUpdate()`).
This is identical to how `TogglePanel()` and `OnClearChat()` call `m_PanelVisible.Update()` and
`m_MessagesJson.Update()` directly without any polling loop. The `OnUpdate()` drain pattern
(`m_ClaudeAPI.PendingResult`) only exists because `ClaudeAPISystem.RunRequestAsync` writes from
a .NET thread-pool thread — a cross-thread scenario. Memory file operations are synchronous and
fast (small text files), so no background thread or drain loop is needed.

If Phase 1's async refactor of `NarrativeMemorySystem` is completed first, `WriteFile` and
`DeleteFile` may become async. In that case, the trigger handler would need to await the result
or use a continuation to push to `m_MemoryOpResult`. The planner should note this dependency:
**Phase 5 assumes synchronous file I/O from trigger handlers, which is valid as long as Phase 1's
async refactor keeps `WriteFile`/`DeleteFile` synchronous at the public API level.**

---

## Common Pitfalls

### Pitfall 1: Tab bar captures drag events
**What goes wrong:** Clicking a tab triggers a drag start on the header, moving the panel.
**Why it happens:** `handleHeaderMouseDown` is on the entire `<header>` element.
**How to avoid:** Wrap the tab bar in a `<div onMouseDown={stopDragPropagation}>` — the same
technique already used for `ca-panel__header-actions` (line 307 of `CityAgentPanel.tsx`).
**Warning signs:** Panel moves when you click a tab.

### Pitfall 2: `memoryOpResult` stale value causes wrong UI state
**What goes wrong:** After a successful write, `memoryOpResult` still holds `"ok"` from a
previous operation. On the next read, React sees `"ok"` and misinterprets it as the file content.
**Why it happens:** The binding is shared between read result and write/delete result.
**How to avoid:** C# handler for `readMemoryFile` writes actual file content unconditionally (even
if the previous value was `"ok"`). React disambiguates based on what was just triggered (see D-18).
Reset `memoryOpResult` to `""` in the C# handler at the start of each trigger (before the operation).
**Warning signs:** File viewer shows `"ok"` as content.

### Pitfall 3: `ListFiles()` includes files from subdirectories
**What goes wrong:** `chat-history/*.md` and `archive/*.md` appear in the list.
**Why it happens:** Developer changes `GetFiles` to use `SearchOption.AllDirectories`.
**How to avoid:** Keep `Directory.GetFiles(m_CityDir, "*.md")` — no search option argument means
`SearchOption.TopDirectoryOnly` by default. D-08 is already satisfied by the current implementation.

### Pitfall 4: Lock emoji 🔒 renders as broken box in Coherent GT
**What goes wrong:** The 🔒 emoji shows as a blank box or missing character because Coherent GT
ships without an emoji font.
**Why it happens:** CS2's Coherent GT lacks native emoji font support — established in CLAUDE.md.
**How to avoid:** Use Twemoji CDN background-image span (the established `ca-emoji` pattern from
`style.css` lines 355–363) or use a plain text label `[core]`. Twemoji approach:
```tsx
<span className="ca-emoji" style={{
  backgroundImage: "url('https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/svg/1f512.svg')"
}} />
```
The 🔒 SVG code point is `1f512`.
**Warning signs:** Lock icon appears as a square or is invisible.

### Pitfall 5: `<textarea>` needs explicit styling for Coherent GT
**What goes wrong:** The textarea for edit mode looks unstyled or has wrong background/color.
**Why it happens:** Coherent GT does not apply browser defaults consistently for form elements.
**How to avoid:** Apply the same explicit CSS properties as `.ca-input` (the existing textarea):
`background: rgba(20, 36, 56, 0.8)`, `color: #e0e8f0`, `border: 1px solid rgba(60,100,150,0.3)`,
`font-family: inherit`. The file-content textarea can reuse `.ca-input` class or a new variant.
The existing `<textarea>` in `CityAgentPanel.tsx` (lines 345–351) confirms textarea works in Coherent GT.
**Warning signs:** White textarea with black text (browser default colors).

### Pitfall 6: File content textarea height with flexbox
**What goes wrong:** The file content textarea does not fill the available panel height.
**Why it happens:** Flexbox children don't stretch to fill unless `flex: 1` and `height: 0` are set.
**How to avoid:** Set the content area container to `flex: 1`, `display: flex`, `flex-direction: column`.
Give the `<textarea>` `flex: 1` and `resize: none`. Avoid setting an explicit `height` in pixels.
**Warning signs:** Textarea is a small fixed height at the top of the panel.

### Pitfall 7: `writeMemoryFile` file must pre-exist
**What goes wrong:** User edits a file and Save returns `"[Error]: File 'x.md' does not exist."`.
**Why it happens:** `WriteFile()` rejects writes to non-existent files (it is not `CreateFile`).
**How to avoid:** Phase 5 only exposes Edit/Save for files that were read from `ListFiles()` —
these files already exist on disk. No path for writing a non-existent file in Phase 5 scope.
Confirm by checking: a file shown in the list always exists (it was returned from `GetFiles()`).

---

## Coherent GT Constraints (Confirmed Relevant to Phase 5)

| Feature | Status | Evidence | Phase 5 Impact |
|---------|--------|----------|----------------|
| `<textarea>` element | Works | Used in `CityAgentPanel.tsx` lines 345–351 | Edit mode textarea is safe |
| CSS Grid | NOT SUPPORTED | REQUIREMENTS.md Out of Scope table | All layout must be flexbox |
| `::placeholder` pseudo-selector | NOT SUPPORTED | `style.css` header comment line 6; use `[placeholder]` or omit | Don't style textarea placeholder |
| `:disabled` pseudo-selector | NOT SUPPORTED | `style.css` header comment line 6 | Use `[disabled]` attribute selector |
| Emoji font | NOT SUPPORTED | CLAUDE.md + `style.css` `.ca-emoji` pattern | Use Twemoji CDN for 🔒 or use text label |
| `-webkit-scrollbar` | Works | `style.css` lines 152–168 | Use for file list and content scroll areas |
| `Array.at()` | NOT SUPPORTED | CLAUDE.md Coherent GT note | Use `arr[arr.length - 1]` instead |
| `gap` CSS property | NOT SUPPORTED | `style.css` header comment | Use `margin-right`/`margin-bottom` for spacing |
| Unicode property escapes in regex | NOT SUPPORTED | CLAUDE.md Coherent GT note | Don't use `\p{L}` etc. |
| `const`/`let` in utilities | Avoid in utility functions | `renderMarkdown.ts` uses `var` intentionally | Use `var` in new utility functions like `relativeTime()` |

---

## Existing CSS Classes Reusable in Phase 5

| Class | Source Location | Phase 5 Use |
|-------|----------------|-------------|
| `.ca-panel__header` | style.css:95 | Unchanged — Phase 5 modifies its children, not the element |
| `.ca-panel__header-actions` | style.css:116 | Unchanged — still holds New Chat + close buttons |
| `.ca-btn-icon` | style.css:121 | Tab buttons, sub-header action buttons (Edit, Delete, Save, Cancel, back arrow) |
| `.ca-btn-icon:hover` | style.css:133 | Free hover states for all new buttons |
| `.ca-messages` | style.css:139 | File list scroll area — same scroll + flex-column container pattern |
| `.ca-input` | style.css:274 | Edit-mode textarea — can reuse directly or subclass as `.ca-memory-editor` |
| `.ca-input:focus` | style.css:289 | Focus ring for editor textarea |
| `.ca-input-area` | style.css:232 | Pattern reference for sub-header background/border treatment |
| `.ca-screenshot-chip` | style.css:241 | Pattern reference for inline notices / delete confirmation chip |
| `.ca-bubble--assistant` | style.css:191 | Error notice styling candidate (or use system pill from Phase 2) |
| `.ca-markdown` | style.css:366 | Can wrap file content in `<pre>` — don't apply markdown rendering by default |
| `.ca-emoji` | style.css:354 | Lock icon via Twemoji background-image |
| `[disabled]` selector pattern | style.css:314,336 | Disabled state for buttons |

**New CSS classes Phase 5 must add:**
- `.ca-tab-bar` — flex row for tab buttons; sits where `.ca-panel__header-title` was
- `.ca-tab` — individual tab button (variant of `.ca-btn-icon` but with active state)
- `.ca-tab--active` — active tab highlight (accent color border-bottom or background)
- `.ca-memory-subheader` — sub-header bar below tabs in file/edit view
- `.ca-memory-file-list` — scrollable list container
- `.ca-memory-file-row` — single file row (filename, size, modified time, lock icon)
- `.ca-memory-file-meta` — secondary text (size, relative time) in a file row
- `.ca-memory-content` — file content display area (`<pre>` view)
- `.ca-memory-editor` — textarea variant for edit mode (may reuse `.ca-input`)
- `.ca-memory-error` — inline error text in sub-header (red, small)

---

## State Machine for Memory Tab (React-side)

The Memory tab has three sub-states. All state is in React `useState` only.

```
[file-list]
  ← initial state when Memory tab opens
  → user clicks a file → [file-view]

[file-view]
  ← after readMemoryFile trigger fires (memoryOpResult holds content)
  → user clicks ← back → [file-list]
  → user clicks Edit → [edit-mode]
  → user clicks Delete → [confirm-delete] (inline in sub-header)
  → user confirms delete on success → fire refreshMemoryFiles → [file-list]

[edit-mode]
  ← after user clicks Edit
  → user clicks Cancel → [file-view] (restore original content)
  → user clicks Save → writeMemoryFile trigger:
      - on "ok" → [file-view] (show saved content)
      - on "[Error]: ..." → stay in [edit-mode], show error
```

Recommended state fields inside `CityAgentInner`:
```typescript
type MemoryView = 'list' | 'view' | 'edit';
const [memView,        setMemView]        = useState<MemoryView>('list');
const [selectedFile,   setSelectedFile]   = useState<string>('');
const [editContent,    setEditContent]    = useState<string>('');
const [memWriteError,  setMemWriteError]  = useState<string | null>(null);
const [confirmDelete,  setConfirmDelete]  = useState<boolean>(false);
```

When `activeTab` changes to `'memory'`, reset all of these to initial values (or manage in a
single `useEffect` watching `activeTab` per D-04).

---

## Validation Architecture

`workflow.nyquist_validation` is `true` in `.planning/config.json`.

### Test Framework

| Property | Value |
|----------|-------|
| Automated test framework | None — no test runner, no test files, no jest/vitest config in `UI/package.json` |
| C# unit test project | None — no `*.Test.csproj` or `xunit`/`nunit` in `src/` |
| Quick run command | `cd UI && npm run build` (TypeScript type-check via ts-loader; compile errors fail the build) |
| Full suite command | `cd src && dotnet build -c Release` (C# compile) + `cd UI && npm run build` (TS compile) |

No automated test infrastructure exists in this codebase. This is consistent with CS2 mod
development, where the game runtime is required to exercise bindings — unit testing C#↔Coherent GT
binding behavior outside the game is not practical without a simulator that does not exist.

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Command | File Exists? |
|--------|----------|-----------|---------|-------------|
| MEM-01 | File list renders in Memory tab | Manual in-game | Open Memory tab, verify file rows appear | N/A |
| MEM-01 | `ListFiles()` returns `last_modified_unix` | Build verification | `dotnet build -c Release` (compiles new field) | ✅ exists |
| MEM-02 | File click shows content | Manual in-game | Click `_index.md`, verify content appears | N/A |
| MEM-02 | `ReadFile()` returns content string | Build verification | `dotnet build -c Release` | ✅ exists |
| MEM-03 | Edit and save persists to disk | Manual in-game | Edit `lore.md`, save, send a message, verify Claude reads new content | N/A |
| MEM-03 | `writeMemoryFile` trigger wired correctly | Build verification | `dotnet build -c Release` (type-check TriggerBinding<string,string>) | ✅ exists |
| MEM-04 | Core file Delete button is hidden | Manual in-game | Click `_index.md`, verify no Delete button | N/A |
| MEM-04 | Attempt to delete core file via trigger | Manual in-game (or direct) | Fire deleteMemoryFile("_index.md"), verify "[Error]" response | N/A |

### Sampling Rate
- **Per task commit:** `cd UI && npm run build` — catches TypeScript errors before commit
- **Per wave merge:** `cd src && dotnet build -c Release` — C# compile; `cd UI && npm run build` — full TS/CSS bundle
- **Phase gate:** Both builds green + all 4 manual in-game tests pass before `/gsd:verify-work`

### Wave 0 Gaps
None — existing build infrastructure (dotnet + webpack) covers all Phase 5 validation requirements.
No test framework setup needed.

---

## Environment Availability

Phase 5 has no external service dependencies. All file I/O is local disk.

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | `dotnet build` | ✓ (CS2 installed, `.csproj` compiles) | unverified exact version | — |
| Node.js | `npm run build` | ✓ | v24.13.0 (from MEMORY.md) | — |
| CS2 game | In-game manual validation | ✓ (Steam install confirmed) | — | — |
| Colossal.UI.Binding.dll | `TriggerBinding<T1,T2>` | ✓ (confirmed in DLL inspection) | game-bundled | — |

**No missing dependencies.**

---

## State of the Art

No library upgrades or pattern changes required for Phase 5. All patterns are established by
prior phases.

| Area | Current Approach | Phase 5 Change |
|------|-----------------|----------------|
| Binding pattern | `ValueBinding<string>` + `AddBinding()` | Same — add 2 more |
| Trigger pattern | `TriggerBinding<string>` | Same + add `TriggerBinding<string,string>` |
| React state | `useState` in `CityAgentInner` | Same — add tab + memory sub-state |
| CSS | `ca-` BEM in `style.css` | Same — add new selectors |
| File I/O | `NarrativeMemorySystem` methods | Same — extend `ListFiles()` output only |

---

## Open Questions

1. **`size_kb` vs `size_bytes` field name**
   - What we know: CONTEXT.md binding contract says `size_kb`; current `ListFiles()` uses `size_bytes` (raw long)
   - What's unclear: Should C# compute `size_kb` as a float, or should React format it?
   - Recommendation: Compute in C# as `Math.Round(info.Length / 1024.0, 1)` and use field name `size_kb` (double) to match the binding contract. React displays as `"4.2KB"`.

2. **Phase 1 async refactor impact on `WriteFile` / `DeleteFile`**
   - What we know: Phase 1 CONTEXT D-11 says full async refactor of NarrativeMemorySystem — `WriteFile`, `AppendToLog`, `SaveChatSession` become async
   - What's unclear: If Phase 5 executes after Phase 1, trigger handlers calling async methods need `async void` or continuation pattern
   - Recommendation: Planner must note that if Phase 1 async refactor is complete, `OnWriteMemoryFile` and `OnDeleteMemoryFile` may need `async void` handlers. For small files, synchronous version is acceptable if Phase 1 scope is narrowed to leave `WriteFile`/`DeleteFile` synchronous.

3. **`memoryOpResult` reset timing**
   - What we know: CONTEXT.md says "reset on next trigger fire is sufficient" (Claude's discretion)
   - What's unclear: Should C# reset to `""` at the start of each trigger handler, or after React consumes it?
   - Recommendation: Reset `m_MemoryOpResult.Update("")` at the start of `OnReadMemoryFile` before writing the new value. This prevents stale values from being misread if a previous write/delete left `"ok"` in the binding.

---

## Sources

### Primary (HIGH confidence)
- Direct code read: `src/Systems/NarrativeMemorySystem.cs` — full public API (ListFiles, ReadFile, WriteFile, DeleteFile signatures, return values, error strings, CoreFiles set)
- Direct code read: `src/Systems/CityAgentUISystem.cs` — all existing ValueBinding/TriggerBinding patterns
- Direct code read: `UI/src/components/CityAgentPanel.tsx` — full panel structure, header layout, content area, drag pattern
- Direct code read: `UI/src/style.css` — all existing `ca-` classes and Coherent GT constraints
- Direct code read: `UI/types/cs2-api.d.ts` — `trigger(...args: unknown[])` signature confirms multi-arg support
- DLL reflection: `Colossal.UI.Binding.dll` — confirmed `TriggerBinding<T1,T2>` exists with exact constructor signature

### Secondary (MEDIUM confidence)
- `.planning/phases/05-memory-file-explorer/05-CONTEXT.md` — user decisions (all locked)
- `.planning/phases/02-chat-ui-polish/02-CONTEXT.md` — CSS conventions, center-pill pattern
- `.planning/phases/01-api-migration-core-stability/01-CONTEXT.md` — thread model, async decisions

### Tertiary (LOW confidence — unverified)
- CLAUDE.md notes on Coherent GT missing features (`Array.at()`, Unicode regex, `gap`, `::placeholder`) — stated as project knowledge; not independently verified against Coherent GT changelog

---

## Project Constraints (from CLAUDE.md)

- **Tech stack**: C# .NET Standard 2.1 for mod layer; React/TypeScript for UI. No deviation.
- **Game thread**: All HTTP calls async/non-blocking. File I/O for Phase 5 (small text reads/writes) is synchronous on the UI thread — acceptable given file sizes.
- **CS2 binding limit**: State crosses C#↔JS bridge as JSON strings via `ValueBinding`. `memoryFilesJson` and `memoryOpResult` follow this.
- **API key security**: Not applicable to Phase 5.
- **ECS access**: Not applicable to Phase 5 (no new city data reads).
- **Distribution**: Not applicable to Phase 5.
- **C# thin**: Phase 5 C# additions are bridge-only — no business logic beyond calling `NarrativeMemorySystem` methods.
- **CS2 is ECS, not OOP**: Not applicable to Phase 5.
- **UI is React/JS, not C#**: All layout, state machines, and display logic are in React. C# only exposes raw data and write results.
- **Close CS2 before building**: Applies to all C# build tasks in this phase.
- **Never hardcode API key**: Not applicable to Phase 5.
- **CSS Grid forbidden in Coherent GT**: All layout must be flexbox.
- **No `gap` CSS property**: Use margins instead.
- **No `::placeholder` or `:disabled`**: Use `[disabled]` attribute selector.
- **Emoji requires Twemoji CDN**: Lock icon must use `ca-emoji` background-image pattern or text fallback.
- **`var` in utility functions**: `relativeTime()` and any other new pure utility functions should use `var` (not `const`/`let`) for Coherent GT safety, per `renderMarkdown.ts` convention.
- **GSD workflow**: All file changes go through a GSD command (`/gsd:execute-phase`), not direct edits.

---

## Metadata

**Confidence breakdown:**
- NarrativeMemorySystem API signatures: HIGH — read directly from source code
- TriggerBinding<T1,T2> availability: HIGH — confirmed via DLL reflection
- trigger() multi-arg JS support: HIGH — confirmed in `cs2-api.d.ts`
- Thread safety (sync handlers): HIGH — matches existing trigger handler pattern
- Coherent GT constraints: MEDIUM — sourced from CLAUDE.md project knowledge; not independently verified
- CSS reuse assessment: HIGH — read directly from `style.css`

**Research date:** 2026-03-28
**Valid until:** 2026-05-28 (stable — game DLLs and project source don't change)
