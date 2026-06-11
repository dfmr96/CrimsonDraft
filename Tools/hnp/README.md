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

---

## HNP API reference

Base URL: `https://api.hacknplan.com/v0`  
Auth header: `Authorization: ApiKey YOUR_KEY`  
Swagger spec: `https://api.hacknplan.com/swagger/docs/v0`

### Crear workitem

`POST /projects/{projectId}/workitems`

Campos **requeridos** (sin estos devuelve 400):

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `title` | string | Nombre de la tarea |
| `isStory` | boolean | `false` para tareas normales |
| `estimatedCost` | float | Estimación en horas (`0` si no aplica) |
| `importanceLevelId` | int | 1=Urgent, 2=High, 3=Normal, 4=Low |

Campos opcionales útiles: `categoryId`, `boardId`, `stageId`.

```bash
curl -X POST -H "Authorization: ApiKey $HNP_API_KEY" -H "Content-Type: application/json" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems" \
  -d '{"title":"Nombre tarea","isStory":false,"estimatedCost":0,"importanceLevelId":3,"categoryId":1,"boardId":676214}'
```

### Agregar comentario a una tarea

`POST /projects/{projectId}/workitems/{workItemId}/comments`

El body es un **string JSON literal** (no un objeto):

```bash
curl -X POST -H "Authorization: ApiKey $HNP_API_KEY" -H "Content-Type: application/json" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/{workItemId}/comments" \
  -d '"Texto del comentario con \n saltos de línea"'
```

### Cambiar stage de una tarea

`PATCH /projects/{projectId}/workitems/{workItemId}` — actualización parcial, solo los campos que cambian:

```bash
curl -X PATCH -H "Authorization: ApiKey $HNP_API_KEY" -H "Content-Type: application/json" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/{workItemId}" \
  -d '{"stageId": 2}'
```

Stages por defecto: 1=Planned, 2=In progress, 3=Testing, 4=Completed.  
Para obtener los stages del proyecto: `GET /projects/{projectId}/stages`

### Obtener tareas de un board (sprint)

```bash
# Listar boards
curl -H "Authorization: ApiKey $HNP_API_KEY" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/boards"

# Tareas del board
curl -H "Authorization: ApiKey $HNP_API_KEY" \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems?boardId={boardId}"
```

Las respuestas listan-paginadas usan el wrapper `{"totalCount":N,"items":[...]}`.
