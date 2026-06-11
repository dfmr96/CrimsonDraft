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

**Windows (PowerShell, run once):**
```powershell
[System.Environment]::SetEnvironmentVariable("HNP_API_KEY", "your_hnp_api_key", "User")
[System.Environment]::SetEnvironmentVariable("HNP_PROJECT_ID", "the_shared_project_id", "User")
```

Or via GUI: System Properties → Advanced → Environment Variables → User variables → New.

Restart Claude Code after setting them so they are picked up.

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
