using System;
using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "GraphicSettingsResolution", menuName = "Horror Engine/Settings/Resolution")]
    public class GraphicSettingsElementResolution : SettingsElementComboContent
    {
        public GraphicSettingsElementFullScreen FullScreenSetting;
        public Vector2Int[] Options;

        public override void Apply()
        {
            int index = GetItemIndex(GetAsString());
            if (index >= 0)
            {
                Vector2Int res = Options[index];
                bool fullScreen = FullScreenSetting.GetAsBool();
                Screen.SetResolution(res.x, res.y, fullScreen);
            }
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