#nullable enable

using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class MenuButtonSelectScale : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float scaleUpAmount = 1.15f;
        [SerializeField] private float duration       = 0.15f;
        [SerializeField] private Ease  ease           = Ease.OutBack;

        [Tooltip("Imagen que se tiñe al seleccionar. Vacio = la Image de este mismo objeto.")]
        [SerializeField] private Image? targetImage;
        [Tooltip("Apagado cuando una MenuSelectionBar ya marca la seleccion y la Image del boton es solo un ancla invisible.")]
        [SerializeField] private bool   tintImageOnSelect = true;
        [SerializeField] private Color  selectedColor = Color.white;

        private RectTransform rectTransform = null!;
        private Vector3       baseScale;
        private Color         baseColor;

        private void Awake()
        {
            this.rectTransform = (RectTransform)this.transform;
            this.baseScale = this.rectTransform.localScale;

            if (!this.tintImageOnSelect) this.targetImage = null;
            else if (this.targetImage == null) this.targetImage = GetComponent<Image>();
            if (this.targetImage != null) this.baseColor = this.targetImage.color;
        }

        public void OnSelect(BaseEventData eventData)   => Apply(this.baseScale * this.scaleUpAmount, this.selectedColor);
        public void OnDeselect(BaseEventData eventData) => Apply(this.baseScale, this.baseColor);

        private void Apply(Vector3 targetScale, Color targetColor)
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform
                .DOScale(targetScale, this.duration)
                .SetTarget(this.rectTransform)
                .SetUpdate(true)
                .SetEase(this.ease);

            // Color tween is a separate tween on the same target; both die together in Kill above.
            if (this.targetImage != null)
                this.targetImage
                    .DOColor(targetColor, this.duration)
                    .SetTarget(this.rectTransform)
                    .SetUpdate(true);
        }

        private void OnDisable()
        {
            DOTween.Kill(this.rectTransform);
            this.rectTransform.localScale = this.baseScale;
            if (this.targetImage != null) this.targetImage.color = this.baseColor;
        }
    }
}
