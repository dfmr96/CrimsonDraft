# HNP MCP Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create three Claude Code slash commands (`/hnp-start`, `/hnp-stop`, `/hnp-status`) that connect Hack N Plan, Toggl Track, and Git — no separate CLI required.

**Architecture:** Project-level slash commands live in `.claude/commands/` and are shared via git. Each command is a markdown file that instructs Claude to orchestrate the workflow using: the Toggl MCP (`toggl_start_timer`, `toggl_stop_timer`, `toggl_get_current_entry`), HNP REST API v0 via `curl` in Bash, and `git log` for commit collection. Session state persists in `~/.hnp/state.json` so timers survive between Claude sessions.

**Tech Stack:** Claude Code slash commands, Toggl MCP (`@verygoodplugins/mcp-toggl`), Hack N Plan REST API v0, Bash (`curl`, `git`).

---

## File Map

| File | Responsibility |
|------|---------------|
| `.claude/commands/hnp-start.md` | `/hnp-start` — select task, start Toggl timer, write state |
| `.claude/commands/hnp-stop.md` | `/hnp-stop` — stop timer, post commits to HNP, update status |
| `.claude/commands/hnp-status.md` | `/hnp-status` — show active task and elapsed time |
| `tools/hnp/README.md` | Team setup instructions (MCP config + env vars) |

**Machine-specific (never in git):**

| Location | Contents |
|----------|----------|
| `~/.hnp/state.json` | Active session: taskId, taskName, startedAt, togglEntryId |
| `TOGGL_API_KEY` env var | Personal Toggl API token |
| `TOGGL_DEFAULT_WORKSPACE_ID` env var | Toggl workspace ID (auto-detect via `toggl_list_workspaces`) |
| `HNP_API_KEY` env var | Personal HNP API key |
| `HNP_PROJECT_ID` env var | Shared project ID (same for all team members) |

---

## Task 1: Toggl MCP Setup (per machine — document only)

**Files:**
- Create: `tools/hnp/README.md` (setup instructions)

This task produces the documentation team members follow to configure the Toggl MCP. No code changes to the repo beyond the README.

- [ ] **Step 1: Verify Toggl MCP works in Claude Code**

In Claude Code, ask:
```
Use toggl_check_auth to verify my Toggl connection.
```

If it fails, add the MCP to Claude Code's config. Location on Windows:
`%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "mcp-toggl": {
      "command": "npx",
      "args": ["-y", "@verygoodplugins/mcp-toggl@latest"],
      "env": {
        "TOGGL_API_KEY": "your_api_key_here",
        "TOGGL_DEFAULT_WORKSPACE_ID": "your_workspace_id"
      }
    }
  }
}
```

To find your workspace ID, ask Claude:
```
Use toggl_list_workspaces to show my workspaces.
```

- [ ] **Step 2: Set HNP environment variables**

Add to your system environment (Windows → System Properties → Environment Variables):

```
HNP_API_KEY=your_hnp_api_key
HNP_PROJECT_ID=your_project_id
```

To find your HNP API key: Hack N Plan → Settings → API.
To find your Project ID: it appears in the URL of your project (`/projects/{ID}/...`).

Restart Claude Code after setting env vars so they are picked up.

- [ ] **Step 3: Verify HNP env vars are accessible**

In Claude Code, ask:
```
Run in Bash: echo $HNP_API_KEY
```

Expected: prints your HNP API key (not empty).

- [ ] **Step 4: Create `tools/hnp/README.md`**

