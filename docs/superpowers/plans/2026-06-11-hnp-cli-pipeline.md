# HNP Pipeline CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Node.js CLI tool (`hnp`) that connects Hack N Plan, Toggl Track, and Git — `hnp start` picks a sprint task and starts a timer; `hnp stop` stops the timer and posts commits as an HNP comment.

**Architecture:** Single Node.js ESM package in `tools/hnp-cli/`, installed globally on each machine via `npm link`. Session state persists in `~/.hnp/state.json` so the active timer survives terminal restarts. All API calls use Node 18's built-in `fetch` — no SDK wrappers.

**Tech Stack:** Node.js 18+, `@clack/prompts` ^0.7 for terminal UI, Hack N Plan REST API v0, Toggl Track REST API v9.

---

## File Map

| File | Responsibility |
|------|---------------|
| `tools/hnp-cli/package.json` | Package definition, bin entry, ESM config |
| `tools/hnp-cli/bin/hnp.js` | Entry point — parses `process.argv`, routes to command |
| `tools/hnp-cli/src/config.js` | Read/write `~/.hnp/config.json` and `~/.hnp/state.json` |
| `tools/hnp-cli/src/git.js` | Run `git log --after` and return commit subjects |
| `tools/hnp-cli/src/toggl-client.js` | Toggl Track API: start/stop timer, get workspace ID |
| `tools/hnp-cli/src/hnp-client.js` | HNP API: fetch tasks, fetch stages, post comment, update stage |
| `tools/hnp-cli/src/commands/setup.js` | `hnp setup` — guided config wizard |
| `tools/hnp-cli/src/commands/start.js` | `hnp start` — select task, start timer, write state |
| `tools/hnp-cli/src/commands/stop.js` | `hnp stop` — stop timer, post commits, update status |
| `tools/hnp-cli/src/commands/status.js` | `hnp status` — show active task and elapsed time |
| `tools/hnp-cli/tests/config.test.js` | Unit tests for config read/write/state |
| `tools/hnp-cli/tests/git.test.js` | Unit tests for commit parsing |
| `tools/hnp-cli/README.md` | 4-step team setup instructions |

---

## Task 1: Project Scaffold

**Files:**
- Create: `tools/hnp-cli/package.json`
- Create: `tools/hnp-cli/bin/hnp.js` (stub)
- Create directories: `src/commands/`, `tests/`

- [ ] **Step 1: Create directory structure**

```
mkdir tools\hnp-cli\bin tools\hnp-cli\src\commands tools\hnp-cli\tests
```

- [ ] **Step 2: Create `tools/hnp-cli/package.json`**

```json
{
  "name": "hnp-cli",
  "version": "1.0.0",
  "description": "HNP + Toggl + Git workflow CLI for CrimsonDraft team",
  "type": "module",
  "bin": {
    "hnp": "./bin/hnp.js"
  },
  "engines": {
    "node": ">=18.0.0"
  },
  "dependencies": {
    "@clack/prompts": "^0.7.0"
  }
}
```

- [ ] **Step 3: Create stub `tools/hnp-cli/bin/hnp.js`**

```js
#!/usr/bin/env node
console.log('HNP CLI — stub');
```

- [ ] **Step 4: Install dependencies**

```
cd tools/hnp-cli
npm install
```

Expected: `node_modules/` created with `@clack/prompts`.

- [ ] **Step 5: Verify the stub runs**

```
node bin/hnp.js
```

Expected output: `HNP CLI — stub`

- [ ] **Step 6: Commit**

```bash
git add tools/hnp-cli/
git commit -m "feat(tools): scaffold hnp-cli package"
```

---

## Task 2: Config Module

**Files:**
- Create: `tools/hnp-cli/src/config.js`
- Create: `tools/hnp-cli/tests/config.test.js`

The config module reads/writes two files in `~/.hnp/`. It never touches the repo.

- [ ] **Step 1: Write the failing tests**

`tools/hnp-cli/tests/config.test.js`:

