#nullable enable

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.UI
{
    // Scales this RectTransform up while it holds UI selection (gamepad/keyboard
    // navigation focus), mirroring the same selection feedback used elsewhere
    // (e.g. SliderFillSelectionTint's color swap). Also punches the scale down and
    // back up on activation (click or Submit) so pressing a control reads as a
    // press even under Time.timeScale == 0 (the pause menu) -- the punch runs on
    // unscaled time for that reason.
    // On a Toggle it also stays enlarged (onScale) while isOn, so the active option of a
    // ToggleGroup (e.g. Classic/Modern) keeps reading as chosen after focus moves away,
    // and dims to offAlpha (via a CanvasGroup on the same object) while isOn is false.
    public sealed class SelectionScalePunch : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private float selectedScale = 1.1f;
        [SerializeField] private float pressedScale  = 0.9f;
        [SerializeField] private float pressDuration = 0.08f;
        [SerializeField] private float onScale       = 1.2f;
        [SerializeField, Range(0f, 1f)] private float offAlpha = 1f;

        private Vector3   normalScale;
        private bool      isSelected;
        private Coroutine? pressRoutine;
        private Toggle?   toggle;
        private CanvasGroup? canvasGroup;

        private Vector3 RestingScale =>
            this.normalScale
            * (this.isSelected ? this.selectedScale : 1f)
            * (this.toggle != null && this.toggle.isOn ? this.onScale : 1f);

        private void Awake()
        {
            this.normalScale = this.transform.localScale;
            this.toggle      = GetComponent<Toggle>();
            this.canvasGroup = GetComponent<CanvasGroup>();
            if (this.toggle != null) this.toggle.onValueChanged.AddListener(_ => RefreshVisuals());
        }

        // SetIsOnWithoutNotify (used when a menu restores saved state) skips onValueChanged,
        // so re-read isOn whenever the control is shown. Disabling kills a running punch
        // coroutine without clearing its handle, so drop it here too.
        private void OnEnable()
        {
            this.pressRoutine = null;
            RefreshVisuals();
        }

        void ISelectHandler.OnSelect(BaseEventData eventData)
        {
            this.isSelected = true;
            RefreshVisuals();
        }

        void IDeselectHandler.OnDeselect(BaseEventData eventData)
        {
            this.isSelected = false;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (this.pressRoutine == null) this.transform.localScale = RestingScale;
            if (this.toggle != null && this.canvasGroup != null)
                this.canvasGroup.alpha = this.toggle.isOn ? 1f : this.offAlpha;
        }

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData) => Punch();
        void ISubmitHandler.OnSubmit(BaseEventData eventData) => Punch();

        private void Punch()
        {
            if (this.pressRoutine != null) StopCoroutine(this.pressRoutine);
            this.pressRoutine = StartCoroutine(PunchRoutine());
        }

        private IEnumerator PunchRoutine()
        {
            Vector3 pressedTarget = this.normalScale * this.pressedScale;

            for (float t = 0f; t < this.pressDuration; t += Time.unscaledDeltaTime)
            {
                this.transform.localScale = Vector3.Lerp(RestingScale, pressedTarget, t / this.pressDuration);
                yield return null;
            }
            this.transform.localScale = pressedTarget;

            for (float t = 0f; t < this.pressDuration; t += Time.unscaledDeltaTime)
            {
                this.transform.localScale = Vector3.Lerp(pressedTarget, RestingScale, t / this.pressDuration);
                yield return null;
            }
            this.transform.localScale = RestingScale;
            this.pressRoutine = null;
        }
    }
}
