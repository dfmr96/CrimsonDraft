#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.UI.MainMenu
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuButtonSelectScale : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float scaleUpAmount = 1.15f;
        [SerializeField] private float duration       = 0.15f;
        [SerializeField] private Ease  ease           = Ease.OutBack;

        private RectTransform rectTransform = null!;
        private Vector3       baseScale;

        private void Awake()
        {
            this.rectTransform = (RectTransform)this.transform;
            this.baseScale = this.rectTransform.localScale;
        }

        public void OnSelect(BaseEventData eventData)   => Scale(this.baseScale * this.scaleUpAmount);
        public void OnDeselect(BaseEventData eventData) => Scale(this.baseScale);

        private void Scale(Vector3 targetScale)
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform
                .DOScale(targetScale, this.duration)
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .SetEase(this.ease);
        }

        private void OnDisable()
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform.localScale = this.baseScale;
        }
    }
}
