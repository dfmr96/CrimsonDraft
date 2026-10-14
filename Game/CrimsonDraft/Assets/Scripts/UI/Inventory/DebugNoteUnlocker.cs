#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Linq;
using CrimsonDraft.Infrastructure;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// Debug shortcut: Shift + O + P adds every note of the NoteDatabase to the player's
    /// collection, so they can all be read from the Files tab during gameplay.
    /// Editor and development builds only.
    /// </summary>
    public sealed class DebugNoteUnlocker : ITickable
    {
        private readonly NoteRegistry       registry;
        private readonly FilesTabController filesTab;

        public DebugNoteUnlocker(NoteRegistry registry, FilesTabController filesTab)
        {
            this.registry = registry;
            this.filesTab = filesTab;
        }

        public void Tick()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (!shift || !kb.oKey.isPressed || !kb.pKey.isPressed) return;

            // Fire once per chord: only on the frame the last of O/P goes down.
            if (!kb.oKey.wasPressedThisFrame && !kb.pKey.wasPressedThisFrame) return;

            int added = 0;
            foreach (var note in this.filesTab.Database.Notes.Where(n => n != null && !string.IsNullOrEmpty(n.NoteId)))
            {
                if (this.registry.IsCollected(note.NoteId)) continue;
                this.registry.SetCollected(note.NoteId);
                added++;
            }

            Debug.Log($"[Notes] Debug unlock (Shift+O+P): {added} note(s) added to the collection.");
        }
    }
}

#endif
