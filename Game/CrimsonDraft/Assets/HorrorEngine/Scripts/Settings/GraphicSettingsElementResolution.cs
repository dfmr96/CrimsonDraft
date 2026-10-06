using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsResolution", menuName = "Horror Engine/Settings/Resolution")]
    public class GraphicSettingsElementResolution : SettingsElementComboContent
    {
        public Vector2Int[] Options;

        public override void Apply()
        {
            if (SettingsManager.Instance.Get(this, out string outVal))
            {
                int index = GetItemIndex(outVal);
                if (index >= 0)
                {
                    Vector2Int res = Options[index];
                    Screen.SetResolution(res.x, res.y, Screen.fullScreen);
                }
            }
        }

        public override string GetDefaultValue()
        {
            return Screen.width + "x" + Screen.height;
        }

        public override int GetItemCount()
        {
            return Options.Length;
        }

        public override string GetItemName(int index)
        {
            Vector2Int item = Options[index];
            return item.x + "x" + item.y;
        }
    }
}