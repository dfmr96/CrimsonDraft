using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "AudioSettingsMixerParam", menuName = "Horror Engine/Settings/AudioMixerParameter")]
    public class AudioSettingsElementMixerParameter : SettingsElementSliderContent
    {
        public float MinParamValue;
        public float NeutralParamValue;
        public float MaxParamValue;
        public AudioMixer Mixer;
        public string MixerParameter;

        public override void Apply()
        {
            float fVal = GetAsFloat();
            float mixerVal = 0;
            float halfSliderValue = (MaxSliderValue + MinSliderValue) * 0.5f;
            if (fVal < halfSliderValue)
            {
                mixerVal = MathUtils.Map(fVal, MinSliderValue, halfSliderValue, MinParamValue, NeutralParamValue);
            }
            else
            {
                mixerVal = MathUtils.Map(fVal, halfSliderValue, MaxSliderValue, NeutralParamValue, MaxParamValue);
            }
            Mixer.SetFloat(MixerParameter, mixerVal);
        }

    }
}