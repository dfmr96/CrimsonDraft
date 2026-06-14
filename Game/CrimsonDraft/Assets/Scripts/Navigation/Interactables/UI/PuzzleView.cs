#nullable enable

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class PuzzleView : MonoBehaviour, INavigablePuzzle
    {
        [SerializeField] private Image[]    buttonImages    = null!;
        [SerializeField] private Image      leverImage      = null!;
        [SerializeField] private Image      selectorImage   = null!;
        [SerializeField] private TMP_Text   decimalText     = null!;
        [SerializeField] private Sprite     onSprite        = null!;
        [SerializeField] private Sprite     offSprite       = null!;
        [SerializeField] private GameObject leverOnObject   = null!;
        [SerializeField] private GameObject leverOffObject  = null!;
        [SerializeField] private Image      resultImage     = null!;
        [SerializeField] private Sprite     resultOffSprite = null!;
        [SerializeField] private Sprite     resultWrongSprite  = null!;
        [SerializeField] private Sprite     resultCorrectSprite = null!;
        [Range(0, 31)]
        [SerializeField] private int        expectedValue;
        [SerializeField] private float      leverResetDelay  = 1f;
        [SerializeField] private float      solvedCloseDelay = 1.5f;

        public Action? OnSolved { get; set; }

        private RectTransform[] navigables = null!;
        private readonly bool[] states = new bool[5];
        private int       currentIndex;
        private Coroutine? resetCoroutine;

        private void Start()
        {
            this.navigables = new RectTransform[this.buttonImages.Length + 1];
            for (int i = 0; i < this.buttonImages.Length; i++)
                this.navigables[i] = this.buttonImages[i].rectTransform;
            this.navigables[this.buttonImages.Length] = this.leverImage.rectTransform;

            SetLever(false);
            this.resultImage.sprite = this.resultOffSprite;
            Canvas.ForceUpdateCanvases();
            Refresh();
        }

        public void MoveLeft()
        {
            if (this.currentIndex <= 0) return;
            this.currentIndex--;
            Refresh();
        }

        public void MoveRight()
        {
            if (this.currentIndex >= this.navigables.Length - 1) return;
            this.currentIndex++;
            Refresh();
        }

        public void MoveUp()   { }
        public void MoveDown() { }

        public void Toggle()
        {
            if (this.currentIndex < this.buttonImages.Length)
            {
                this.states[this.currentIndex] = !this.states[this.currentIndex];
                Refresh();
            }
            else
            {
                CheckSolution();
            }
        }

        private void CheckSolution()
        {
            if (this.resetCoroutine != null)
            {
                StopCoroutine(this.resetCoroutine);
                this.resetCoroutine = null;
            }

            SetLever(true);

            if (ComputeDecimal() == this.expectedValue)
            {
                this.resultImage.sprite = this.resultCorrectSprite;
                StartCoroutine(CloseAfterDelay());
            }
            else
            {
                this.resultImage.sprite = this.resultWrongSprite;
                this.resetCoroutine = StartCoroutine(ResetAfterDelay());
            }
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSecondsRealtime(this.leverResetDelay);
            SetLever(false);
            this.resultImage.sprite = this.resultOffSprite;
            this.resetCoroutine = null;
        }

        private IEnumerator CloseAfterDelay()
        {
            yield return new WaitForSecondsRealtime(this.solvedCloseDelay);
            OnSolved?.Invoke();
        }

        private void SetLever(bool active)
        {
            this.leverOnObject.SetActive(active);
            this.leverOffObject.SetActive(!active);
        }

        private void Refresh()
        {
            for (int i = 0; i < this.buttonImages.Length; i++)
                this.buttonImages[i].sprite = this.states[i] ? this.onSprite : this.offSprite;

            SnapSelectorTo(this.navigables[this.currentIndex]);

            this.decimalText.text = ComputeDecimal().ToString();
        }

        private void SnapSelectorTo(RectTransform target)
        {
            var selRect = this.selectorImage.rectTransform;
            selRect.position = target.position;
            selRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, target.rect.width);
            selRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   target.rect.height);
        }

        private int ComputeDecimal()
        {
            int value = 0;
            for (int i = 0; i < this.states.Length; i++)
                if (this.states[i])
                    value += 1 << (this.states.Length - 1 - i);
            return value;
        }
    }
}
