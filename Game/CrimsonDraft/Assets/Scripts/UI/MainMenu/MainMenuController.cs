#nullable enable

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Infrastructure.Save;
using CrimsonDraft.Infrastructure.Save.UI;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string newGameSceneName = "Deck_B_Development";

        [SerializeField] private Button newGameButton  = null!;
        [SerializeField] private Button loadGameButton = null!;
        [SerializeField] private Button exitButton     = null!;
        [SerializeField] private SaveSlotListView loadSlotListView = null!;

        private ISaveGameService   saveGameService   = null!;
        private IGameStateResetter gameStateResetter = null!;

        [Inject]
        public void Construct(ISaveGameService saveGameService, IGameStateResetter gameStateResetter)
        {
            this.saveGameService   = saveGameService;
            this.gameStateResetter = gameStateResetter;

            this.newGameButton.onClick.AddListener(OnNewGameClicked);
            this.loadGameButton.onClick.AddListener(OnLoadGameClicked);
            this.exitButton.onClick.AddListener(OnExitClicked);
        }

        private void OnNewGameClicked()
        {
            this.gameStateResetter.ResetAll();
            SceneManager.LoadScene(this.newGameSceneName, LoadSceneMode.Single);
        }

        private void OnLoadGameClicked()
        {
            this.loadSlotListView.Show(this.saveGameService.ListSlotSummaries(), OnLoadSlotClicked);
        }

        private void OnLoadSlotClicked(SaveSlotSummary summary)
        {
            if (summary.isEmpty) return;
            this.loadSlotListView.ShowConfirm(
                $"Load slot {summary.slot + 1}?",
                () => this.saveGameService.LoadSlot(summary.slot));
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
