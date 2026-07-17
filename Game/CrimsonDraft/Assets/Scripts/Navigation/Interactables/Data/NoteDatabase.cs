#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Static catalog of every note in the game. Which ones the player has collected is tracked separately by NoteRegistry.</summary>
    [CreateAssetMenu(menuName = "CrimsonDraft/NoteDatabase")]
    public sealed class NoteDatabase : ScriptableObject
    {
        [SerializeField] private DocumentData[] notes = System.Array.Empty<DocumentData>();
        public DocumentData[] Notes => this.notes;
    }
}
