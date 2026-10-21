#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure
{
    public sealed class NoteRegistry
    {
        private readonly HashSet<string> collected = new();

        [Preserve]
        public NoteRegistry() { }

        public bool IsCollected(string noteId) => this.collected.Contains(noteId);
        public event Action? Changed;

        public void SetCollected(string noteId)
        {
            if (this.collected.Add(noteId)) Changed?.Invoke();
        }

        public IReadOnlyCollection<string> CollectedIds => this.collected;

        public void LoadState(IEnumerable<string> saved)
        {
            this.collected.Clear();
            foreach (var id in saved)
                this.collected.Add(id);
        }

        public void ClearAll() => this.collected.Clear();
    }
}
