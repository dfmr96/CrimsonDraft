#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/NoteDatabase")]
    public sealed class NoteDatabase : ScriptableObject
    {
        [SerializeField] private DocumentData[] notes = System.Array.Empty<DocumentData>();
        public DocumentData[] Notes => this.notes;

        public bool Contains(DocumentData note) => System.Array.IndexOf(this.notes, note) >= 0;

        /// <summary>Appends a note at runtime (e.g. when picked up). No-op if already present.</summary>
        public void Add(DocumentData note)
        {
            if (note == null || Contains(note)) return;

            var resized = new DocumentData[this.notes.Length + 1];
            System.Array.Copy(this.notes, resized, this.notes.Length);
            resized[this.notes.Length] = note;
            this.notes = resized;
        }
    }
}
