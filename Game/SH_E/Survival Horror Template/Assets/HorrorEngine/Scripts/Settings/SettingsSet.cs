using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "SettingsSet", menuName = "Horror Engine/Settings/Settings Set", order = -1)]
    public class SettingsSet : ScriptableObject
    {
        public SettingsElementContent[] Elements;
    }
}
