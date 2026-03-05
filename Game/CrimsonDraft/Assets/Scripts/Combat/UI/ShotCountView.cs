#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class ShotCountView : MonoBehaviour, IShotCountView
    {
        [SerializeField] private TextMeshProUGUI valueLabel = null!;
        [SerializeField] private Vector2 offset = Vector2.zero;

        private int value = 1;
        private int maxValue = 1;

        public int Value => this.value;
        public int MaxValue => this.maxValue;

        public void Show(RectTransform commandPanelRect, int initial, int max)
        {
            this.maxValue = Mathf.Max(1, max);
            this.value = Mathf.Clamp(initial, 1, this.maxValue);

            var panel = (RectTransform)this.transform;
            panel.localPosition = commandPanelRect.localPosition + new Vector3(this.offset.x, this.offset.y, 0f);

            this.RefreshLabel();
            this.gameObject.SetActive(true);
        }

        public void Hide() => this.gameObject.SetActive(false);

        public void Increment()
        {
            this.value = Mathf.Min(this.maxValue, this.value + 1);
            this.RefreshLabel();
        }

        public void Decrement()
        {
            this.value = Mathf.Max(1, this.value - 1);
            this.RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (this.valueLabel != null)
                this.valueLabel.text = this.value.ToString();
        }
    }
}
