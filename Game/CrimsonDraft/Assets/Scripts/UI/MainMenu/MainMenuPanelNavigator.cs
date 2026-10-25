#nullable enable

using System;
using DG.Tweening;
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

        // How the title gives way to a sub menu: straight away, with the storm (lightning, white-out,
        // slow dim), or with a plain dim while the rain fades out (Options).
        private enum Intro { None, Storm, Dim }

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
        [Tooltip("Transicion lenta (rayo, flash, fondo que se oscurece) para New Game y Load Game. Opcional: sin esto se abren directo.")]
        [SerializeField] private MainMenuStormTransition? stormTransition;
        [Tooltip("Lluvia en el titulo, musica solo en Options, nada en New Game / Load Game.")]
        [SerializeField] private MainMenuMusicController? music;
        [Tooltip("Con esto se abre la lista de saves al entrar a LOADGAME. El panel de Load Game maneja su propio Confirm/Cancel (navegación anidada lista/confirmar), así que OnCancel lo ignora por completo -- ver BackFromLoadGame.")]
        [SerializeField] private MainMenuController mainMenuController = null!;

        private IInputService     inputService = null!;
        private MainMenuSfxData   sfx          = null!; // panel switch whoosh
        private InventorySfxData  uiSfx        = null!; // shared UI decide / cancel
        private Destination       currentDestination;
        private bool              transitioning; // the storm is playing: no cancel, no second open
        private bool              launching;     // the game is starting (long fade): no way back
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
            Open(Destination.NewGame, this.newGamePanel, this.newGameButton, this.newGameNavigator.Open, Intro.Storm);

        private void OpenOptions() =>
            Open(Destination.Options, this.optionsPanel, this.optionsButton, this.optionsController.Open, Intro.Dim);

        private void OpenLoadGame() =>
            Open(Destination.LoadGame, this.loadGamePanel, this.loadGameButton, this.mainMenuController.OpenLoadGameList, Intro.Storm);

        private void Open(Destination destination, GameObject panel, Button source, Action onOpened, Intro intro = Intro.None)
        {
            if (this.currentDestination != Destination.None) return;
            this.currentDestination = destination;
            this.lastMainMenuButton = source.gameObject;

            this.uiSfx.PlayDecide(gameObject);
            EventSystem.current?.SetSelectedGameObject(null);

            if (intro == Intro.Dim && this.stormTransition != null)
            {
                // The rain fades out and the screen dims; the sub menu only opens once it is quiet.
                this.transitioning = true;
                this.music?.EnterSilentSubMenu();
                this.stormTransition.PlayDim(() =>
                {
                    this.transitioning = false;
                    ShowPanel(destination, panel, onOpened);
                });
                return;
            }

            if (intro == Intro.Storm && this.stormTransition != null)
            {
                this.transitioning = true;
                this.stormTransition.Play(
                    onThunder:  () => this.sfx.PlayThunder(gameObject),
                    onDarkened: () =>
                    {
                        this.transitioning = false;
                        ShowPanel(destination, panel, onOpened);
                    });
                return;
            }

            ShowPanel(destination, panel, onOpened);
        }

        private void ShowPanel(Destination destination, GameObject panel, Action onOpened)
        {
            this.sfx.PlayPanelTravel(gameObject);
            if (destination == Destination.Options) this.music?.EnterOptions();
            else this.music?.EnterSilentSubMenu();

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

        /// <summary>The game is about to load (the long fade has started): Cancel no longer goes back.</summary>
        public void BeginLaunch() => this.launching = true;

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
            if (this.transitioning
                || this.launching
                || this.currentDestination == Destination.None
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

            // Leaving Options: the music fades out first and the title only comes back once it is
            // gone (see ShowMainMenuWhenQuiet). From the other sub menus there is nothing to wait for.
            float closeStarted = Time.unscaledTime;
            bool waitForQuiet  = from == Destination.Options && this.music != null;
            if (waitForQuiet) this.music!.EnterSilentSubMenu();
            else this.music?.EnterMainMenu();

            GameObject panel = this.newGamePanel;
            switch (from)
            {
                case Destination.NewGame:
                    this.newGameNavigator.Close();
                    panel = this.newGamePanel;
                    break;
                case Destination.Options:
                    this.optionsController.Close();
                    panel = this.optionsPanel;
                    break;
                case Destination.LoadGame:
                    panel = this.loadGamePanel;
                    break;
            }

            // The screen turns off (ScreenPowerOn) before the main menu comes back.
            Action showTitle = waitForQuiet ? () => ShowMainMenuWhenQuiet(closeStarted) : ShowMainMenu;
            var powerOn = panel.GetComponent<ScreenPowerOn>();
            if (powerOn != null) powerOn.Close(showTitle);
            else
            {
                panel.SetActive(false);
                showTitle();
            }
        }

        private void ShowMainMenuWhenQuiet(float closeStarted)
        {
            float wait = this.music!.SilenceSeconds - (Time.unscaledTime - closeStarted);
            if (wait <= 0f)
            {
                ShowMainMenu();
                return;
            }
            DOVirtual.DelayedCall(wait, ShowMainMenu, ignoreTimeScale: true).SetTarget(this);
        }

        private void ShowMainMenu()
        {
            // Something opened a sub menu again before the close finished: leave it alone.
            if (this.currentDestination != Destination.None) return;

            // Enabling the main menu re-selects its first button; put the cursor back on the one
            // that opened the sub menu instead.
            this.stormTransition?.Restore();
            this.music?.EnterMainMenu();
            this.mainMenuPanel.SetActive(true);
            if (this.lastMainMenuButton != null)
                EventSystem.current?.SetSelectedGameObject(this.lastMainMenuButton);
        }
    }
}
