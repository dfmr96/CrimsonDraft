#nullable enable

#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Linq;
using System;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.DebugMenu;
using UnityEngine;
using VContainer.Unity;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// Debug menu entry ("Unlock all notes") that adds every note of the NoteDatabase to the player's
    /// collection, so they can all be read from the Files tab during gameplay.
    /// Editor and development builds only.
    /// </summary>
    public sealed class DebugNoteUnlocker : IInitializable, IDisposable
    {
        private readonly IDebugMenu         menu;
        private readonly NoteRegistry       registry;
        private readonly FilesTabController filesTab;

        private IDisposable? registration;

        public DebugNoteUnlocker(IDebugMenu menu, NoteRegistry registry, FilesTabController filesTab)
        {
            this.menu     = menu;
            this.registry = registry;
            this.filesTab = filesTab;
        }

        public void Initialize()
        {
            this.registration = this.menu.Register(DebugMenuEntry.Action("Inventory", "Unlock all notes", UnlockAll));
        }

        public void Dispose() => this.registration?.Dispose();

        private void UnlockAll()
        {
            int added = 0;
            foreach (var note in this.filesTab.Database.Notes.Where(n => n != null && !string.IsNullOrEmpty(n.NoteId)))
            {
                if (this.registry.IsCollected(note.NoteId)) continue;
                this.registry.SetCollected(note.NoteId);
                added++;
            }

            Debug.Log($"[Notes] Debug unlock: {added} note(s) added to the collection.");
        }
    }
}

#endif
