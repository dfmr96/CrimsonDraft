using UnityEngine;

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "Scene Layer", menuName = "Horror Engine/Design/Layer")]
    public class SceneLayer : ScriptableObject
    {
        public string Name;
        public Color SceneFolderColor = new Color(.15f, .15f, .15f, 1f);
    }
}