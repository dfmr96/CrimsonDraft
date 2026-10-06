using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsVSync", menuName = "Horror Engine/Settings/VSync")]
    public class GraphicSettingsElementVSync : SettingsElementToggleContent
    {
        public override string GetDefaultValue()
        {
            return QualitySettings.vSyncCount.ToString();
        }

        public override void Apply()
        {
            if (SettingsManager.Instance.GetInt(this, out int outVal))
            {
                QualitySettings.vSyncCount = outVal;
            }
        }
    }
}