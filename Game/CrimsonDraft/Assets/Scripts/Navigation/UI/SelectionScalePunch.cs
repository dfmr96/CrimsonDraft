#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.Navigation.UI
{
    // Scales this RectTransform up while it holds UI selection (gamepad/keyboard
    // navigation focus), mirroring the same selection feedback used elsewhere
    // (e.g. SliderFillSelectionTint's color swap).
    public sealed class SelectionScalePunch : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float selectedScale = 1.1f;

        private Vector3 normalScale;

        private void Awake() => this.normalScale = this.transform.localScale;

        void ISelectHandler.OnSelect(BaseEventData eventData) => this.transform.localScale = this.normalScale * this.selectedScale;
        void IDeselectHandler.OnDeselect(BaseEventData eventData) => this.transform.localScale = this.normalScale;
    }
}
