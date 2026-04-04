using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorEngine
{
    [RequireComponent(typeof(Volume))]
    public class BrightnessInjector : MonoBehaviour
    {
        [SerializeField] GraphicSettingsElementBrightness m_BrigthnessSetting;

        private ColorAdjustments m_ColorAdjustments;
        private Volume m_Volume;
        private float m_Offset;

        private void Awake()
        {
            m_Volume = GetComponent<Volume>();
            MessageBuffer<BrightnessSettingChangedMessage>.Subscribe(OnBrightnessChanged);
        }

        private void OnDestroy()
        {
            MessageBuffer<BrightnessSettingChangedMessage>.Unsubscribe(OnBrightnessChanged);
        }

        private void OnBrightnessChanged(BrightnessSettingChangedMessage ev)
        {
            ApplyBrightness();
        }

        void Start()
        {
            // 1. Try to find the Color Adjustments in the Volume Profile
            if (m_Volume.profile.TryGet(out m_ColorAdjustments))
            {
                if (m_ColorAdjustments != null)
                {
                    if (m_ColorAdjustments.postExposure.overrideState)
                    {
                        m_Offset = m_ColorAdjustments.postExposure.value;
                    }
                }
                ApplyBrightness();
            }
            else
            {
                Debug.LogError("Color Adjustments not found on the Global Volume Profile!");
            }
        }

        public void ApplyBrightness()
        {
            float brightness = m_BrigthnessSetting.GetAsFloat();
            if (m_ColorAdjustments != null)
            {
                m_ColorAdjustments.postExposure.value = brightness + m_Offset;
                m_ColorAdjustments.postExposure.overrideState = true;
            }
        }
    }
}