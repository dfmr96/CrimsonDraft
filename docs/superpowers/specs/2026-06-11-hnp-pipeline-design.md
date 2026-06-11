# HNP Pipeline Design — Task Tracking + Time Tracking + Git

**Date:** 2026-06-11
**Status:** Approved

## Revision (2026-06-11)

**Architecture changed from Node.js CLI to Claude Code slash commands.** The Toggl MCP (`@verygoodplugins/mcp-toggl`) replaces the custom `toggl-client.js`. HNP API calls are made via `curl` in the Bash tool. The CLI plan (`2026-06-11-hnp-cli-pipeline.md`) is superseded by `2026-06-11-hnp-mcp-pipeline.md`.

---

## Overview

A Node.js CLI tool (`hnp`) that connects Hack N Plan (task management), Toggl Track (time tracking), and Git (commit log) into a single workflow. The tool is installed from the repo by each team member, with machine-specific credentials stored outside the repo.

## Goals

- Start a work session by selecting a task → automatically start a Toggl timer
- End a session → stop the timer, attach git commits as a description, optionally update the HNP task status
- Zero friction: no manual copy-pasting between tools, no changes to the git commit workflow

## Non-Goals

- Automatically creating or changing HNP tasks based on git events
- CI/CD integration
- Time reporting or analytics

---

## Architecture

### Where the code lives

```
tools/hnp-cli/             ← inside the game repo, shared via git
├── package.json
├── README.md              ← setup instructions (4 steps)
├── bin/
│   └── hnp.js             ← entry point, registered as global command
└── src/
    ├── commands/
    │   ├── setup.js
    │   ├── start.js
    │   ├── stop.js
    │   └── status.js
    ├── hnp-client.js      ← Hack N Plan REST API wrapper
    ├── toggl-client.js    ← Toggl Track API wrapper
    ├── state.js           ← read/write ~/.hnp/state.json
    └── git.js             ← runs git log and parses commits
```

### Machine-specific files (never in git)

```
~/.hnp/
├── config.json            ← API keys and project ID
└── state.json             ← active session (exists only during a session)
```

**config.json schema:**
```json
{
  "hnpApiKey": "string",
  "togglApiKey": "string",
  "hnpProjectId": "string"
}
```

**state.json schema:**
```json
{
  "taskId": "string",
  "taskName": "string",
  "startedAt": "ISO 8601 UTC timestamp",
  "togglEntryId": "string"
}
```

---

## Commands

### `hnp setup`

Interactive first-time configuration. Prompts for HNP API key, Toggl API key, and HNP project ID. Writes `~/.hnp/config.json`. Safe to re-run to update credentials.

### `hnp start`

1. Reads `~/.hnp/config.json` — errors if missing, directs user to run `hnp setup`
2. Checks for an existing `state.json` — if found, asks: *"Ya hay una sesión activa con [tarea]. ¿Cerrarla primero?"*
3. Fetches tasks from the active sprint via HNP API
4. Renders interactive list (arrow keys + Enter) using `clack`
5. Calls Toggl API to start a timer with the task name as description
6. Writes `~/.hnp/state.json` with task ID, name, start timestamp, and Toggl entry ID

### `hnp stop`

1. Reads `~/.hnp/state.json` — errors if missing ("No hay sesión activa")
2. Calls Toggl API to stop the active timer
3. Runs `git log --after="<startedAt>" --format="%s"` to collect commit subjects
4. Posts a comment to the HNP task with the commit list (or "Sin commits en esta sesión" if none)
5. Prompts: *"¿Cambiar estado de la tarea?"* — shows available HNP statuses, user selects or skips
6. If a status is selected, calls HNP API to update the task
7. Deletes `~/.hnp/state.json`

### `hnp status`

Reads `~/.hnp/state.json` and prints the active task name and elapsed time. Errors if no session is active.

---

## Data Flow

```
hnp start
  └─ HNP API: GET /sprints/{active}/tasks
  └─ clack: interactive list
  └─ Toggl API: POST /time_entries (start)
  └─ write ~/.hnp/state.json

[developer works, commits normally — no hooks]

hnp stop
  └─ Toggl API: PATCH /time_entries/{id} (stop)
  └─ git log --after="<startedAt>"
  └─ HNP API: POST /tasks/{id}/comments
  └─ clack: status selection prompt
  └─ HNP API: PATCH /tasks/{id} (status) [optional]
  └─ delete ~/.hnp/state.json
```

---

## Error Handling

| Situation | Behavior |
|-----------|----------|
| `hnp start` with no config | Error: "Corré `hnp setup` primero" |
| `hnp start` with active session | Prompt to close existing session first |
| `hnp stop` with no active session | Error: "No hay sesión activa" |
| `hnp stop` outside a git repo | Warns, continues — posts "Sin commits en esta sesión" to HNP |
| No commits during session | Posts "Sin commits en esta sesión" as HNP comment |
| HNP or Toggl API failure | Shows error, **does not delete state.json** — user can retry with `hnp stop` |

**Principle:** never lose data silently. If an API call fails, the session state is preserved so the user can retry.

---

## Team Setup (per machine)

Each team member runs these steps once:

```sh
git pull
cd tools/hnp-cli
npm install
npm link          # registers "hnp" as a global command

hnp setup         # enters personal API keys → writes ~/.hnp/config.json
```

The `README.md` inside `tools/hnp-cli/` documents these four steps.

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `clack` | Interactive terminal UI (lists, prompts) |
| `node-fetch` or built-in `fetch` | HTTP calls to HNP and Toggl APIs |

No framework, no database, no daemon. The tool is a thin script over two REST APIs and `git log`.
