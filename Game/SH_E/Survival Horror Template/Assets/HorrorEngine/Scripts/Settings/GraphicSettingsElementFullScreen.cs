using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsFullScreen", menuName = "Horror Engine/Settings/FullScreen")]
    public class GraphicSettingsElementFullScreen : SettingsElementToggleContent
    {
        public override void Apply()
        {
            bool fullScreen = GetAsBool(); 
            Screen.fullScreen = fullScreen;
            if (fullScreen)
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
            else
                Screen.fullScreenMode = FullScreenMode.Windowed;
        }
    }
}