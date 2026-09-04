#nullable enable

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CrimsonDraft.Navigation.UI
{
    // Scales this RectTransform up while it holds UI selection (gamepad/keyboard
    // navigation focus), mirroring the same selection feedback used elsewhere
    // (e.g. SliderFillSelectionTint's color swap). Also punches the scale down and
    // back up on activation (click or Submit) so pressing a control reads as a
    // press even under Time.timeScale == 0 (the pause menu) -- the punch runs on
    // unscaled time for that reason.
    public sealed class SelectionScalePunch : MonoBehaviour, ISelectHandler, IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private float selectedScale = 1.1f;
        [SerializeField] private float pressedScale  = 0.9f;
        [SerializeField] private float pressDuration = 0.08f;

        private Vector3   normalScale;
        private bool      isSelected;
        private Coroutine? pressRoutine;

        private void Awake() => this.normalScale = this.transform.localScale;

        void ISelectHandler.OnSelect(BaseEventData eventData)
        {
            this.isSelected = true;
            if (this.pressRoutine == null) this.transform.localScale = this.normalScale * this.selectedScale;
        }

        void IDeselectHandler.OnDeselect(BaseEventData eventData)
        {
            this.isSelected = false;
            if (this.pressRoutine == null) this.transform.localScale = this.normalScale;
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
            Vector3 restingScale = this.normalScale * (this.isSelected ? this.selectedScale : 1f);
            Vector3 pressedTarget = this.normalScale * this.pressedScale;

            for (float t = 0f; t < this.pressDuration; t += Time.unscaledDeltaTime)
            {
                this.transform.localScale = Vector3.Lerp(restingScale, pressedTarget, t / this.pressDuration);
                yield return null;
            }
            this.transform.localScale = pressedTarget;

            for (float t = 0f; t < this.pressDuration; t += Time.unscaledDeltaTime)
            {
                this.transform.localScale = Vector3.Lerp(pressedTarget, restingScale, t / this.pressDuration);
                yield return null;
            }
            this.transform.localScale = restingScale;
            this.pressRoutine = null;
        }
    }
}
