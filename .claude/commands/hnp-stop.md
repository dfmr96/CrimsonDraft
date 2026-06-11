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

Replace STARTED_AT_VALUE with the actual `startedAt` timestamp from the state file (ISO 8601 UTC, e.g. `2026-06-11T14:00:00.000Z`).

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

Replace `TASK_ID` with the `taskId` parsed from the state file in Step 1.

Post it via Bash:
```bash
curl -s -X POST \
  -H "Authorization: ApiKey $HNP_API_KEY" \
  -H "Content-Type: application/json" \
  -d '"COMMENT_TEXT_JSON_ESCAPED"' \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/TASK_ID/comments"
```

Note: the body is a plain JSON string literal (outer single quotes wrap inner double quotes). Replace `COMMENT_TEXT_JSON_ESCAPED` with the comment text, properly JSON-escaped (newlines as `\n`, quotes as `\"`).

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

Show the stage names and ask the user to pick one by 1-based index or name.

Once selected, update the task via PATCH:
```bash
curl -s -X PATCH \
  -H "Authorization: ApiKey $HNP_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"stageId": SELECTED_STAGE_ID}' \
  "https://api.hacknplan.com/v0/projects/$HNP_PROJECT_ID/workitems/TASK_ID"
```

Replace `SELECTED_STAGE_ID` with the integer `stageId` of the stage the user selected (not quoted — it's a number). Replace `TASK_ID` with the `taskId` from the state file.

If this fails, warn the user but continue to Step 7.

**Step 7 — Clear session state**

Run in Bash:
```bash
rm "$USERPROFILE/.hnp/state.json" 2>/dev/null || rm "$HOME/.hnp/state.json" 2>/dev/null
```

**Step 8 — Confirm to the user**

"Sesión cerrada: [taskName] — [duration]. Tiempo registrado en Toggl y comentario agregado en HNP."
