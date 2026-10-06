using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsFullScreen", menuName = "Horror Engine/Settings/FullScreen")]
    public class GraphicSettingsElementFullScreen : SettingsElementToggleContent
    {
        public override string GetDefaultValue()
        {
            return Screen.fullScreen ? "1" : "0";
        }

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                Screen.fullScreen = outVal != "0";
                /*if (Screen.fullScreen) 
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                else 
                    Screen.fullScreenMode = FullScreenMode.Windowed;*/
            }
        }
    }
}