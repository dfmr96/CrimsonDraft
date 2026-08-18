#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
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

        private IInputService      inputService      = null!;
        private ISaveGameService   saveGameService   = null!;
        private IGameStateResetter gameStateResetter = null!;
        private SaveSlotNavigator  loadNavigator     = null!;

        [Inject]
        public void Construct(IInputService inputService, ISaveGameService saveGameService, IGameStateResetter gameStateResetter)
        {
            this.inputService      = inputService;
            this.saveGameService   = saveGameService;
            this.gameStateResetter = gameStateResetter;

            this.loadNavigator = new SaveSlotNavigator(
                this.loadSlotListView,
                "Load",
                slot => this.saveGameService.LoadSlot(slot),
                canConfirm: summary => !summary.isEmpty,
                onClosed: () => this.inputService.SwitchToGameplay());

            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UIBack.performed     += OnBack;

            this.newGameButton.onClick.AddListener(OnNewGameClicked);
            this.loadGameButton.onClick.AddListener(OnLoadGameClicked);
            this.exitButton.onClick.AddListener(OnExitClicked);

            this.loadGameButton.interactable = HasAnySave();
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UIBack.performed     -= OnBack;
        }

        private bool HasAnySave()
        {
            var summaries = this.saveGameService.ListSlotSummaries();
            for (int i = 0; i < summaries.Count; i++)
                if (!summaries[i].isEmpty) return true;
            return false;
        }

        private void OnNewGameClicked()
        {
            this.gameStateResetter.ResetAll();
            SceneManager.LoadScene(this.newGameSceneName, LoadSceneMode.Single);
        }

        private void OnLoadGameClicked()
        {
            this.inputService.SwitchToUI();
            this.loadNavigator.Open(this.saveGameService.ListSlotSummaries());
        }

        private void OnNavigate(InputAction.CallbackContext ctx) => this.loadNavigator.HandleNavigate(ctx.ReadValue<Vector2>());
        private void OnConfirm(InputAction.CallbackContext _)    => this.loadNavigator.HandleConfirm();
        private void OnBack(InputAction.CallbackContext _)       => this.loadNavigator.HandleBack();

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
