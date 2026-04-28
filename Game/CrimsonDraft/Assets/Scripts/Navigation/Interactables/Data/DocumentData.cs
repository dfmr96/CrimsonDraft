#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Interactables/DocumentData")]
    public sealed class DocumentData : ScriptableObject
    {
        [SerializeField] private string title = string.Empty;

        [TextArea(3, 10)]
        [SerializeField] private string[] pages = System.Array.Empty<string>();

        public string   Title => this.title;
        public string[] Pages => this.pages;
    }
}
