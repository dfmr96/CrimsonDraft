#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    public sealed class NoteDetailView : MonoBehaviour
    {
        [SerializeField] private GameObject  panel        = null!;
        [SerializeField] private TMP_Text    titleLabel   = null!;
        [SerializeField] private TMP_Text    bodyLabel    = null!;
        [SerializeField] private TMP_Text?   pageLabel;
        [SerializeField] private Image?      pageImage;
        [SerializeField] private TMP_Text?   bigTitleLabel;

        public bool IsOpen { get; private set; }

        public void Show(string title, string body, int page, int total)
        {
            this.titleLabel.text = title;
            this.titleLabel.gameObject.SetActive(true);
            this.bodyLabel.text  = body;
            this.bodyLabel.gameObject.SetActive(true);

            if (this.pageImage != null)
                this.pageImage.gameObject.SetActive(false);
            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        /// <summary>Text-only page (no title) — used after the dedicated title page in image docs.</summary>
        public void ShowBodyOnly(string body, int page, int total)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.text = body;
            this.bodyLabel.gameObject.SetActive(true);

            if (this.pageImage != null)
                this.pageImage.gameObject.SetActive(false);
            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        public void ShowTitleOnly(string title, int page, int total)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.gameObject.SetActive(false);

            if (this.pageImage != null)
                this.pageImage.gameObject.SetActive(false);

            if (this.bigTitleLabel != null)
            {
                this.bigTitleLabel.text = title;
                this.bigTitleLabel.gameObject.SetActive(true);
            }

            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        public void ShowImage(Sprite image, int page, int total)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.gameObject.SetActive(false);
            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            if (this.pageImage != null)
            {
                this.pageImage.sprite = image;
                this.pageImage.gameObject.SetActive(true);
                this.pageImage.SetNativeSize();
            }

            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        void SetPageLabel(int page, int total)
        {
            if (this.pageLabel != null)
                this.pageLabel.text = total > 1 ? $"{page} / {total}" : string.Empty;
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            this.IsOpen = false;
        }
    }
}
