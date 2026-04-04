using UnityEngine;

namespace HorrorEngine
{
    public class BrightnessSettingChangedMessage : BaseMessage
    {
        public float Brightness;
    }

    [CreateAssetMenu(fileName = "GraphicSettingsBrightness", menuName = "Horror Engine/Settings/Brightness")]
    public class GraphicSettingsElementBrightness : SettingsElementSliderContent
    {
        private BrightnessSettingChangedMessage m_ChangedMsg;

        public override void Apply()
        {
            float outVal = 0f;
            SettingsManager.Instance.GetFloat(this, out outVal);

            if (m_ChangedMsg == null)
            {
                m_ChangedMsg = new BrightnessSettingChangedMessage();
            }

            m_ChangedMsg.Brightness = outVal;

            MessageBuffer<BrightnessSettingChangedMessage>.Dispatch(m_ChangedMsg);
        }

    }
}