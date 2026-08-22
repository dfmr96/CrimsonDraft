#nullable enable

using UnityEngine;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    /// <summary>
    /// Tracks total playtime across the app session. Time.realtimeSinceStartup resets whenever
    /// the process restarts, so it can't be written to a save directly -- doing so silently
    /// discards whatever playtime a loaded save already had. This adds a restorable base offset
    /// on top of it.
    /// </summary>
    public sealed class PlaytimeTracker
    {
        private float baseSeconds;
        private float sessionStartRealtime;

        [Preserve]
        public PlaytimeTracker()
        {
            this.sessionStartRealtime = Time.realtimeSinceStartup;
        }

        public float CurrentSeconds => this.baseSeconds + (Time.realtimeSinceStartup - this.sessionStartRealtime);

        public void RestoreFrom(float savedSeconds)
        {
            this.baseSeconds          = savedSeconds;
            this.sessionStartRealtime = Time.realtimeSinceStartup;
        }

        public void Reset()
        {
            this.baseSeconds          = 0f;
            this.sessionStartRealtime = Time.realtimeSinceStartup;
        }
    }
}
