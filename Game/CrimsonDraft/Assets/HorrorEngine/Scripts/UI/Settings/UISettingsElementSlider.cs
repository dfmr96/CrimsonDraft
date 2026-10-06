using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UISettingsElementSlider : UISettingsElement
    {
        public TMPro.TextMeshProUGUI ValueText;
        public string Format;
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

            string outVal;
            if (!SettingsManager.Instance.Get(content, out outVal))
            {
                outVal = content.GetDefaultValue();
            }

            SettingsElementSliderContent sliderContent = content as SettingsElementSliderContent;
            ValueSlider.minValue = sliderContent.MinSliderValue;
            ValueSlider.maxValue = sliderContent.MaxSliderValue;
            ValueSlider.wholeNumbers = sliderContent.WholeNumbers;
            ValueSlider.value = (float)Convert.ToDouble(outVal);

            if (ValueText)
                ValueText.text = string.Format(sliderContent.TextFormat, outVal);
        }
    }
}