```markdown
# HNP Workflow Commands

Three Claude Code slash commands that connect Hack N Plan, Toggl Track, and Git.

## Setup (once per machine)

### 1. Configure the Toggl MCP

Add to `%APPDATA%\Claude\claude_desktop_config.json` (Windows) or
`~/Library/Application Support/Claude/claude_desktop_config.json` (Mac):

```json
{
  "mcpServers": {
    "mcp-toggl": {
      "command": "npx",
      "args": ["-y", "@verygoodplugins/mcp-toggl@latest"],
      "env": {
        "TOGGL_API_KEY": "your_toggl_api_key",
        "TOGGL_DEFAULT_WORKSPACE_ID": "your_workspace_id"
      }
    }
  }
}
```

Find your **Toggl API key**: toggl.com → Profile Settings → API Token.
Find your **workspace ID**: ask Claude `use toggl_list_workspaces`.

### 2. Set HNP environment variables

Add to your system environment variables and restart Claude Code:

```
HNP_API_KEY=your_hnp_api_key
HNP_PROJECT_ID=the_shared_project_id
```

Find your **HNP API key**: Hack N Plan → Settings → API.
The **Project ID** is in the URL of the project (`/projects/{ID}/...`) — ask a teammate.

### 3. Pull the latest commands

```
git pull
```

The `.claude/commands/` directory contains the slash commands — Claude Code picks them up automatically.

## Usage

```
/hnp-start    Select a sprint task and start the Toggl timer
/hnp-stop     Stop the timer, post commits to HNP, optionally update task status
/hnp-status   Show the active task and elapsed time
```

## How it works

- Active session is stored in `~/.hnp/state.json` (never goes into git)
- Commits are collected via `git log --after="<start time>"` from your working directory
- Time is tracked in Toggl with the task name as the description
```

- [ ] **Step 5: Commit README**

```bash
git add tools/hnp/README.md
git commit -m "docs(tools): add HNP workflow setup instructions"
```

---

## Task 2: `/hnp-status` Command

**Files:**
- Create: `.claude/commands/hnp-status.md`

Start with `status` since it's the simplest — it only reads state and has no side effects. Good for verifying the state file format works before building start/stop.

- [ ] **Step 1: Create `.claude/commands/hnp-status.md`**

```markdown
Check the status of the current HNP work session.

Follow these steps exactly:

1. Read the active session state:
   Run in Bash: `cat "$USERPROFILE/.hnp/state.json" 2>/dev/null || cat "$HOME/.hnp/state.json" 2>/dev/null`

2. If the file is empty or does not exist:
   Tell the user: "No hay sesión activa."
   Stop here.

3. If the file exists, parse the JSON and show the user:
   - Task name
   - Time elapsed since `startedAt` (calculate from current time)
   Format: "Sesión activa: [taskName] — [Xh Ym]"

4. Also use toggl_get_current_entry to confirm the Toggl timer is still running.
   If no timer is running in Toggl, warn the user: "El timer de Toggl no está corriendo. Puede que se haya detenido manualmente."
```

- [ ] **Step 2: Test the command with no active session**

In Claude Code, run:
```
/hnp-status
```

Expected: "No hay sesión activa."

- [ ] **Step 3: Test with a manually created state file**

Run in Bash:
```bash
mkdir -p "$USERPROFILE/.hnp"
echo '{"taskId":"test-1","taskName":"Test Task","startedAt":"'"$(date -u +%Y-%m-%dT%H:%M:%SZ)"'","togglEntryId":"0"}' \
  > "$USERPROFILE/.hnp/state.json"
```

Then run `/hnp-status`. Expected: shows task name and elapsed time (~0m).

Clean up:
```bash
rm "$USERPROFILE/.hnp/state.json"
```

- [ ] **Step 4: Commit**

```bash
git add .claude/commands/hnp-status.md
git commit -m "feat(tools): add /hnp-status slash command"
```

---

## Task 3: `/hnp-start` Command

**Files:**
- Create: `.claude/commands/hnp-start.md`

- [ ] **Step 1: Create `.claude/commands/hnp-start.md`**

````markdown
Start a new HNP work session. Follow these steps in order, stopping if any step fails.

**Step 1 — Check for existing session**

Run in Bash:
```
cat "$USERPROFILE/.hnp/state.json" 2>/dev/null || cat "$HOME/.hnp/state.json" 2>/dev/null
```