```js
import { test } from 'node:test';
import { strict as assert } from 'node:assert';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

// Point the module at a temp dir by monkey-patching the home dir env var
const tmpHome = mkdtempSync(join(tmpdir(), 'hnp-test-'));
process.env.HOME = tmpHome;       // Unix
process.env.USERPROFILE = tmpHome; // Windows

// Dynamic import after env is set
const { loadConfig, saveConfig, loadState, saveState, clearState } =
  await import('../src/config.js');

test('saveConfig then loadConfig round-trips the object', () => {
  const cfg = { hnpApiKey: 'a', hnpProjectId: 'b', togglApiKey: 'c', togglWorkspaceId: 123 };
  saveConfig(cfg);
  assert.deepEqual(loadConfig(), cfg);
});

test('loadConfig throws when config file does not exist', () => {
  clearState(); // make sure we're clean
  // Remove config by writing to a fresh temp dir
  const fresh = mkdtempSync(join(tmpdir(), 'hnp-fresh-'));
  process.env.HOME = fresh;
  process.env.USERPROFILE = fresh;
  assert.throws(() => loadConfig(), /hnp setup/);
  process.env.HOME = tmpHome;
  process.env.USERPROFILE = tmpHome;
});

test('saveState then loadState round-trips the object', () => {
  const state = { taskId: '1', taskName: 'test', startedAt: '2026-06-11T00:00:00Z', togglEntryId: '99' };
  saveState(state);
  assert.deepEqual(loadState(), state);
});

test('loadState returns null when no state file exists', () => {
  clearState();
  assert.equal(loadState(), null);
});

test('clearState removes the state file', () => {
  saveState({ taskId: '1', taskName: 'x', startedAt: 'y', togglEntryId: 'z' });
  clearState();
  assert.equal(loadState(), null);
});
```

- [ ] **Step 2: Run tests to confirm they fail**

```
cd tools/hnp-cli
node --test tests/config.test.js
```

Expected: import error — `config.js` does not exist yet.

- [ ] **Step 3: Implement `tools/hnp-cli/src/config.js`**

```js
import { readFileSync, writeFileSync, mkdirSync, existsSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { homedir } from 'node:os';

function hnpDir() {
  const home = process.env.USERPROFILE || process.env.HOME || homedir();
  const dir = join(home, '.hnp');
  if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
  return dir;
}

function configPath() { return join(hnpDir(), 'config.json'); }
function statePath()  { return join(hnpDir(), 'state.json'); }

export function loadConfig() {
  if (!existsSync(configPath())) {
    throw new Error('Config not found. Run `hnp setup` first.');
  }
  return JSON.parse(readFileSync(configPath(), 'utf-8'));
}

export function saveConfig(config) {
  writeFileSync(configPath(), JSON.stringify(config, null, 2), 'utf-8');
}

export function loadState() {
  if (!existsSync(statePath())) return null;
  return JSON.parse(readFileSync(statePath(), 'utf-8'));
}

export function saveState(state) {
  writeFileSync(statePath(), JSON.stringify(state, null, 2), 'utf-8');
}

export function clearState() {
  if (existsSync(statePath())) rmSync(statePath());
}
```

- [ ] **Step 4: Run tests — expect all to pass**

```
node --test tests/config.test.js
```

Expected: `5 tests pass`

- [ ] **Step 5: Commit**

```bash
git add tools/hnp-cli/src/config.js tools/hnp-cli/tests/config.test.js
git commit -m "feat(tools): add config module with state read/write"
```

---

## Task 3: Git Module

**Files:**
- Create: `tools/hnp-cli/src/git.js`
- Create: `tools/hnp-cli/tests/git.test.js`

The git module runs `git log` from the current working directory and returns an array of commit subjects. Returns `null` if not in a git repo.

- [ ] **Step 1: Write the failing tests**

`tools/hnp-cli/tests/git.test.js`:

