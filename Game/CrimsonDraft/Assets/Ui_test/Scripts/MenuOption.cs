using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CrimsonDraft.UI
{
    public class MenuOption : MonoBehaviour
    {
        public enum OptionType { Use, Inspect, Combine }

        [Header("References")]
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;

        [Header("Colors")]
        [SerializeField] private Color colorNormal   = new Color(1f, 1f, 1f, 0f);
        [SerializeField] private Color colorSelected = new Color(1f, 1f, 0f, 1f);
        [SerializeField] private Color colorDisabled = new Color(0.4f, 0.4f, 0.4f, 1f);

        [Header("Config")]
        [SerializeField] private OptionType optionType;

        private bool disabled;

        public OptionType Type => optionType;

        public void SetLabel(string text)
        {
            if (this.label != null) this.label.text = text;
        }

        public void SetDisabled(bool value)
        {
            disabled = value;
            if (!disabled)
                SetState(false);
        }

        public bool IsDisabled => disabled;

        public void SetState(bool selected)
        {
            if (disabled)
            {
                background.color = colorDisabled;
                label.color      = colorDisabled;
                return;
            }

            background.color = selected ? colorSelected : colorNormal;
            label.color      = selected ? Color.white    : Color.gray;
        }
    }
}
