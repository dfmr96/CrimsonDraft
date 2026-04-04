using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsVSync", menuName = "Horror Engine/Settings/VSync")]
    public class GraphicSettingsElementVSync : SettingsElementToggleContent
    {
        public override void Apply()
        {
            QualitySettings.vSyncCount = GetAsBool() ? 1 : 0;
        }
    }
}