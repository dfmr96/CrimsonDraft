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

        [Header("Chrome hidden while the page image fills the screen")]
        [SerializeField] private GameObject? filesBrowserChrome;
        [SerializeField] private GameObject? tabBarChrome;

        [Header("Background watermark (text pages of an image doc)")]
        [SerializeField] private float backgroundImageAlpha = 0.06f;

        public bool IsOpen { get; private set; }

        public void Show(string title, string body, int page, int total)
        {
            this.titleLabel.text = title;
            this.titleLabel.gameObject.SetActive(true);
            this.bodyLabel.text                 = body;
            this.bodyLabel.maxVisibleCharacters = int.MaxValue;
            this.bodyLabel.gameObject.SetActive(true);

            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            SetPageImage(null, 1f);
            SetChromeVisible(true);
            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        /// <summary>Text-only page (no title) — used after the dedicated title page in image docs.</summary>
        public void ShowBodyOnly(string body, int page, int total, Sprite? backgroundImage = null)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.text                 = body;
            this.bodyLabel.maxVisibleCharacters = int.MaxValue;
            this.bodyLabel.gameObject.SetActive(true);

            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            SetPageImage(backgroundImage, this.backgroundImageAlpha);
            SetChromeVisible(true);
            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        public void ShowTitleOnly(string title, int page, int total, Sprite? backgroundImage = null)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.gameObject.SetActive(false);

            if (this.bigTitleLabel != null)
            {
                this.bigTitleLabel.text = title;
                this.bigTitleLabel.gameObject.SetActive(true);
            }

            SetPageImage(backgroundImage, this.backgroundImageAlpha);
            SetChromeVisible(true);
            SetPageLabel(page, total);
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        public void ShowImage(Sprite image)
        {
            this.titleLabel.gameObject.SetActive(false);
            this.bodyLabel.gameObject.SetActive(false);
            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            // Deliberately not SetNativeSize() -- pageImage's RectTransform is a fixed box and
            // its Image has Preserve Aspect on, so any sprite regardless of source resolution
            // or aspect ratio is contained within that box instead of overflowing the panel.
            SetPageImage(image, 1f);

            // Nothing but the scanned page should be on screen for this reveal -- including
            // the page counter, which only starts once the reader moves past it.
            SetChromeVisible(false);
            if (this.pageLabel != null) this.pageLabel.text = string.Empty;
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        /// <summary>
        /// Voice-note transcript reveal: <paramref name="body"/> is the paragraph's full markup
        /// text but only <paramref name="visibleChars"/> of it are shown (TMP hides the rest
        /// without breaking rich-text tags), and the page counter is replaced by a countdown of
        /// time left in the whole recording.
        /// </summary>
        public void ShowVoiceBody(string title, string body, int visibleChars, string timerText, bool showTitleInline, Sprite? backgroundImage = null)
        {
            this.titleLabel.text = title;
            this.titleLabel.gameObject.SetActive(showTitleInline);

            this.bodyLabel.text                 = body;
            this.bodyLabel.maxVisibleCharacters = visibleChars;
            this.bodyLabel.gameObject.SetActive(true);

            if (this.bigTitleLabel != null)
                this.bigTitleLabel.gameObject.SetActive(false);

            SetPageImage(backgroundImage, this.backgroundImageAlpha);
            SetChromeVisible(true);
            if (this.pageLabel != null) this.pageLabel.text = timerText;
            this.panel.SetActive(true);
            this.IsOpen = true;
        }

        void SetPageImage(Sprite? sprite, float alpha)
        {
            if (this.pageImage == null) return;

            if (sprite == null)
            {
                this.pageImage.gameObject.SetActive(false);
                return;
            }

            this.pageImage.sprite = sprite;
            this.pageImage.gameObject.SetActive(true);
            this.pageImage.transform.SetSiblingIndex(1); // stay behind the text labels

            var color = this.pageImage.color;
            color.a = alpha;
            this.pageImage.color = color;
        }

        void SetChromeVisible(bool visible)
        {
            this.filesBrowserChrome?.SetActive(visible);
            this.tabBarChrome?.SetActive(visible);
        }

        void SetPageLabel(int page, int total)
        {
            if (this.pageLabel != null)
                this.pageLabel.text = total > 1 ? $"<{page:00}/{total:00}>" : string.Empty;
        }

        public void Hide()
        {
            this.panel.SetActive(false);
            SetChromeVisible(true);
            this.IsOpen = false;
        }
    }
}
