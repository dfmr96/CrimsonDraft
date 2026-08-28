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

        private RectTransform rectTransform = null!;
        private Vector3 baseScale;

        private void Awake()
        {
            this.rectTransform = (RectTransform)this.transform;
            this.baseScale = this.rectTransform.localScale;
        }

        public void SetSelected(bool selected)
        {
            DOTween.Kill(this.rectTransform);
            Vector3 target = selected ? this.baseScale * this.selectedScaleUp : this.baseScale;
            this.rectTransform
                .DOScale(target, this.duration)
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .SetEase(this.ease);
        }

        private void OnDisable() => DOTween.Kill(this.rectTransform);
    }
}
