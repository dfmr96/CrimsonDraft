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

        // True while the controller itself flips toggles (restoring saved state, following the
        // device in hand) -- those aren't player presses, so they must not play the confirm sound.
        private bool syncingView;

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
            this.view.GammaSlider.onValueChanged.AddListener(this.graphicsSettings.SetGamma);

            // Toggles share a ToggleGroup (mutually exclusive) -- only react to the one turning
            // ON, or a single click would fire both listeners (the one switching off too).
            this.view.ModernToggle.onValueChanged.AddListener(isOn => { if (isOn) { PlayToggleConfirm(); SetScheme(ControlScheme.Modern); } });
            this.view.ClassicToggle.onValueChanged.AddListener(isOn => { if (isOn) { PlayToggleConfirm(); SetScheme(ControlScheme.Classic); } });

            this.view.KeyboardLayoutToggle.onValueChanged.AddListener(isOn => { if (isOn) { PlayToggleConfirm(); this.view.ShowControlsLayout(false); } });
            this.view.GamepadLayoutToggle.onValueChanged.AddListener(isOn  => { if (isOn) { PlayToggleConfirm(); this.view.ShowControlsLayout(true); } });

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
            this.view.SetGammaValue(this.graphicsSettings.Gamma);
            this.view.SetControlToggle(this.controlScheme.CurrentScheme == ControlScheme.Classic);
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

            // Always open on the mapping of the device in hand; the layout toggles only
            // override it until the window is reopened or the player switches device.
            if (window == OptionsWindow.Controls)
            {
                SyncControlsLayout(this.usingGamepad);
                this.view.SetSchemeDescription(this.controlScheme.CurrentScheme == ControlScheme.Classic);
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
            this.state = PauseState.Options;
            this.view.ShowOptions();
            if (this.openWindow == OptionsWindow.Video)
                this.view.FadeInventoryVolume(true);
            EventSystem.current.SetSelectedGameObject(this.view.CategoryButton(this.openWindow).gameObject);
        }

        private void SyncControlsLayout(bool gamepad)
        {
            this.syncingView = true;
            this.view.ShowControlsLayout(gamepad);
            this.syncingView = false;
        }

        private void PlayToggleConfirm()
        {
            if (!this.syncingView) this.view.PlayConfirm();
        }

        private void SetScheme(ControlScheme scheme)
        {
            this.controlScheme.SetScheme(scheme);
            this.view.SetSchemeDescription(scheme == ControlScheme.Classic);
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
            if (this.state == PauseState.Window && this.openWindow == OptionsWindow.Controls)
                SyncControlsLayout(gamepad);
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
            InputSystem.onActionChange -= OnActionChange;
        }
    }
}
