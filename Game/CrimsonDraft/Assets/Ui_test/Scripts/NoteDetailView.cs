#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.UI
{
    public sealed class NoteDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject  panel      = null!;
        [SerializeField] private TMP_Text    titleLabel = null!;
        [SerializeField] private TMP_Text    bodyLabel  = null!;
        [SerializeField] private TMP_Text?   pageLabel;

        public bool IsOpen { get; private set; }

        public void Show(string title, string body, int page, int total)
        {
            this.titleLabel.text = title;
            this.bodyLabel.text  = body;

            if (this.pageLabel != null)
                this.pageLabel.text = total > 1 ? $"{page} / {total}" : string.Empty;

            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            this.IsOpen = false;
        }
    }
}
