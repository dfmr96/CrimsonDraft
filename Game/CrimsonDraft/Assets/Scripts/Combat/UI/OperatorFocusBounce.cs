#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    // Nudges an operator card when roster browse focus lands on/leaves it — a quick
    // upward kick that overshoots then settles into a slightly raised resting position,
    // so the currently browsed card reads clearly against its neighbors. Losing focus
    // eases back down to the card's original layout position.
    public sealed class OperatorFocusBounce : MonoBehaviour
    {
        [SerializeField] private RectTransform target = null!;
        [SerializeField] private float liftAmount     = 6f;
        [SerializeField] private float focusDuration  = 0.28f;
        [SerializeField] private float focusOvershoot = 1.6f;
        [SerializeField] private float unfocusDuration = 0.18f;

        private float baseY;
        private bool  initialized;

        public void SetFocused(bool focused)
        {
            this.EnsureInitialized();
            this.target.DOKill();

            if (focused)
                this.target.DOAnchorPosY(this.baseY + this.liftAmount, this.focusDuration)
                    .SetEase(Ease.OutBack, this.focusOvershoot);
            else
                this.target.DOAnchorPosY(this.baseY, this.unfocusDuration)
                    .SetEase(Ease.OutQuad);
        }

        private void EnsureInitialized()
        {
            if (this.initialized) return;
            if (this.target == null) this.target = (RectTransform)this.transform;
            this.baseY = this.target.anchoredPosition.y;
            this.initialized = true;
        }

        private void OnDisable() => this.target?.DOKill();
    }
}