```js
import { test } from 'node:test';
import { strict as assert } from 'node:assert';
import { parseCommitOutput } from '../src/git.js';

test('parseCommitOutput returns array of non-empty lines', () => {
  const raw = 'feat: add door transition\nfix: resolve NavMesh issue\nrefactor: clean up combat\n';
  assert.deepEqual(parseCommitOutput(raw), [
    'feat: add door transition',
    'fix: resolve NavMesh issue',
    'refactor: clean up combat',
  ]);
});

test('parseCommitOutput returns empty array for blank output', () => {
  assert.deepEqual(parseCommitOutput(''), []);
  assert.deepEqual(parseCommitOutput('\n\n'), []);
});

test('parseCommitOutput filters blank lines', () => {
  const raw = 'feat: one\n\nfeat: two\n';
  assert.deepEqual(parseCommitOutput(raw), ['feat: one', 'feat: two']);
});
```

- [ ] **Step 2: Run tests — expect failure**

```
node --test tests/git.test.js
```

Expected: import error — `git.js` does not exist yet.

- [ ] **Step 3: Implement `tools/hnp-cli/src/git.js`**

```js
import { execSync } from 'node:child_process';

export function parseCommitOutput(raw) {
  return raw.trim().split('\n').filter(Boolean);
}

export function getCommitsSince(isoTimestamp) {
  try {
    const output = execSync(
      `git log --after="${isoTimestamp}" --format="%s" --no-merges`,
      { encoding: 'utf-8', stdio: ['pipe', 'pipe', 'pipe'] }
    );
    return parseCommitOutput(output);
  } catch {
    return null; // not a git repo or git unavailable
  }
}
```

- [ ] **Step 4: Run tests — expect all to pass**

```
node --test tests/git.test.js
```

Expected: `3 tests pass`

- [ ] **Step 5: Commit**

```bash
git add tools/hnp-cli/src/git.js tools/hnp-cli/tests/git.test.js
git commit -m "feat(tools): add git module for commit log parsing"
```

---

## Task 4: Toggl Client

**Files:**
- Create: `tools/hnp-cli/src/toggl-client.js`

Wraps three Toggl Track v9 API calls. Auth is HTTP Basic with the API key as username and the literal string `api_token` as password.

- [ ] **Step 1: Create `tools/hnp-cli/src/toggl-client.js`**

```js
const BASE = 'https://api.track.toggl.com/api/v9';

function authHeader(apiKey) {
  return 'Basic ' + Buffer.from(`${apiKey}:api_token`).toString('base64');
}

async function togglFetch(path, apiKey, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Authorization': authHeader(apiKey),
      'Content-Type': 'application/json',
      ...(options.headers ?? {}),
    },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`Toggl API error ${res.status}: ${text}`);
  }
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export async function getWorkspaceId(apiKey) {
  const me = await togglFetch('/me', apiKey);
  return me.default_workspace_id;
}

export async function startTimer(taskName, apiKey, workspaceId) {
  const entry = await togglFetch(`/workspaces/${workspaceId}/time_entries`, apiKey, {
    method: 'POST',
    body: JSON.stringify({
      description: taskName,
      duration: -1,
      start: new Date().toISOString(),
      workspace_id: workspaceId,
      created_with: 'hnp-cli',
    }),
  });
  return { id: entry.id };
}

export async function stopTimer(entryId, apiKey, workspaceId) {
  await togglFetch(`/workspaces/${workspaceId}/time_entries/${entryId}/stop`, apiKey, {
    method: 'PATCH',
  });
}
```

- [ ] **Step 2: Manual smoke test (requires a real Toggl API key)**

```
node -e "
import('./src/toggl-client.js').then(async ({ getWorkspaceId }) => {
  const id = await getWorkspaceId('YOUR_TOGGL_API_KEY');
  console.log('workspace id:', id);
});
"
```

Expected: prints your Toggl workspace ID as a number.

- [ ] **Step 3: Commit**

```bash
git add tools/hnp-cli/src/toggl-client.js
git commit -m "feat(tools): add Toggl Track API client"
```

---

## Task 5: HNP Client

**Files:**
- Create: `tools/hnp-cli/src/hnp-client.js`

Wraps four Hack N Plan API v0 calls. Auth is the `Authorization: ApiKey <key>` header.

