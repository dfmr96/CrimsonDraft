using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HorrorEngine
{
    public class UIBrightnessCalibration : MonoBehaviour
    {
        [SerializeField] GraphicSettingsElementBrightness m_BrightnessSetting;
        [SerializeField] Slider m_Slider;

        public UnityEvent OnAlreadyCalibrated;

        void Start()
        {
            string savedInPlayerPrefs = m_BrightnessSetting.GetPlayerPrefsValue();
            float defaultValue = m_BrightnessSetting.GetAsFloat();
            if (!string.IsNullOrEmpty(savedInPlayerPrefs))
            {
                defaultValue = float.Parse(savedInPlayerPrefs);
                OnAlreadyCalibrated?.Invoke();
            }

            m_Slider.minValue = m_BrightnessSetting.MinSliderValue;
            m_Slider.maxValue = m_BrightnessSetting.MaxSliderValue;
            m_Slider.value = defaultValue;
            m_Slider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        public void Update()
        {
            EventSystemUtils.SelectDefaultOnLostFocus(m_Slider.gameObject);
        }

        private void OnSliderValueChanged(float brightness)
        {
            SettingsManager.Instance.Set(m_BrightnessSetting, brightness.ToString());
            m_BrightnessSetting.Apply();
            m_BrightnessSetting.SaveInPlayerPrefs();
        }
    }
}