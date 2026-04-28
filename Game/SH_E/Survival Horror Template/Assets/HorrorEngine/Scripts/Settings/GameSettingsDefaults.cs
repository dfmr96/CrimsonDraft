using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "DefaultSettings", menuName = "Horror Engine/Settings/DefaultSettings", order = -5)]
    public class GameSettingsDefaults : ScriptableObject
    {
        public SettingsElementContent[] PersistentSettings;
    }
}
