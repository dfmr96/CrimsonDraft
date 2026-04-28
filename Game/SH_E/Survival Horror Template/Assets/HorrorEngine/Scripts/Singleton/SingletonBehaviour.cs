using UnityEngine;

namespace HorrorEngine
{
    public class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        protected static T m_Instance;

        public static bool Exists { get { return m_Instance != null; } }

        /**
           Returns the instance of this singleton.
        */

        public static T Instance
        {
            get
            {
                if (m_Instance == null)
                {

#if !UNITY_2021_1_OR_NEWER
                    m_Instance = (T)FindObjectOfType(typeof(T)); // TODO - Replace with a custom find function
#else
                    m_Instance = (T)FindFirstObjectByType(typeof(T), FindObjectsInactive.Include);
#endif

                    Debug.Assert(m_Instance != null, "SingletonBehaviour couldn't be found");
                }

                return m_Instance;
            }
        }

        protected virtual void Awake()
        {
            if (m_Instance == null)
                m_Instance = this as T;
        }

    }
}