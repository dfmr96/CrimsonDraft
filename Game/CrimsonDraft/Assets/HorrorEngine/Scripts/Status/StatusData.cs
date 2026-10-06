using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    [CreateAssetMenu(fileName = "StatusData", menuName = "Horror Engine/Status/StatusData")]
    public class StatusData : Register
    {
        public UIPlayerStatusEntry[] UIEntries;

#if UNITY_EDITOR
        public void EditorOnly_MigrateUnlocalizedData()
        {
            bool dirty = false;

            foreach(var entry in UIEntries)
                {
                if (!string.IsNullOrEmpty(entry.Text_DEPRECATED))
                {
                    entry.Text.Unlocalized = entry.Text_DEPRECATED;
                    entry.Text_DEPRECATED = "";
                    dirty = true;
                }
            }

            if (dirty)
            {
                var context = this;
                EditorApplication.delayCall += () => { EditorUtility.SetDirty(context); };
            }

        }
#endif
    }

    [System.Serializable]
    public class UIPlayerStatusEntry
    {
        public float FromHealth;
        public Color Color;
        public float Tiling = 1f;
        public float Interval = 1f;
        public float Speed = 1f;
        public LocalizableText Text;

        // TODO - Remove eventually
        [FormerlySerializedAs("Text")]
        [HideInInspector]
        public string Text_DEPRECATED;
        // ---------------
    }
}
