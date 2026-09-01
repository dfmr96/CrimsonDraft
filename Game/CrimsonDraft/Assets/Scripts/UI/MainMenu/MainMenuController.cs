#nullable enable

using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private LoadGameSaveListView loadListView      = null!;
        [SerializeField] private MainMenuCameraTravel cameraTravel      = null!;
        [SerializeField] private NewGamePromptView    newGamePromptView = null!;

        private IInputService         inputService         = null!;
        private ISaveGameService      saveGameService      = null!;
        private IGameStateResetter    gameStateResetter    = null!;
        private IControlSchemeService controlSchemeService = null!;
        private SaveSlotNavigator     loadNavigator        = null!;
        private bool                  isLoadingSlot;

        [Inject]
        public void Construct(
            IInputService          inputService,
            ISaveGameService       saveGameService,
            IGameStateResetter     gameStateResetter,
            IControlSchemeService  controlSchemeService)
        {
            this.inputService         = inputService;
            this.saveGameService      = saveGameService;
            this.gameStateResetter    = gameStateResetter;
            this.controlSchemeService = controlSchemeService;

            this.loadNavigator = new SaveSlotNavigator(
                this.loadListView,
                "Load",
                OnSlotConfirmed,
                canConfirm: summary => !summary.isEmpty,
                onClosed: () => DeferredInputAction.Run(OnLoadNavigatorClosed));

            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnBack;

            this.newGameButton.onClick.AddListener(OnNewGameClicked);
            this.exitButton.onClick.AddListener(OnExitClicked);

            this.newGamePromptView.ModernButton.onClick.AddListener(() => SelectScheme(ControlScheme.Modern));
            this.newGamePromptView.ClassicButton.onClick.AddListener(() => SelectScheme(ControlScheme.Classic));
            this.newGamePromptView.SetSelectedScheme(this.controlSchemeService.CurrentScheme == ControlScheme.Classic);

            this.loadGameButton.interactable = HasAnySave();
        }

        private void Start()
        {
            // Runs after every scope's IInitializable.Initialize() (VContainer builds in
            // Awake), so this wins over InputService's default SwitchToGameplay() and lets
            // the EventSystem's InputSystemUIInputModule read Move/Submit/Cancel from the UI
            // map -- otherwise arrow keys/gamepad do nothing on the main menu.
            this.inputService.SwitchToUI();
        }

        private void OnDestroy()
        {
            if (this.inputService == null) return;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UICancel.performed   -= OnBack;
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

        private void SelectScheme(ControlScheme scheme)
        {
            this.controlSchemeService.SetScheme(scheme);
            this.newGamePromptView.SetSelectedScheme(scheme == ControlScheme.Classic);
        }

        /// <summary>
        /// Called by MainMenuCameraTravel once the camera arrives at LoadGame-Camera --
        /// mirrors how TravelToOptions() hands off to OptionsTabController.Open().
        /// </summary>
        public void OpenLoadGameList()
        {
            this.inputService.SwitchToUI();

            // The Load Game button stays the EventSystem's selected object after being
            // clicked/submitted. Left selected, pressing Confirm while the slot list is
            // open would also re-trigger this Button's OnClick (same UI/Submit action),
            // reopening the navigator and resetting the cursor to slot 1.
            EventSystem.current.SetSelectedGameObject(null);

            this.loadNavigator.Open(this.saveGameService.ListSlotSummaries());
        }

        private void OnSlotConfirmed(int slot)
        {
            this.isLoadingSlot = true;

            // The registries GameStateResetter clears are root-scoped singletons that
            // survive every scene reload for the whole play session (not just this save
            // file) -- e.g. RosterHealthRegistry/OperatorCorpseRegistry still hold whatever
            // an earlier death (a prior playthrough, a Game Over test) last wrote via
            // OperatorRosterBootstrap.Dispose(). Without clearing them here, that stale
            // state gets reapplied by OperatorRosterBootstrap.Initialize() in the freshly
            // loaded scene before SaveGameLoader gets a chance to restore the real save
            // data, and can leak into it.
            this.gameStateResetter.ResetAll();
            this.saveGameService.LoadSlot(slot);
        }

        private void OnLoadNavigatorClosed()
        {
            // On a successful load the scene is already being replaced -- RoomOrchestrator
            // in the new scene switches to Gameplay itself. Touching input/EventSystem here
            // would race with (and can override) that switch, or hit destroyed objects.
            if (this.isLoadingSlot) return;

            this.inputService.SwitchToUI();
            this.cameraTravel.TravelBackFromLoadGame();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            var direction = ctx.ReadValue<Vector2>();

            // While the Yes/No confirm prompt is up, horizontal input toggles which option is
            // selected instead of moving the (hidden) slot list cursor -- SaveSlotNavigator
            // itself ignores Navigate entirely while confirming.
            if (this.loadNavigator.IsConfirming)
            {
                if (direction.x > 0.5f) this.loadListView.SetConfirmSelection(yesSelected: true);
                else if (direction.x < -0.5f) this.loadListView.SetConfirmSelection(yesSelected: false);
                return;
            }

            this.loadNavigator.HandleNavigate(direction);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            // Confirming a row first opens the Yes/No prompt (HandleConfirm below); pressing
            // Confirm again while it's up executes whichever option is currently selected.
            if (this.loadNavigator.IsConfirming)
            {
                if (this.loadListView.IsYesSelected) this.loadNavigator.HandleConfirm();
                else this.loadNavigator.HandleBack();
                return;
            }

            this.loadNavigator.HandleConfirm();
        }

        private void OnBack(InputAction.CallbackContext _) => this.loadNavigator.HandleBack();

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
