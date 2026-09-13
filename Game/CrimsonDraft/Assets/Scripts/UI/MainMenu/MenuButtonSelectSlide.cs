#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.UI.MainMenu
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuButtonSelectSlide : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float slideOffsetX = -40f;
        [SerializeField] private float duration     = 0.15f;
        [SerializeField] private Ease  ease          = Ease.OutQuad;

        private RectTransform rectTransform = null!;
        private float basePositionX;

        private void Awake()
        {
            this.rectTransform = (RectTransform)this.transform;
            this.basePositionX = this.rectTransform.anchoredPosition.x;
        }

        public void OnSelect(BaseEventData eventData)   => Slide(this.basePositionX + this.slideOffsetX);
        public void OnDeselect(BaseEventData eventData) => Slide(this.basePositionX);

        private void Slide(float targetX)
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform
                .DOAnchorPosX(targetX, this.duration)
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .SetEase(this.ease);
        }

        private void OnDisable()
        {
            DOTween.Kill(this.rectTransform);
            var pos = this.rectTransform.anchoredPosition;
            pos.x = this.basePositionX;
            this.rectTransform.anchoredPosition = pos;
        }
    }
}
