#nullable enable

using System;
using CrimsonDraft.Infrastructure.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Switches between the main menu and its sub menus (New Game, Load Game, Options), all of
    /// which live in the same screen-space canvas -- no camera movement. Pressing one of the main
    /// menu buttons hides the main menu and shows that sub menu's root; Cancel (or the sub menu
    /// closing itself) hides it again and shows the main menu with the same button selected.
    /// </summary>
    public sealed class MainMenuPanelNavigator : MonoBehaviour
    {
        private enum Destination { None, NewGame, Options, LoadGame }

        [Header("Panels")]
        [SerializeField] private GameObject mainMenuPanel = null!;
        [SerializeField] private GameObject newGamePanel  = null!;
        [SerializeField] private GameObject optionsPanel  = null!;
        [SerializeField] private GameObject loadGamePanel = null!;

        [Header("Main menu buttons")]
        [SerializeField] private Button newGameButton  = null!;
        [SerializeField] private Button loadGameButton = null!;
        [SerializeField] private Button optionsButton  = null!;

        [Header("Sub menu controllers")]
        [SerializeField] private OptionsMenuNavigator optionsController = null!;
        [Tooltip("Navegación del panel de New Game (filas de controles + botón de inicio).")]
        [SerializeField] private NewGameNavigator newGameNavigator = null!;
        [Tooltip("Con esto se abre la lista de saves al entrar a LOADGAME. El panel de Load Game maneja su propio Confirm/Cancel (navegación anidada lista/confirmar), así que OnCancel lo ignora por completo -- ver BackFromLoadGame.")]
        [SerializeField] private MainMenuController mainMenuController = null!;

        private IInputService     inputService = null!;
        private MainMenuSfxData   sfx          = null!; // panel switch whoosh
        private InventorySfxData  uiSfx        = null!; // shared UI decide / cancel
        private Destination       currentDestination;
        private GameObject?       lastMainMenuButton;

        [Inject]
        public void Construct(IInputService inputService, MainMenuSfxData sfx, InventorySfxData uiSfx)
        {
            this.inputService = inputService;
            this.sfx          = sfx;
            this.uiSfx        = uiSfx;
            this.inputService.UICancel.performed += OnCancel;
        }

        private void Awake()
        {
            this.newGameButton.onClick.AddListener(OpenNewGame);
            this.loadGameButton.onClick.AddListener(OpenLoadGame);
            this.optionsButton.onClick.AddListener(OpenOptions);
        }

        private void OnDestroy()
        {
            this.newGameButton.onClick.RemoveListener(OpenNewGame);
            this.loadGameButton.onClick.RemoveListener(OpenLoadGame);
            this.optionsButton.onClick.RemoveListener(OpenOptions);

            if (this.inputService == null) return;
            this.inputService.UICancel.performed -= OnCancel;
        }

        private void OpenNewGame() =>
            Open(Destination.NewGame, this.newGamePanel, this.newGameButton, this.newGameNavigator.Open);

        private void OpenOptions() =>
            Open(Destination.Options, this.optionsPanel, this.optionsButton, this.optionsController.Open);

        private void OpenLoadGame() =>
            Open(Destination.LoadGame, this.loadGamePanel, this.loadGameButton, this.mainMenuController.OpenLoadGameList);

        private void Open(Destination destination, GameObject panel, Button source, Action onOpened)
        {
            if (this.currentDestination != Destination.None) return;
            this.currentDestination = destination;
            this.lastMainMenuButton = source.gameObject;

            this.uiSfx.PlayDecide(gameObject);
            this.sfx.PlayPanelTravel(gameObject);
            EventSystem.current?.SetSelectedGameObject(null);

            this.mainMenuPanel.SetActive(false);
            panel.SetActive(true);

            // The Confirm that pressed the button is still being dispatched to every UIConfirm
            // subscriber this frame -- opening the sub menu right away would let it also react to
            // that same press (e.g. confirm its first row). Wait until the input update is done.
            DeferredInputAction.Run(() =>
            {
                if (this.currentDestination == destination) onOpened();
            });
        }

        /// <summary>
        /// The Load Game panel owns Confirm/Cancel itself (list vs confirm sub-state via
        /// SaveSlotNavigator), so OnCancel ignores Destination.LoadGame entirely -- this is
        /// called instead once MainMenuController's navigator fully closes (backed all the
        /// way out, not a slot being loaded).
        /// </summary>
        public void BackFromLoadGame()
        {
            if (this.currentDestination != Destination.LoadGame) return;
            BackToMainMenu();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (this.currentDestination == Destination.None
                || this.currentDestination == Destination.LoadGame) return;

            // New Game has two steps (controls, gamma) and Options has two levels (buttons,
            // a tab's content): cancel first steps back inside them.
            if (this.currentDestination == Destination.NewGame && this.newGameNavigator.TryGoBack()) return;
            if (this.currentDestination == Destination.Options && this.optionsController.TryGoBack()) return;
            BackToMainMenu();
        }

        private void BackToMainMenu()
        {
            Destination from = this.currentDestination;
            this.currentDestination = Destination.None;

            this.uiSfx.PlayCancel(gameObject);
            this.sfx.PlayPanelTravel(gameObject);

            switch (from)
            {
                case Destination.NewGame:
                    this.newGameNavigator.Close();
                    this.newGamePanel.SetActive(false);
                    break;
                case Destination.Options:
                    this.optionsController.Close();
                    this.optionsPanel.SetActive(false);
                    break;
                case Destination.LoadGame:
                    this.loadGamePanel.SetActive(false);
                    break;
            }

            // Enabling the main menu re-selects its first button; put the cursor back on the one
            // that opened the sub menu instead.
            this.mainMenuPanel.SetActive(true);
            if (this.lastMainMenuButton != null)
                EventSystem.current?.SetSelectedGameObject(this.lastMainMenuButton);
        }
    }
}
