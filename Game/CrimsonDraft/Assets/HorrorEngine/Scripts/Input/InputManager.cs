using UnityEngine;

namespace HorrorEngine
{
    public class InputManager : SingletonBehaviourDontDestroy<InputManager>
    {
        [SerializeField] private GameObject[] m_RuntimePrefabs;

        protected override void Awake()
        {
            // Make a root object before it's marked as DontDestroyOnLoad
            transform.SetParent(null); 

            base.Awake();

            if (Instance == this)
            {
                foreach (var prefab in m_RuntimePrefabs)
                {
                    var runtime = Instantiate(prefab);
                    runtime.transform.SetParent(transform);
                }
            }
        }
    }
}