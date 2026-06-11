Start a new HNP work session. Follow these steps in order, stopping if any step fails.

**Step 1 — Check for existing session**

Run in Bash:
```
cat "$USERPROFILE/.hnp/state.json" 2>/dev/null || cat "$HOME/.hnp/state.json" 2>/dev/null
```

Parse the JSON output and extract the `taskName` field. Use the actual value in the Spanish message below (do not print `[taskName]` literally).

If a session exists, ask the user:
"Ya hay una sesión activa: '[taskName]'. ¿La cerramos antes de empezar? (s/n)"

- If yes: run /hnp-stop first, then continue with Step 2.
- If no: stop here.

**Step 2 — Fetch sprint tasks from HNP**

First, check that `$HNP_API_KEY` and `$HNP_PROJECT_ID` are set. If either is empty, tell the user:
"Faltan variables de entorno. Seguí las instrucciones en tools/hnp/README.md."
Stop here.

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

**Step 3 — Present task list and get user selection**

Show the list of tasks with their names and IDs. Ask the user to select one.
Wait for the user to respond with a task name or a 1-based index from the displayed list.

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
(Use ISO 8601 UTC format, e.g. `2026-06-11T14:00:00.000Z` — equivalent to JavaScript's `new Date().toISOString()`)
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
