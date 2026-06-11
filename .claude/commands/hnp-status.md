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
