#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrimsonDraft.Infrastructure
{
    public static class Bootstrapper
    {
        private const string BootSceneName = "Boot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LoadBootScene()
        {
            if (SceneManager.GetSceneByName(BootSceneName).isLoaded)
                return;

            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Additive);
        }
    }
}
