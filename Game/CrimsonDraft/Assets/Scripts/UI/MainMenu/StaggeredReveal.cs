#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Shows a column of items one after another, top to bottom, every time this object is enabled:
    /// each starts invisible and fades in a beat after the one above it. With <see cref="waitFor"/>
    /// set, the first reveal waits until that screen has finished opening (the Settings frame), so the
    /// items don't appear before the frame they sit in. Items are the CanvasGroups of the buttons,
    /// in order.
    /// </summary>
    public sealed class StaggeredReveal : MonoBehaviour
    {
        [Tooltip("De arriba hacia abajo.")]
        [SerializeField] private CanvasGroup[] items = System.Array.Empty<CanvasGroup>();
        [SerializeField] private float stagger      = 0.22f;
        [SerializeField] private float fadeDuration = 0.3f;
        [Tooltip("Pantalla cuya apertura hay que esperar antes de empezar. Opcional.")]
        [SerializeField] private ScreenPowerOn? waitFor;

        private bool subscribed;

        private void OnEnable()
        {
            foreach (var item in this.items)
            {
                item.alpha          = 0f;
                item.blocksRaycasts = false;
            }

            if (this.waitFor != null && !this.waitFor.IsOpen)
            {
                this.waitFor.Opened += Begin;
                this.subscribed = true;
                return;
            }

            Begin();
        }

        private void OnDisable()
        {
            Unsubscribe();
            DOTween.Kill(this);

            // Leave everything usable for whoever shows this again without going through OnEnable.
            foreach (var item in this.items)
            {
                if (item == null) continue;
                item.alpha          = 1f;
                item.blocksRaycasts = true;
            }
        }

        private void Unsubscribe()
        {
            if (!this.subscribed || this.waitFor == null) return;
            this.waitFor.Opened -= Begin;
            this.subscribed = false;
        }

        private void Begin()
        {
            Unsubscribe();

            for (int i = 0; i < this.items.Length; i++)
            {
                var item = this.items[i];
                DOVirtual.DelayedCall(i * this.stagger, () =>
                {
                    if (item == null) return;
                    item.blocksRaycasts = true;
                    item.DOFade(1f, this.fadeDuration).SetTarget(this).SetUpdate(true);
                }, ignoreTimeScale: true).SetTarget(this);
            }
        }
    }
}
