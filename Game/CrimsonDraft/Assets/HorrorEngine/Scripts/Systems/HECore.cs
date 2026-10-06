using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HorrorEngine
{
    public class HECore : MonoBehaviour
    {
        [FormerlySerializedAs("CorePrefabs")]
        [SerializeField] private HECorePrefabs m_CorePrefabs;
        [FormerlySerializedAs("DestroyInScenes")]
        [SerializeField] private SceneReference[] m_DestroyInScenes;

        // --------------------------------------------------------------------


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.Connected)
            {
                SetDefaultTransform();
            }
        }
#endif

        // --------------------------------------------------------------------

        private void SetDefaultTransform()
        {
            Debug.Assert(transform.parent == null, "HECore has to be placed at the top level of the hierarchy (without a parent)");
            transform.parent = null;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        // --------------------------------------------------------------------

        void Awake()
        {
            SetDefaultTransform();

            Debug.Assert(transform.parent == null, "HECore has to be placed at the top level of the hierarchy (without a parent)");

            // Ensure only one HECore exists
            HECore[] cores = FindObjectsByType<HECore>(FindObjectsSortMode.None);
            if (cores.Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);

            SceneManager.activeSceneChanged += OnSceneChange;
        }

        private void OnDestroy()
        {
            SceneManager.activeSceneChanged -= OnSceneChange;
        }

        // --------------------------------------------------------------------

        private void Start()
        {
            if (m_CorePrefabs)
            {
                InitCorePrefabs();
            }
            else
            {
                Debug.LogWarning("CorePrefabs has not been assigned. Are you using a deprecated HECommon? Use HECore prefab with a valid reference to CorePrefabs", gameObject);
            }
        }

        // --------------------------------------------------------------------

        private void InitCorePrefabs()
        {
            var mapped = m_CorePrefabs.GetMappedPrefabs();
            foreach (var mapEntry in mapped)
            {
                string path = mapEntry.Key;
                List<GameObject> objects = mapEntry.Value;

                Transform parent = string.IsNullOrEmpty(path) ? transform : transform.Find(path);
                if (!parent)
                {
                    GameObject parentGO = new GameObject(path);
                    parent = parentGO.transform;
                    parent.SetParent(transform);
                }

                foreach (var go in objects)
                {
                    if (!go)
                        continue;

                    Instantiate(go, parent);
                }
            }
        }

        // --------------------------------------------------------------------

        void OnSceneChange(Scene oldScene, Scene newScene)
        {
            foreach (var scene in m_DestroyInScenes)
            {
                if (newScene.name == scene.name)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
