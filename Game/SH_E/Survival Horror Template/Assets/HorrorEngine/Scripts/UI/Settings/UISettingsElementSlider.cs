using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UISettingsElementSlider : UISettingsElement
    {
        public TMPro.TextMeshProUGUI ValueText;
        public Slider ValueSlider;

        // ---------------------------------------------------------

        public void ChangeValue(float value)
        {
            SettingsElementSliderContent sliderContent = m_Content as SettingsElementSliderContent;
            if (ValueText)
                ValueText.text = string.Format(sliderContent.TextFormat, value);

            ValueSlider.value = value;
            OnValueChanged?.Invoke(value.ToString());
        }

        // ---------------------------------------------------------

        public override void Fill(SettingsElementContent content)
        {
            base.Fill(content);

            float fVal = content.GetAsFloat();

            SettingsElementSliderContent sliderContent = content as SettingsElementSliderContent;
            ValueSlider.minValue = sliderContent.MinSliderValue;
            ValueSlider.maxValue = sliderContent.MaxSliderValue;
            ValueSlider.wholeNumbers = sliderContent.WholeNumbers;
            ValueSlider.value = fVal;

            if (ValueText)
                ValueText.text = string.Format(sliderContent.TextFormat, fVal);
        }
    }
}