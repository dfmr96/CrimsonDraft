#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// Scales a RectTransform up/down to show selection -- same look as MenuButtonSelectScale
    /// elsewhere in the main menu, but driven manually (SetSelected) instead of Unity's
    /// Selectable/EventSystem, for cursors owned by SaveSlotNavigator-style hand-rolled input.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class ManualSelectScale : MonoBehaviour
    {
        [SerializeField] private float selectedScaleUp = 1.15f;
        [SerializeField] private float duration = 0.15f;
        [SerializeField] private Ease ease = Ease.OutBack;

        [Header("Punch (press feedback)")]
        [SerializeField] private float pushDownAmount = 0.85f;
        [SerializeField] private float pushDuration    = 0.12f;

        private RectTransform rectTransform = null!;
        private Vector3 baseScale;
        private bool    selected;

        private void Awake()
        {
            this.rectTransform = (RectTransform)this.transform;
            this.baseScale = this.rectTransform.localScale;
        }

        public void SetSelected(bool selected)
        {
            this.selected = selected;
            DOTween.Kill(this.rectTransform);
            Vector3 target = selected ? this.baseScale * this.selectedScaleUp : this.baseScale;
            this.rectTransform
                .DOScale(target, this.duration)
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .SetEase(this.ease);
        }

        /// <summary>Brief squash-and-return on top of the current resting scale -- the "press" feel for a confirm/activate, independent of SetSelected's hover/focus scale.</summary>
        public void Punch()
        {
            DOTween.Kill(this.rectTransform);
            Vector3 restingScale = this.selected ? this.baseScale * this.selectedScaleUp : this.baseScale;
            this.rectTransform.localScale = restingScale;

            DOTween.Sequence()
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .Append(this.rectTransform.DOScale(restingScale * this.pushDownAmount, this.pushDuration * 0.4f).SetEase(Ease.OutQuad))
                .Append(this.rectTransform.DOScale(restingScale, this.pushDuration * 0.6f).SetEase(Ease.OutBack));
        }

        private void OnDisable() => DOTween.Kill(this.rectTransform);
    }
}
