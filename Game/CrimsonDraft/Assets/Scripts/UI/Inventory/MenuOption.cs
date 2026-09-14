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
        [SerializeField] private GameObject? selectedImage; // shown only while this option is the current selection

        [Header("Colors")]
        [SerializeField] private Color colorNormal   = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color colorSelected = new Color(1f, 1f, 0f, 0.9f);
        [SerializeField] private Color colorDisabled = new Color(0.4f, 0.4f, 0.4f, 0f);

        [Header("Selection Scale")]
        [SerializeField] private float selectedScale = 1.1f;

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
                // selectedImage is optional -- `?.` doesn't safely no-op on an unassigned
                // UnityEngine.Object reference (it throws UnassignedReferenceException
                // instead of skipping the call the way it does for a real null), so this
                // must use an explicit null check.
                if (this.selectedImage != null) this.selectedImage.SetActive(false);
                this.transform.localScale = Vector3.one;
                return;
            }

            background.color = selected ? colorSelected : colorNormal;
            label.color      = Color.white;
            if (this.selectedImage != null) this.selectedImage.SetActive(selected);
            this.transform.localScale = selected ? Vector3.one * this.selectedScale : Vector3.one;
        }
    }
}
