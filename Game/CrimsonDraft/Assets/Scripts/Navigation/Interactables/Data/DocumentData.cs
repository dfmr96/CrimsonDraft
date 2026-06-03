#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DocumentData")]
    public sealed class DocumentData : ScriptableObject
    {
        [SerializeField] private string           title    = string.Empty;
        [SerializeField] private DocumentCategory category = DocumentCategory.Marinera;
        [SerializeField] private Sprite?          icon;

        [TextArea(3, 10)]
        [SerializeField] private string[] pages = System.Array.Empty<string>();

        public string            Title    => this.title;
        public DocumentCategory  Category => this.category;
        public Sprite?           Icon     => this.icon;
        public string[]          Pages    => this.pages;
    }
}