If a session exists, ask the user:
"Ya hay una sesión activa: '[taskName]'. ¿La cerramos antes de empezar? (s/n)"

- If yes: run /hnp-stop first, then continue with Step 2.
- If no: stop here.

**Step 2 — Fetch sprint tasks from HNP**

Run in Bash (replace variables with their actual env var values):
```bash
curl -s -H "Authorization: ApiKey $HNP_API_KEY" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/milestones"
```

Find the milestone where `isActive` is true. Save its `id` as MILESTONE_ID.

If no active milestone is found, fetch all tasks without a milestone filter.

Then fetch tasks:
```bash
curl -s -H "Authorization: ApiKey $HNP_API_KEY" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems?milestoneId=MILESTONE_ID"
```

If the env vars are empty, tell the user:
"Faltan variables de entorno. Seguí las instrucciones en tools/hnp/README.md."
Stop here.

**Step 3 — Present task list and get user selection**

Show the list of tasks with their names and IDs. Ask the user to select one.
Wait for the user to respond with a task name or number.

**Step 4 — Start Toggl timer**

Use `toggl_start_timer` with:
- description: the selected task name
- No project or tags needed unless the user specifies

Save the returned time entry ID.

**Step 5 — Save session state**

Run in Bash:
```bash
mkdir -p "$USERPROFILE/.hnp" 2>/dev/null || mkdir -p "$HOME/.hnp"
```

Then write the state file. Use the actual values (not placeholders):
```bash
cat > "$USERPROFILE/.hnp/state.json" << 'EOF'
{
  "taskId": "ACTUAL_TASK_ID",
  "taskName": "ACTUAL_TASK_NAME",
  "startedAt": "CURRENT_ISO_TIMESTAMP",
  "togglEntryId": "ACTUAL_TOGGL_ENTRY_ID"
}
EOF
```

Use PowerShell if the heredoc syntax fails on Windows:
```powershell
$state = @{ taskId="ACTUAL_ID"; taskName="ACTUAL_NAME"; startedAt="TIMESTAMP"; togglEntryId="TOGGL_ID" }
$state | ConvertTo-Json | Out-File "$env:USERPROFILE\.hnp\state.json" -Encoding utf8
```

**Step 6 — Confirm to the user**

"Sesión iniciada: [taskName]. El timer de Toggl está corriendo."
````

- [ ] **Step 2: Test `/hnp-start` end-to-end**

Run `/hnp-start` in Claude Code. Go through the full flow:
1. Task list appears
2. Select a task
3. Toggl timer starts (verify in Toggl web UI)
4. Run `/hnp-status` — should show the active task

- [ ] **Step 3: Test with existing active session**

Run `/hnp-start` while a session is already active. Expected: asks to close the existing session first.

- [ ] **Step 4: Commit**

```bash
git add .claude/commands/hnp-start.md
git commit -m "feat(tools): add /hnp-start slash command"
```

---

## Task 4: `/hnp-stop` Command

**Files:**
- Create: `.claude/commands/hnp-stop.md`

- [ ] **Step 1: Create `.claude/commands/hnp-stop.md`**

````markdown
Stop the current HNP work session. Follow these steps in order.

**Step 1 — Read active session**

Run in Bash:
```
cat "$USERPROFILE/.hnp/state.json" 2>/dev/null || cat "$HOME/.hnp/state.json" 2>/dev/null
```

If the file is empty or does not exist:
Tell the user "No hay sesión activa." and stop here.

Parse the JSON: save taskId, taskName, startedAt, togglEntryId.

**Step 2 — Stop Toggl timer**

Use `toggl_stop_timer` to stop the current running timer.

If it fails (timer already stopped), continue anyway — do not abort the rest of the steps.

**Step 3 — Collect git commits**

Run in Bash from the current directory:
```bash
git log --after="STARTED_AT_VALUE" --format="%s" --no-merges 2>/dev/null
```

Replace STARTED_AT_VALUE with the actual `startedAt` timestamp from the state file.

