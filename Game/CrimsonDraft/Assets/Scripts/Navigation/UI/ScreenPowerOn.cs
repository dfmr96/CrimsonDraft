#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// "The screen turns on" when a sub menu panel opens (New Game, Load Game, an Options tab):
    /// the frame (background + border) starts as a flat line at its vertical centre and grows to its
    /// full height; once it has, every other direct child of the panel -- the actual UI -- fades in.
    /// Everything is driven from OnEnable/OnDisable, so the panels' navigators keep activating and
    /// wiring them exactly as before; the content is only held invisible (and unclickable) meanwhile.
    /// <see cref="Close"/> plays it backwards -- content out, then the frame collapses to the line --
    /// and only then deactivates the panel, so callers that used to SetActive(false) call that instead.
    /// </summary>
    public sealed class ScreenPowerOn : MonoBehaviour
    {
        [Tooltip("Marco de la pantalla (BG): se abre en Y desde una linea. El resto de los hijos directos es el contenido.")]
        [SerializeField] private RectTransform frame = null!;
        [SerializeField] private float openDuration = 0.28f;
        [SerializeField] private Ease  openEase     = Ease.OutCubic;
        [Tooltip("Escala Y inicial del marco, como fraccion de la final (la linea).")]
        [SerializeField] private float lineThickness = 0.004f;
        [SerializeField] private float contentFadeDuration = 0.12f;
        [SerializeField] private float closeDuration       = 0.22f;
        [SerializeField] private Ease  closeEase           = Ease.InCubic;

        /// <summary>True once the frame has opened and the content is fading in; false while opening or closing.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>Raised once the frame has finished opening (the content starts to appear).</summary>
        public event System.Action? Opened;

        private CanvasGroup[] content = System.Array.Empty<CanvasGroup>();
        private Vector3       frameScale;
        private Sequence?     sequence;
        private System.Action? pendingClosed;

        private void Awake()
        {
            if (this.frame == null)
            {
                Debug.LogError("ScreenPowerOn needs its frame (the BG) assigned.", this);
                enabled = false;
                return;
            }
            this.frameScale = this.frame.localScale;

            var groups = new System.Collections.Generic.List<CanvasGroup>();
            foreach (Transform child in transform)
            {
                if (child == this.frame) continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();
                groups.Add(group);
            }
            this.content = groups.ToArray();
        }

        private void OnEnable()
        {
            KillSequence();
            this.IsOpen = false;

            var line = this.frameScale;
            line.y *= this.lineThickness;
            this.frame.localScale = line;
            SetContent(alpha: 0f, interactive: false);

            this.sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(this.frame.DOScaleY(this.frameScale.y, this.openDuration).SetEase(this.openEase))
                .AppendCallback(() =>
                {
                    SetContent(alpha: 0f, interactive: true);
                    this.IsOpen = true;
                    this.Opened?.Invoke();
                })
                .Append(DOVirtual.Float(0f, 1f, this.contentFadeDuration, v => SetContent(v, interactive: true)))
                .OnComplete(() => this.sequence = null);
        }

        private void OnDisable()
        {
            KillSequence();
            this.IsOpen = false;
            this.frame.localScale = this.frameScale;
            SetContent(alpha: 1f, interactive: true);

            // Deactivated by someone else mid-close: the caller still has to be told it's closed.
            var callback = this.pendingClosed;
            this.pendingClosed = null;
            callback?.Invoke();
        }

        /// <summary>
        /// Turns the screen off, then deactivates this panel and calls <paramref name="onClosed"/>.
        /// An inactive panel just calls it right away.
        /// </summary>
        public void Close(System.Action? onClosed = null)
        {
            if (!isActiveAndEnabled)
            {
                onClosed?.Invoke();
                return;
            }

            KillSequence();
            this.IsOpen = false;
            this.pendingClosed = onClosed;
            float from = this.content.Length > 0 && this.content[0] != null ? this.content[0].alpha : 1f;
            SetContent(from, interactive: false);

            var line = this.frameScale;
            line.y *= this.lineThickness;

            this.sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(DOVirtual.Float(from, 0f, this.contentFadeDuration * from, v => SetContent(v, interactive: false)))
                .Append(this.frame.DOScaleY(line.y, this.closeDuration).SetEase(this.closeEase))
                .OnComplete(() =>
                {
                    var callback = this.pendingClosed;
                    this.pendingClosed = null;
                    this.sequence = null;
                    gameObject.SetActive(false);
                    callback?.Invoke();
                });
        }

        private void KillSequence()
        {
            this.sequence?.Kill();
            this.sequence = null;
        }

        private void SetContent(float alpha, bool interactive)
        {
            foreach (var group in this.content)
            {
                if (group == null) continue;
                group.alpha          = alpha;
                group.blocksRaycasts = interactive;
            }
        }
    }
}
