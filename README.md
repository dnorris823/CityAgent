# CityAgent

An AI advisor mod for Cities: Skylines 2. Pass screenshots of your city to Claude, get narrative context and build recommendations back — in-game, in real time.

Inspired by the storytelling approach of CityPlannerPlays. Powered by the Claude API.

## Status

Phase 1 — toolchain validation (in progress)

## Architecture

- **C# mod** (`src/`) — thin bridge that reads game data and calls the Claude API
- **React UI** (`UI/`) — in-game panel rendered via CS2's Coherent GT browser
- **Claude API** — `claude-sonnet-4-6` with vision and tool use

## Build

See [CLAUDE.md](./CLAUDE.md) for full setup, prerequisites, and phase breakdown.
