using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorEngine
{
    [Serializable]
    public struct SceneTransitionParams
    {
        public SceneReference LoadScene;
        public LoadSceneMode LoadMode;
        public SceneReference UnloadScene;
        public CharacterData SpawnCharacter;
    }

    public class SceneTransition : MonoBehaviour
    {
        public bool FadeOut = true;
        [ShowIf(nameof(FadeOut))]
        public float FadeOutDuration = 1f;

        public bool FadeIn = true;
        [ShowIf(nameof(FadeIn))]
        public float FadeInDuration = 1f;

        [SerializeField] private SceneReference m_LoadScene;
        [SerializeField] private LoadSceneMode m_LoadMode;
        [SerializeField] private SceneReference m_UnloadScene;
        [SerializeField] private CharacterData m_SpawnCharacter;
        [SerializeField] string m_SpawnPointId;

        private CharacterData m_CharacterOverride;

        // --------------------------------------------------------------------

        public void UpdateTransition(SceneTransitionParams transitionParams)
        {
            m_LoadScene = transitionParams.LoadScene;
            m_LoadMode = transitionParams.LoadMode;
            m_UnloadScene = transitionParams.UnloadScene;
            m_SpawnCharacter = transitionParams.SpawnCharacter;
        }

        // --------------------------------------------------------------------

        public void TriggerTransitionWithNewCharacter(CharacterData characterToSpawn)
        {
            m_CharacterOverride = characterToSpawn;
            TriggerTransition();
        }

        // --------------------------------------------------------------------

        // Auxiliary method to be called from UI with no character
        public void TriggerTransition()
        {
            SceneTransitionController.Instance.Trigger(this);
        }


        // --------------------------------------------------------------------

        public IEnumerator StartSceneTransition()
        {
            MessageBuffer<SceneTransitionPreMessage>.Dispatch();

            AsyncOperation asyncUnLoad = null;
            AsyncOperation asyncLoad = null;

            // Load new scene async
            if (m_LoadScene && !m_LoadScene.IsLoaded())
            {
                asyncLoad = SceneManager.LoadSceneAsync(m_LoadScene.Name, m_LoadMode);
                while (!asyncLoad.isDone)
                {
                    yield return null;
                }
            }

            // Unload old scene async
            if (m_UnloadScene && m_UnloadScene.IsLoaded())
            {
                asyncUnLoad = SceneManager.UnloadSceneAsync(m_UnloadScene.Name);
                while (!asyncUnLoad.isDone)
                {
                    yield return null;
                }
            }

            // This needs to happen before player teleportation or the object state will override the player transform
            MessageBuffer<SceneTransitionPostMessage>.Dispatch();

            if (m_CharacterOverride || m_SpawnCharacter)
            {
                PlayerSpawnPoint spawnPoint = null;
                if (string.IsNullOrEmpty(m_SpawnPointId))
                {
                    spawnPoint = FindFirstObjectByType<PlayerSpawnPoint>();
                    Debug.Assert(spawnPoint, "Default player spawn point not found");
                }
                else
                {
                    var spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsSortMode.None);
                    foreach(var spawnP in spawnPoints)
                    {
                        if (spawnP.SpawnId == m_SpawnPointId)
                        {
                            spawnPoint = spawnP;
                            break;
                        }
                    }
                    Debug.Assert(spawnPoint, $"Player spawn point with id {m_SpawnPointId} not found");
                }
                
                GameManager.Instance.SwitchCharacter(m_CharacterOverride ? m_CharacterOverride : m_SpawnCharacter, spawnPoint.transform);
                m_CharacterOverride = null;
            }

        }

    }
}