> **Note:** If any endpoint returns 404, verify exact paths against the [HNP API docs](https://hacknplan.com/knowledge-base/api/). Field names (`isActive`, `stageId`) should match what the API returns.

- [ ] **Step 1: Create `tools/hnp-cli/src/hnp-client.js`**

```js
const BASE = 'https://api.hacknplan.com/v0';

async function hnpFetch(path, apiKey, options = {}) {
  const res = await fetch(`${BASE}${path}`, {
    ...options,
    headers: {
      'Authorization': `ApiKey ${apiKey}`,
      'Content-Type': 'application/json',
      ...(options.headers ?? {}),
    },
  });
  if (!res.ok) {
    const text = await res.text();
    throw new Error(`HNP API error ${res.status}: ${text}`);
  }
  const text = await res.text();
  return text ? JSON.parse(text) : null;
}

export async function fetchSprintTasks(projectId, apiKey) {
  // Fetch active milestones (sprints)
  const milestones = await hnpFetch(`/projects/${projectId}/milestones`, apiKey);
  const active = milestones.find(m => m.isActive) ?? milestones[0];

  const query = active ? `?milestoneId=${active.id}` : '';
  const items = await hnpFetch(`/projects/${projectId}/workitems${query}`, apiKey);
  return items.map(i => ({ id: String(i.id), name: i.name }));
}

export async function fetchTaskStages(projectId, apiKey) {
  const stages = await hnpFetch(`/projects/${projectId}/stages`, apiKey);
  return stages.map(s => ({ id: String(s.id), name: s.name }));
}

export async function postComment(projectId, taskId, text, apiKey) {
  await hnpFetch(`/projects/${projectId}/workitems/${taskId}/comments`, apiKey, {
    method: 'POST',
    body: JSON.stringify({ text }),
  });
}

export async function updateTaskStage(projectId, taskId, stageId, apiKey) {
  await hnpFetch(`/projects/${projectId}/workitems/${taskId}`, apiKey, {
    method: 'PUT',
    body: JSON.stringify({ stageId }),
  });
}
```

- [ ] **Step 2: Manual smoke test (requires real HNP credentials)**

```
node -e "
import('./src/hnp-client.js').then(async ({ fetchSprintTasks }) => {
  const tasks = await fetchSprintTasks('YOUR_PROJECT_ID', 'YOUR_HNP_API_KEY');
  console.log(tasks.slice(0, 3));
});
"
```

Expected: prints an array of `{ id, name }` objects from your active sprint.

- [ ] **Step 3: Commit**

```bash
git add tools/hnp-cli/src/hnp-client.js
git commit -m "feat(tools): add Hack N Plan API client"
```

---

## Task 6: Setup Command

**Files:**
- Create: `tools/hnp-cli/src/commands/setup.js`

Guides the user through entering their personal API keys. Fetches the Toggl workspace ID automatically so it doesn't need to be entered manually.

- [ ] **Step 1: Create `tools/hnp-cli/src/commands/setup.js`**

```js
import { intro, outro, text, spinner, note } from '@clack/prompts';
import { saveConfig } from '../config.js';
import { getWorkspaceId } from '../toggl-client.js';

export async function runSetup() {
  intro('HNP CLI — Setup');

  const hnpApiKey = await text({
    message: '¿Tu HNP API key?',
    validate: v => v.trim() ? undefined : 'Requerida',
  });

  const hnpProjectId = await text({
    message: '¿Project ID de HNP?',
    validate: v => v.trim() ? undefined : 'Requerido',
  });

  const togglApiKey = await text({
    message: '¿Tu Toggl Track API key?',
    validate: v => v.trim() ? undefined : 'Requerida',
  });

  const s = spinner();
  s.start('Verificando credenciales de Toggl...');
  let togglWorkspaceId;
  try {
    togglWorkspaceId = await getWorkspaceId(togglApiKey.trim());
    s.stop(`Workspace detectado: ${togglWorkspaceId}`);
  } catch (err) {
    s.stop('Error verificando Toggl');
    note(err.message, 'Error');
    process.exit(1);
  }

  saveConfig({
    hnpApiKey: hnpApiKey.trim(),
    hnpProjectId: hnpProjectId.trim(),
    togglApiKey: togglApiKey.trim(),
    togglWorkspaceId,
  });

  outro('Configuración guardada en ~/.hnp/config.json');
}
```

- [ ] **Step 2: Manual test**

Wire setup into the bin temporarily (next task does this properly):
```
node -e "import('./src/commands/setup.js').then(m => m.runSetup())"
```

Enter real credentials. Expected:
- Toggl workspace ID printed
- `~/.hnp/config.json` written with all four fields

- [ ] **Step 3: Commit**

```bash
git add tools/hnp-cli/src/commands/setup.js
git commit -m "feat(tools): add hnp setup command"
```

---

## Task 8: Start Command

**Files:**
- Create: `tools/hnp-cli/src/commands/start.js`

- [ ] **Step 1: Create `tools/hnp-cli/src/commands/start.js`**

```js
import { intro, outro, select, confirm, spinner, isCancel, cancel, note } from '@clack/prompts';
import { loadConfig, loadState, saveState } from '../config.js';
import { fetchSprintTasks } from '../hnp-client.js';
import { startTimer } from '../toggl-client.js';
import { runStop } from './stop.js';

export async function runStart() {
  intro('HNP CLI — Start');

  let config;
  try {
    config = loadConfig();
  } catch (err) {
    note(err.message, 'Error');
    process.exit(1);
  }

  // Check for existing session
  const existing = loadState();
  if (existing) {
    const close = await confirm({
      message: `Ya hay una sesión activa: "${existing.taskName}". ¿Cerrarla primero?`,
    });
    if (isCancel(close) || !close) { cancel('Cancelado.'); process.exit(0); }
    await runStop({ silent: true });
  }

  // Fetch tasks
  const s = spinner();
  s.start('Cargando tareas del sprint...');
  let tasks;
  try {
    tasks = await fetchSprintTasks(config.hnpProjectId, config.hnpApiKey);
    s.stop(`${tasks.length} tareas encontradas`);
  } catch (err) {
    s.stop('Error cargando tareas');
    note(err.message, 'Error');
    process.exit(1);
  }

  if (tasks.length === 0) {
    note('No hay tareas en el sprint activo.', 'Sin tareas');
    process.exit(0);
  }

  // Select task
  const taskId = await select({
    message: 'Seleccioná una tarea:',
    options: tasks.map(t => ({ value: t.id, label: t.name })),
  });
  if (isCancel(taskId)) { cancel('Cancelado.'); process.exit(0); }

  const taskName = tasks.find(t => t.id === taskId).name;

  // Start Toggl timer
  const s2 = spinner();
  s2.start('Iniciando timer en Toggl...');
  let togglEntry;
  try {
    togglEntry = await startTimer(taskName, config.togglApiKey, config.togglWorkspaceId);
    s2.stop('Timer iniciado');
  } catch (err) {
    s2.stop('Error iniciando timer en Toggl');
    note(err.message, 'Error');
    process.exit(1);
  }

  // Persist session
  saveState({
    taskId,
    taskName,
    startedAt: new Date().toISOString(),
    togglEntryId: String(togglEntry.id),
  });

  outro(`Sesión iniciada: "${taskName}"`);
}
```

- [ ] **Step 2: Manual test (requires configured `~/.hnp/config.json`)**

```
node -e "import('./src/commands/start.js').then(m => m.runStart())"
```

Expected:
- Task list appears
- After selection, Toggl timer starts
- `~/.hnp/state.json` written with `taskId`, `taskName`, `startedAt`, `togglEntryId`

- [ ] **Step 3: Commit**

```bash
git add tools/hnp-cli/src/commands/start.js
git commit -m "feat(tools): add hnp start command"
```

---

## Task 7: Stop Command

**Files:**
- Create: `tools/hnp-cli/src/commands/stop.js`

- [ ] **Step 1: Create `tools/hnp-cli/src/commands/stop.js`**

```js
import { intro, outro, select, confirm, spinner, isCancel, cancel, note } from '@clack/prompts';
import { loadConfig, loadState, clearState } from '../config.js';
import { getCommitsSince } from '../git.js';
import { stopTimer } from '../toggl-client.js';
import { postComment, fetchTaskStages, updateTaskStage } from '../hnp-client.js';

function formatDuration(startedAt) {
  const ms = Date.now() - new Date(startedAt).getTime();
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function buildComment(commits, taskName, startedAt) {
  const duration = formatDuration(startedAt);
  const lines = commits && commits.length > 0
    ? commits.map(c => `- ${c}`).join('\n')
    : '_Sin commits en esta sesión._';
  return `**Sesión registrada desde hnp-cli**\n\nTarea: ${taskName}\nDuración: ${duration}\n\n**Commits:**\n${lines}`;
}

export async function runStop({ silent = false } = {}) {
  if (!silent) intro('HNP CLI — Stop');

  const state = loadState();
  if (!state) {
    note('No hay sesión activa.', 'Error');
    process.exit(1);
  }

  let config;
  try {
    config = loadConfig();
  } catch (err) {
    note(err.message, 'Error');
    process.exit(1);
  }

  // Stop Toggl timer
  const s1 = spinner();
  s1.start('Parando timer en Toggl...');
  try {
    await stopTimer(state.togglEntryId, config.togglApiKey, config.togglWorkspaceId);
    s1.stop(`Timer parado (${formatDuration(state.startedAt)})`);
  } catch (err) {
    s1.stop('No se pudo parar el timer en Toggl');
    note(err.message, 'Advertencia — continuando');
  }

  // Collect commits
  const commits = getCommitsSince(state.startedAt);
  if (commits === null) {
    note('No se encontró un repositorio git. Se registrará sin commits.', 'Advertencia');
  }

  // Post HNP comment
  const s2 = spinner();
  s2.start('Registrando en Hack N Plan...');
  try {
    const comment = buildComment(commits, state.taskName, state.startedAt);
    await postComment(config.hnpProjectId, state.taskId, comment, config.hnpApiKey);
    s2.stop('Comentario agregado en HNP');
  } catch (err) {
    s2.stop('Error al comentar en HNP');
    note(`${err.message}\nPodés reintentar con \`hnp stop\`.`, 'Error');
    process.exit(1);
  }

  // Prompt status change
  if (!silent) {
    const changeStatus = await confirm({ message: '¿Cambiar el estado de la tarea?' });
    if (!isCancel(changeStatus) && changeStatus) {
      const s3 = spinner();
      s3.start('Cargando estados...');
      let stages;
      try {
        stages = await fetchTaskStages(config.hnpProjectId, config.hnpApiKey);
        s3.stop('');
      } catch (err) {
        s3.stop('No se pudieron cargar los estados');
        note(err.message, 'Advertencia');
        stages = [];
      }

      if (stages.length > 0) {
        const stageId = await select({
          message: 'Nuevo estado:',
          options: stages.map(s => ({ value: s.id, label: s.name })),
        });
        if (!isCancel(stageId)) {
          const s4 = spinner();
          s4.start('Actualizando estado...');
          try {
            await updateTaskStage(config.hnpProjectId, state.taskId, stageId, config.hnpApiKey);
            s4.stop('Estado actualizado');
          } catch (err) {
            s4.stop('Error actualizando estado');
            note(err.message, 'Advertencia');
          }
        }
      }
    }
  }

  clearState();
  if (!silent) outro(`Sesión cerrada: "${state.taskName}"`);
}
```

- [ ] **Step 2: Manual test (with an active session from `hnp start`)**

```
node -e "import('./src/commands/stop.js').then(m => m.runStop())"
```

Expected:
- Toggl timer stops
- HNP comment appears on the task with commit list and duration
- Status change prompt appears
- `~/.hnp/state.json` deleted on success

- [ ] **Step 3: Test error resilience — run `hnp stop` with no active session**

```
node -e "import('./src/commands/stop.js').then(m => m.runStop())"
```

Expected: prints "No hay sesión activa." and exits.

- [ ] **Step 4: Commit**

```bash
git add tools/hnp-cli/src/commands/stop.js
git commit -m "feat(tools): add hnp stop command"
```

---

## Task 9: Status Command

**Files:**
- Create: `tools/hnp-cli/src/commands/status.js`

- [ ] **Step 1: Create `tools/hnp-cli/src/commands/status.js`**

```js
import { intro, outro, note } from '@clack/prompts';
import { loadState } from '../config.js';

