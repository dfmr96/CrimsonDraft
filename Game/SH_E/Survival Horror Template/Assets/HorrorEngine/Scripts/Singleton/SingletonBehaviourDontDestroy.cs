using UnityEngine;

namespace HorrorEngine
{
    public class SingletonBehaviourDontDestroy<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T m_Instance;

        public static bool Exists { get { return m_Instance != null; } }

        public static T Instance
        {
            get
            {
                if (!m_Instance)
                {
                    m_Instance = FindAndRemoveDuplicated();
                    if (!m_Instance)
                    {
                        Debug.Log("Couldn´t find instance of : " + typeof(T) + ", creating a new one");
                        GameObject go = new GameObject(typeof(T).Name, typeof(T));
                        m_Instance = go.GetComponent<T>();
                    }

                    DontDestroyOnLoad(m_Instance);
                }
                return m_Instance;
            }
        }

        private static T FindAndRemoveDuplicated()
        {
            T[] managers = FindObjectsByType(typeof(T), FindObjectsSortMode.None) as T[];
            if (managers != null)
            {
                if (managers.Length > 1)
                {
                    Debug.LogWarning("There is more than one singleton " + typeof(T) + " in the scene");
                    for (int i = 1; i < managers.Length; i++)
                    {
                        T manager = managers[i];
                        Destroy(manager.gameObject);
                    }
                    return managers[0];
                }
                else if (managers.Length == 1)
                {
                    return managers[0];
                }
            }
            return null;
        }

        protected virtual void Awake()
        {
            if (Instance != this)
            {
                Debug.Log($"Duplicated SingletonBehaviourDontDestroy of type {typeof(T)}, object {name} will be destroyed");
                Destroy(gameObject);
            }
        }
    }
}