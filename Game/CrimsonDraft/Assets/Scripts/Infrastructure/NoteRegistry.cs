#nullable enable

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
        public void SetCollected(string noteId) => this.collected.Add(noteId);

        public IReadOnlyCollection<string> CollectedIds => this.collected;
    }
}
