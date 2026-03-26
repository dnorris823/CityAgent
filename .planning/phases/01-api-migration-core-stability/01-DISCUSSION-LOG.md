# Phase 1: API Migration & Core Stability - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-03-26
**Phase:** 01-api-migration-core-stability
**Areas discussed:** Provider settings structure, Rate-limit fallback UX, Ollama fallback optionality, File I/O threading scope

---

## Provider Settings Structure

| Option | Description | Selected |
|--------|-------------|----------|
| Two separate sections | "Claude API" section + "Ollama Fallback (optional)" section | ✓ |
| Provider dropdown + unified fields | Single provider dropdown relabels same fields | |
| Keep Ollama fields + add Claude fields | Additive, keeps existing fields | |

**User's choice:** Two separate sections

| Sub-question | Options | Selected |
|---|---|---|
| Default Claude model | claude-sonnet-4-6 / claude-haiku-4-5 / blank | claude-sonnet-4-6 |
| Active provider indicator | No indicator / Show label in settings | Show label in settings |
| Settings field rename strategy | Clean break / Keep with [Obsolete] | Clean break |
| Default Ollama base URL | http://localhost:11434 / Leave blank | http://localhost:11434 |

---

## Rate-Limit Fallback UX

| Option | Description | Selected |
|--------|-------------|----------|
| In-panel notice | System message "⚠️ Rate limited — retrying with [model]..." | ✓ |
| Silent auto-switch | No notification, Ollama responds normally | |
| Error message | Surface error, no auto-retry | |

**User's choice:** In-panel notice with model name included

| Sub-question | Options | Selected |
|---|---|---|
| Non-429 error behavior | Error only, no fallback / Always fallback on any error | Error only — no fallback |
| Show Ollama model name in notice | No — keep simple / Yes — include model name | Yes — include model name |
| Response styling for Ollama response | Same as normal / Add footer/indicator | Same — no indicator on response |

---

## Ollama Fallback Optionality

| Option | Description | Selected |
|--------|-------------|----------|
| Show clear error in chat | "⚠️ Rate limited by Claude. No Ollama fallback configured..." | ✓ |
| Silent failure | Generic [Error]: ... | |
| Error + disable fallback for session | Show once, skip fallback future retries | |

**User's choice:** Clear error message directing user to configure Ollama in settings

| Sub-question | Options | Selected |
|---|---|---|
| Ollama section label | Yes — mark "(optional)" / No — just leave blank | Yes — label "(optional)" |
| Ollama connectivity validation | Out of scope for Phase 1 / Validate on settings save | Out of scope |
| Tool format per provider | Auto-select per provider / Always OpenAI / Always Anthropic | Auto-select per provider |

---

## File I/O Threading Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Targeted fix at call sites | Wrap specific calls in Task.Run | |
| Full async refactor of NarrativeMemorySystem | Convert all I/O methods to async throughout | ✓ |
| Queue-based background writer | Dedicated background thread with write queue | |

**User's choice:** Full async refactor of NarrativeMemorySystem

| Sub-question | Options | Selected |
|---|---|---|
| Screenshot base64 encoding | Move to background thread / Defer | Move to background thread |
| Async call pattern | Fire-and-forget / Await completion | Fire-and-forget |
| Write failure behavior | Log error, continue silently / Show in chat | Log error, continue silently |
| PendingResult + m_RequestInFlight fix | Fix both / PendingResult only | Fix both |

---

## Claude's Discretion

- Exact async method signatures and Task patterns in NarrativeMemorySystem
- How the "active provider" label reads current state
- Whether to introduce a Provider enum or string-based routing
- System notice message role type for in-panel notices

## Deferred Ideas

None.
