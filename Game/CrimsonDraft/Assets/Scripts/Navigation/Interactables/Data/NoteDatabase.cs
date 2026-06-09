#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/NoteDatabase")]
    public sealed class NoteDatabase : ScriptableObject
    {
        [SerializeField] private DocumentData[] notes = System.Array.Empty<DocumentData>();
        public DocumentData[] Notes => this.notes;
    }
}
