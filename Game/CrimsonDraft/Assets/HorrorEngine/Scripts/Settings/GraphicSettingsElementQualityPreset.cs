using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsQualityPreset", menuName = "Horror Engine/Settings/Quality Preset")]
    public class GraphicSettingsElementQualityPreset : SettingsElementComboContent
    {
        public override string GetDefaultValue()
        {
            return QualitySettings.GetQualityLevel().ToString();
        }

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                int index = GetItemIndex(outVal);
                if (index >= 0) 
                    QualitySettings.SetQualityLevel(index);
            }
        }

        public override string GetItemName(int index)
        {
            return QualitySettings.names[index];
        }

        public override int GetItemCount()
        {
            return QualitySettings.names.Length;
        }
    }
}