#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DocumentData")]
    public sealed class DocumentData : ScriptableObject
    {
        [SerializeField] private string           title     = string.Empty;
        [SerializeField] private DocumentCategory category  = DocumentCategory.Notes;
        [SerializeField] private Sprite?          icon;
        [SerializeField] private string           noteId    = string.Empty;
        [SerializeField] private Sprite?          pageImage;

        [Tooltip("Text alignment on the note page. UseDefault follows NoteLayoutSettings.")]
        [SerializeField] private NoteAlignment    alignment = NoteAlignment.UseDefault;

        public string           Title     => this.title;
        public DocumentCategory Category  => this.category;
        public Sprite?          Icon      => this.icon;
        public string           NoteId    => this.noteId;
        public Sprite?          PageImage => this.pageImage;
        public NoteAlignment    Alignment => this.alignment;
    }
}
