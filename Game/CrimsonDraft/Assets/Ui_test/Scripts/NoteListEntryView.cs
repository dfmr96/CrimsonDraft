#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    public sealed class NoteListEntryView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel = null!;
        [SerializeField] private Image?   highlight;

        public void Bind(string title)        => this.titleLabel.text = title;
        public void SetHighlight(bool active) => this.highlight?.gameObject.SetActive(active);
    }
}
