#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    // Tints the slider's fill graphic while it holds UI selection (gamepad/keyboard
    // navigation focus, matching InputSystemUIInputModule's Navigate/Submit), mirroring
    // the same selection color used elsewhere (e.g. GridCursor's colorHoldTint).
    public sealed class SliderFillSelectionTint : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Image fillImage = null!;
        [SerializeField] private Color selectedColor = new(0.6039216f, 0.62352943f, 0.36078432f, 1f);

        private Color normalColor;

        private void Awake() => this.normalColor = this.fillImage.color;

        void ISelectHandler.OnSelect(BaseEventData eventData) => this.fillImage.color = this.selectedColor;
        void IDeselectHandler.OnDeselect(BaseEventData eventData) => this.fillImage.color = this.normalColor;
    }
}
