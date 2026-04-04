using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    public class SimpleSceneLoader : MonoBehaviour
    {
        [SerializeField] SceneReference m_SceneRef;

        public void LoadScene()
        {
            LoadScene(m_SceneRef);
        }

        public void LoadScene(SceneReference scene)
        {
            SceneManager.LoadScene(scene.Name);
        }
    }
}