If the output is empty or the command fails (not a git repo):
Use the text "_Sin commits en esta sesión._" as the commit list.

Otherwise, format each line as "- commit message".

**Step 4 — Calculate duration**

Calculate the elapsed time between `startedAt` and now. Format as "Xh Ym" (e.g. "1h 23m" or "45m").

**Step 5 — Post comment to HNP**

Build the comment text:
```
Sesión registrada desde Claude Code

Tarea: TASK_NAME
Duración: DURATION

Commits:
- commit one
- commit two
```

Post it via Bash:
```bash
curl -s -X POST \
  -H "Authorization: ApiKey $HNP_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"text": "COMMENT_TEXT_JSON_ESCAPED"}' \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/TASK_ID/comments"
```

If this fails, tell the user: "No se pudo registrar el comentario en HNP: [error]. El estado de sesión se conserva — podés reintentar con /hnp-stop."
Stop here WITHOUT deleting the state file.

**Step 6 — Prompt for status update**

Ask the user: "¿Querés cambiar el estado de la tarea en HNP? (s/n)"

If yes:
Fetch available stages:
```bash
curl -s -H "Authorization: ApiKey $HNP_API_KEY" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/stages"
```

Show the stage names and ask the user to pick one.

Once selected, update the task:
```bash
curl -s -X PUT \
  -H "Authorization: ApiKey $HNP_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"stageId": "SELECTED_STAGE_ID"}' \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/TASK_ID"
```

If this fails, warn the user but continue to Step 7.

**Step 7 — Clear session state**

Run in Bash:
```bash
rm "$USERPROFILE/.hnp/state.json" 2>/dev/null || rm "$HOME/.hnp/state.json" 2>/dev/null
```

**Step 8 — Confirm to the user**

"Sesión cerrada: [taskName] — [duration]. Tiempo registrado en Toggl y comentario agregado en HNP."
````

- [ ] **Step 2: Test `/hnp-stop` end-to-end**

Start a session with `/hnp-start`, make a test commit, then run `/hnp-stop`.

Verify:
- Toggl timer stopped (check Toggl web UI)
- HNP task has a new comment with commit and duration
- `~/.hnp/state.json` is deleted
- `/hnp-status` returns "No hay sesión activa."

- [ ] **Step 3: Test error resilience — run `/hnp-stop` with no session**

Expected: "No hay sesión activa." — no other side effects.

- [ ] **Step 4: Test retry after HNP comment failure**

Temporarily break the HNP URL in the command (edit locally, don't commit), run `/hnp-stop`, verify state file is preserved, restore the URL, run `/hnp-stop` again — should succeed.

- [ ] **Step 5: Commit**

```bash
git add .claude/commands/hnp-stop.md
git commit -m "feat(tools): add /hnp-stop slash command"
```

---

## Task 5: End-to-End Team Simulation

Verify the full workflow works before sharing with the team.

- [ ] **Step 1: Full golden-path test**

```
/hnp-start         → select a real task
                   → verify Toggl timer running in toggl.com
/hnp-status        → shows task name and elapsed time
[make a git commit]
/hnp-stop          → stops timer
                   → verify comment on HNP task with commit listed
                   → verify duration logged in Toggl
                   → change task status when prompted
/hnp-status        → "No hay sesión activa."
```

- [ ] **Step 2: Verify state file cleaned up**

```bash
cat "$USERPROFILE/.hnp/state.json"
```

Expected: file not found.

- [ ] **Step 3: Commit any fixes found during testing**

```bash
git add .claude/commands/
git commit -m "fix(tools): address issues found during end-to-end test"
```

---

## All Slash Commands Summary

| Command | What it does |
|---------|-------------|
| `/hnp-start` | Lists HNP sprint tasks → select one → starts Toggl timer → saves session state |
| `/hnp-stop` | Stops Toggl timer → posts commits + duration to HNP → optional status change → clears state |
| `/hnp-status` | Shows active task name and elapsed time, or "No hay sesión activa." |
