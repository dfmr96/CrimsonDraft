#nullable enable

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Navigation.Interactables.UI
{
    public sealed class MultiGroupPuzzleView : MonoBehaviour, INavigablePuzzle
    {
        [Serializable]
        public sealed class GroupPanel
        {
            [SerializeField] public Image[]  buttonImages  = null!;
            [SerializeField] public TMP_Text decimalText   = null!;
            [SerializeField] public Image    resultImage   = null!;
            [Range(0, 15)]
            [SerializeField] public int      expectedValue;

            [NonSerialized] public bool[] States = null!;

            public int ComputeDecimal()
            {
                int v = 0;
                for (int i = 0; i < States.Length; i++)
                    if (States[i]) v += 1 << (States.Length - 1 - i);
                return v;
            }

            public bool IsCorrect() => ComputeDecimal() == expectedValue;
        }

        [SerializeField] private GroupPanel[] groups              = null!;
        [SerializeField] private Image        leverImage          = null!;
        [SerializeField] private Image        selectorImage       = null!;
        [SerializeField] private Sprite       onSprite            = null!;
        [SerializeField] private Sprite       offSprite           = null!;
        [SerializeField] private GameObject   leverOnObject       = null!;
        [SerializeField] private GameObject   leverOffObject      = null!;
        [SerializeField] private Sprite       resultOffSprite     = null!;
        [SerializeField] private Sprite       resultWrongSprite   = null!;
        [SerializeField] private Sprite       resultCorrectSprite = null!;
        [SerializeField] private float        leverResetDelay     = 1f;
        [SerializeField] private float        solvedCloseDelay    = 1.5f;

        public Action? OnSolved { get; set; }

        // currentGroup == groups.Length means the cursor is on the lever
        private int        currentGroup;
        private int        currentSwitch;
        private int        leverSourceGroup;
        private Coroutine? resetCoroutine;

        private bool AtLever => this.currentGroup == this.groups.Length;

        private void Awake()
        {
            for (int g = 0; g < this.groups.Length; g++)
                this.groups[g].States = new bool[this.groups[g].buttonImages.Length];
        }

        private IEnumerator Start()
        {
            SetLever(false);
            foreach (var group in this.groups)
                group.resultImage.sprite = this.resultOffSprite;
            for (int g = 0; g < this.groups.Length; g++)
                RefreshGroup(g);

            // Wait one frame so the Canvas layout system has computed real positions
            yield return null;
            Canvas.ForceUpdateCanvases();
            SnapSelector();
        }

        public void MoveLeft()
        {
            if (this.AtLever)
            {
                this.currentGroup = this.leverSourceGroup;
                SnapSelector();
                return;
            }
            if (this.currentSwitch <= 0) return;
            this.currentSwitch--;
            SnapSelector();
        }

        public void MoveRight()
        {
            if (this.AtLever) return;
            if (this.currentSwitch >= this.groups[this.currentGroup].buttonImages.Length - 1)
            {
                this.leverSourceGroup = this.currentGroup;
                this.currentGroup     = this.groups.Length;
                SnapSelector();
                return;
            }
            this.currentSwitch++;
            SnapSelector();
        }

        public void MoveUp()
        {
            if (this.currentGroup <= 0) return;
            this.currentGroup--;
            ClampSwitch();
            SnapSelector();
        }

        public void MoveDown()
        {
            if (this.AtLever) return;
            if (this.currentGroup == this.groups.Length - 1)
            {
                this.leverSourceGroup = this.currentGroup;
                this.currentGroup     = this.groups.Length;
            }
            else
            {
                this.currentGroup++;
                ClampSwitch();
            }
            SnapSelector();
        }

        public void Toggle()
        {
            if (this.AtLever)
            {
                CheckSolution();
                return;
            }

            var group = this.groups[this.currentGroup];
            group.States[this.currentSwitch] = !group.States[this.currentSwitch];
            group.resultImage.sprite = this.resultOffSprite;
            RefreshGroup(this.currentGroup);
            SnapSelector();
        }

        private void ClampSwitch()
        {
            if (this.AtLever) return;
            int max = this.groups[this.currentGroup].buttonImages.Length - 1;
            if (this.currentSwitch > max) this.currentSwitch = max;
        }

        private void CheckSolution()
        {
            if (this.resetCoroutine != null)
            {
                StopCoroutine(this.resetCoroutine);
                this.resetCoroutine = null;
            }

            SetLever(true);

            bool allCorrect = true;
            foreach (var group in this.groups)
            {
                bool ok = group.IsCorrect();
                group.resultImage.sprite = ok ? this.resultCorrectSprite : this.resultWrongSprite;
                if (!ok) allCorrect = false;
            }

            if (allCorrect)
                StartCoroutine(CloseAfterDelay());
            else
                this.resetCoroutine = StartCoroutine(ResetAfterDelay());
        }

        private IEnumerator ResetAfterDelay()
        {
            yield return new WaitForSecondsRealtime(this.leverResetDelay);
            SetLever(false);
            foreach (var group in this.groups)
                group.resultImage.sprite = this.resultOffSprite;
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
            for (int g = 0; g < this.groups.Length; g++)
                RefreshGroup(g);
            SnapSelector();
        }

        private void RefreshGroup(int g)
        {
            var panel = this.groups[g];
            for (int s = 0; s < panel.buttonImages.Length; s++)
            {
                if (panel.buttonImages[s] == null) continue;
                panel.buttonImages[s].sprite = panel.States[s] ? this.onSprite : this.offSprite;
            }
            panel.decimalText.text = panel.ComputeDecimal().ToString();
        }

        private void SnapSelector()
        {
            RectTransform target = this.AtLever
                ? this.leverImage.rectTransform
                : this.groups[this.currentGroup].buttonImages[this.currentSwitch].rectTransform;

            var selRect = this.selectorImage.rectTransform;
            selRect.position = target.position;
            selRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, target.rect.width);
            selRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   target.rect.height);
        }
    }
}
