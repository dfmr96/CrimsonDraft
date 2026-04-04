using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public class SceneLayerObject : MonoBehaviour
    {
        public SceneLayer Layer;

#if UNITY_EDITOR
        [ContextMenu("Assign Layer Name")]
        private void AssignLayerName()
        {
            if (Layer)
                name = Layer.Name;

            EditorUtility.SetDirty(this);
        }
#endif

    }
}