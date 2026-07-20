#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string newGameSceneName = "Deck_B_Development";

        [SerializeField] private Button newGameButton  = null!;
        [SerializeField] private Button loadGameButton = null!;
        [SerializeField] private Button exitButton     = null!;

        private void Awake()
        {
            this.newGameButton.onClick.AddListener(OnNewGameClicked);
            this.exitButton.onClick.AddListener(OnExitClicked);

            // Not implemented yet — keep visible but non-functional.
            this.loadGameButton.interactable = false;
        }

        private void OnNewGameClicked()
        {
            SceneManager.LoadScene(this.newGameSceneName, LoadSceneMode.Single);
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
