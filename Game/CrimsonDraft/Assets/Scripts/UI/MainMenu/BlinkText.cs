#nullable enable

using TMPro;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class BlinkText : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label      = null!;
        [SerializeField] private float            blinkSpeed = 2f;
        [SerializeField] private float            minAlpha   = 0.2f;
        [SerializeField] private float            maxAlpha   = 1f;

        private void Update()
        {
            float t     = (Mathf.Sin(Time.unscaledTime * this.blinkSpeed) + 1f) * 0.5f;
            var   color = this.label.color;
            color.a     = Mathf.Lerp(this.minAlpha, this.maxAlpha, t);
            this.label.color = color;
        }
    }
}