function formatDuration(startedAt) {
  const ms = Date.now() - new Date(startedAt).getTime();
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export function runStatus() {
  intro('HNP CLI — Status');

  const state = loadState();
  if (!state) {
    note('No hay sesión activa.', 'Status');
    process.exit(0);
  }

  const duration = formatDuration(state.startedAt);
  note(`Tarea: ${state.taskName}\nEn progreso hace: ${duration}`, 'Sesión activa');
  outro('');
}
```

- [ ] **Step 2: Manual test (with and without active session)**

```
node -e "import('./src/commands/status.js').then(m => m.runStatus())"
```

With session: prints task name and elapsed time.
Without session: prints "No hay sesión activa."

- [ ] **Step 3: Commit**

```bash
git add tools/hnp-cli/src/commands/status.js
git commit -m "feat(tools): add hnp status command"
```

---

## Task 10: Entry Point

**Files:**
- Modify: `tools/hnp-cli/bin/hnp.js`

Replace the stub with the real command router.

- [ ] **Step 1: Replace `tools/hnp-cli/bin/hnp.js`**

```js
#!/usr/bin/env node
import { runSetup }  from '../src/commands/setup.js';
import { runStart }  from '../src/commands/start.js';
import { runStop }   from '../src/commands/stop.js';
import { runStatus } from '../src/commands/status.js';

const command = process.argv[2];

switch (command) {
  case 'setup':  await runSetup();  break;
  case 'start':  await runStart();  break;
  case 'stop':   await runStop();   break;
  case 'status': await runStatus(); break;
  default:
    console.log(`Comandos disponibles: setup, start, stop, status`);
    process.exit(1);
}
```

- [ ] **Step 2: Register globally with npm link**

```
cd tools/hnp-cli
npm link
```

Expected: `hnp` is now available as a global command.

- [ ] **Step 3: End-to-end smoke test**

```
hnp setup
hnp start
# trabajar, hacer commits
hnp stop
hnp status    # should print "No hay sesión activa"
```

- [ ] **Step 4: Commit**

```bash
git add tools/hnp-cli/bin/hnp.js
git commit -m "feat(tools): wire entry point — hnp CLI complete"
```

---

## Task 11: README

**Files:**
- Create: `tools/hnp-cli/README.md`

- [ ] **Step 1: Create `tools/hnp-cli/README.md`**

```markdown
# hnp-cli

CLI para conectar Hack N Plan, Toggl Track y Git en un solo flujo de trabajo.

## Setup (una vez por máquina)

1. **Descargar el código** (ya incluido en el repo):
   ```
   git pull
   ```

2. **Instalar dependencias:**
   ```
   cd tools/hnp-cli
   npm install
   ```

3. **Registrar el comando globalmente:**
   ```
   npm link
   ```

4. **Configurar tus credenciales:**
   ```
   hnp setup
   ```
   Ingresás tu HNP API key, el Project ID, y tu Toggl API key.
   Se guarda en `~/.hnp/config.json` — nunca entra al repo.

## Uso

```
hnp start     # seleccioná una tarea del sprint e iniciá el timer
hnp stop      # pará el timer, registrá commits en HNP, cambiá estado
hnp status    # mostrá la tarea activa y el tiempo transcurrido
```

## Dónde encontrar las API keys

- **HNP API key:** Hack N Plan → Settings → API
- **Toggl API key:** toggl.com → Profile → API token
- **HNP Project ID:** URL del proyecto en HNP (`/projects/{ID}/...`)
```

- [ ] **Step 2: Commit**

```bash
git add tools/hnp-cli/README.md
git commit -m "docs(tools): add hnp-cli setup README"
```

---

## All Tests

Run the full test suite at any point:

```
cd tools/hnp-cli
node --test tests/config.test.js tests/git.test.js
```

Expected: `8 tests pass`
