#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Audio;
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation
{
    public sealed class PauseMenuController : IInitializable, IDisposable
    {
        private const string MainMenuSceneName = "MainMenu";

        private enum PauseState { Closed, Main, Options, Window }

        private readonly IInputService          inputService;
        private readonly PauseMenuView          view;
        private readonly IAudioSettingsService  audioSettings;
        private readonly IGraphicsSettingsService graphicsSettings;
        private readonly IControlSchemeService  controlScheme;
        private readonly ScreenFader            screenFader;

        private PauseState    state      = PauseState.Closed;
        private OptionsWindow openWindow = OptionsWindow.Audio;

        // Last device that performed any action -- drives which layout (gamepad vs keyboard)
        // the Controls window shows, and flips it live if the player switches mid-menu.
        private bool usingGamepad;

        // Row cursor inside the Controls window (0 = Classic/Modern, 1 = Keyboard/Gamepad).
        private int controlsRow;

        [Preserve]
        public PauseMenuController(
            IInputService inputService,
            PauseMenuView view,
            IAudioSettingsService audioSettings,
            IGraphicsSettingsService graphicsSettings,
            IControlSchemeService controlScheme,
            ScreenFader screenFader)
        {
            this.inputService     = inputService;
            this.view             = view;
            this.audioSettings    = audioSettings;
            this.graphicsSettings = graphicsSettings;
            this.controlScheme    = controlScheme;
            this.screenFader      = screenFader;
        }

        void IInitializable.Initialize()
        {
            this.inputService.Pause.performed    += OnPauseToggle;
            this.inputService.UIBack.performed   += OnBack;
            this.inputService.UICancel.performed += OnBack;

            this.view.ResumeButton.onClick.AddListener(()  => { this.view.PlayConfirm(); Resume(); });
            this.view.OptionsButton.onClick.AddListener(() => { this.view.PlayConfirm(); OpenOptions(); });
            this.view.QuitButton.onClick.AddListener(()    => { this.view.PlayConfirm(); QuitToMenuAsync().Forget(); });

            this.view.AudioButton.onClick.AddListener(()    => { this.view.PlayConfirm(); OpenWindow(OptionsWindow.Audio); });
            this.view.VideoButton.onClick.AddListener(()    => { this.view.PlayConfirm(); OpenWindow(OptionsWindow.Video); });
            this.view.ControlsButton.onClick.AddListener(() => { this.view.PlayConfirm(); OpenWindow(OptionsWindow.Controls); });

            this.view.MasterSlider.onValueChanged.AddListener(this.audioSettings.SetMasterVolume);
            this.view.SfxSlider.onValueChanged.AddListener(this.audioSettings.SetSfxVolume);
            this.view.MusicSlider.onValueChanged.AddListener(this.audioSettings.SetMusicVolume);
            // Slider is whole numbers 0..GammaSteps (9 positions, matching every other gamma
            // control) -- rescale back down to the 0..1 value the service stores.
            this.view.GammaSlider.onValueChanged.AddListener(v => this.graphicsSettings.SetGamma(v / this.graphicsSettings.GammaSteps));

            // Toggles share a ToggleGroup (mutually exclusive) -- only react to the one turning
            // ON, or a single click would fire both listeners (the one switching off too).
            this.view.ControlsPanel.Setup(this.controlScheme, this.view.PlayCursor, this.view.PlayConfirm);
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;

            InputSystem.onActionChange += OnActionChange;

            this.view.HideAll();
        }

        private void OnPauseToggle(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case PauseState.Closed: this.view.PlayConfirm(); Open(); break;
                case PauseState.Main:   this.view.PlayCancel();  Resume(); break;
                // Options/Window: ignore -- must back out to Main first.
            }
        }

        private void Open()
        {
            this.state = PauseState.Main;
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
            this.view.ShowMain();
            EventSystem.current.SetSelectedGameObject(this.view.FirstMainSelectable);
            this.graphicsSettings.PushGammaSuppression();
            this.view.FadeInventoryVolume(true);
        }

        private void Resume()
        {
            this.state = PauseState.Closed;
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.view.HideAll();
            EventSystem.current.SetSelectedGameObject(null);
            this.graphicsSettings.PopGammaSuppression();
            this.view.FadeInventoryVolume(false);
        }

        private void OpenOptions()
        {
            this.state = PauseState.Options;
            this.view.SetSliderValues(
                this.audioSettings.MasterVolume,
                this.audioSettings.SfxVolume,
                this.audioSettings.MusicVolume);
            this.view.SetGammaValue(this.graphicsSettings.Gamma * this.graphicsSettings.GammaSteps);
            this.view.ShowOptions();
            EventSystem.current.SetSelectedGameObject(this.view.FirstOptionsSelectable);

            // Open() suppresses gamma so the dimmed pause backdrop stays neutral -- but that
            // also hides the live effect of this exact slider. Lift it while Options is up so
            // moving the slider is visible immediately instead of only after Resume().
            this.graphicsSettings.PopGammaSuppression();
        }

        private void OpenWindow(OptionsWindow window)
        {
            this.state      = PauseState.Window;
            this.openWindow = window;
            this.view.ShowWindow(window);

            // Always open on the mapping of the device in hand; the Keyboard/Gamepad row only
            // overrides it until the window is reopened or the player switches device.
            if (window == OptionsWindow.Controls)
            {
                this.controlsRow = 0;
                this.view.ControlsPanel.SetDevice(this.usingGamepad);
                this.view.ControlsPanel.ShowOutline(this.controlsRow);
                EventSystem.current.SetSelectedGameObject(null); // The panel is navigated by this controller, not the EventSystem.
                return;
            }

            // Brightness has to be judged on the raw scene -- drop the pause menu's CRT/PSX
            // volume while Video is up (the backdrop layers are hidden by ShowWindow).
            if (window == OptionsWindow.Video)
                this.view.FadeInventoryVolume(false);

            var first = this.view.FirstWindowSelectable(window);
            if (first != null)
                EventSystem.current.SetSelectedGameObject(first);
        }

        private void CloseWindow()
        {
            if (this.openWindow == OptionsWindow.Controls) this.view.ControlsPanel.HideOutlines();
            this.state = PauseState.Options;
            this.view.ShowOptions();
            if (this.openWindow == OptionsWindow.Video)
                this.view.FadeInventoryVolume(true);
            EventSystem.current.SetSelectedGameObject(this.view.CategoryButton(this.openWindow).gameObject);
        }

        private bool ControlsOpen => this.state == PauseState.Window && this.openWindow == OptionsWindow.Controls;

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!ControlsOpen) return;
            var panel = this.view.ControlsPanel;
            var direction = ctx.ReadValue<Vector2>();

            int vertical = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (vertical != 0)
            {
                int next = Mathf.Clamp(this.controlsRow + vertical, 0, panel.ChannelCount - 1);
                if (next == this.controlsRow) return;
                this.controlsRow = next;
                this.view.PlayCursor();
                panel.ShowOutline(this.controlsRow);
                return;
            }

            int horizontal = direction.x > 0.5f ? 1 : direction.x < -0.5f ? -1 : 0;
            if (horizontal != 0) panel.Adjust(this.controlsRow, horizontal);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (ControlsOpen) this.view.ControlsPanel.Confirm(this.controlsRow);
        }

        private void OnActionChange(object obj, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || obj is not InputAction action)
                return;

            var device = action.activeControl?.device;
            if (device == null)
                return;

            // Mouse counts as keyboard -- only a Gamepad switches to the pad layout.
            bool gamepad = device is Gamepad;
            if (gamepad == this.usingGamepad)
                return;

            this.usingGamepad = gamepad;
            if (ControlsOpen)
                this.view.ControlsPanel.SetDevice(gamepad);
        }

        private void CloseOptions()
        {
            this.state = PauseState.Main;
            this.view.ShowMain();
            EventSystem.current.SetSelectedGameObject(this.view.OptionsButton.gameObject);

            // Restore the neutral backdrop for the Main panel -- balances the Pop in OpenOptions.
            this.graphicsSettings.PushGammaSuppression();
        }

        private async UniTaskVoid QuitToMenuAsync()
        {
            this.state = PauseState.Closed;
            Time.timeScale = 1f;
            this.graphicsSettings.PopGammaSuppression();
            this.view.FadeInventoryVolume(false);

            // Was a bare synchronous SceneManager.LoadScene(Single) -- that tears down the
            // gameplay scene and activates MainMenu in the same frame, with no gap for Wwise
            // to unregister the old AkAudioListener before the new one registers. Routing
            // through the async LoadSceneAsync (same helper "New Game" already uses) spreads
            // that across frames like every other scene transition does, which is the only
            // structural difference from the transitions that don't lose audio.
            await this.screenFader.LoadSceneAsync(MainMenuSceneName);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (this.state != PauseState.Closed)
                this.view.PlayCancel();

            switch (this.state)
            {
                case PauseState.Window:  CloseWindow(); break;
                case PauseState.Options: CloseOptions(); break;
                case PauseState.Main:    Resume(); break;
                // Closed: no-op.
            }
        }

        void IDisposable.Dispose()
        {
            this.inputService.Pause.performed    -= OnPauseToggle;
            this.inputService.UIBack.performed   -= OnBack;
            this.inputService.UICancel.performed -= OnBack;
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            InputSystem.onActionChange -= OnActionChange;
        }
    }
}
