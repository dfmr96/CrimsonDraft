#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Graphics;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.UI;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation
{
    public sealed class PauseMenuController : IInitializable, IDisposable
    {
        private const string MainMenuSceneName = "MainMenu";

        private enum PauseState { Closed, Main, Options }

        private readonly IInputService            inputService;
        private readonly PauseMenuView            view;
        private readonly IGraphicsSettingsService graphicsSettings;
        private readonly IControlSchemeService    controlScheme;
        private readonly ScreenFader              screenFader;

        private PauseState state = PauseState.Closed;
        private bool quitting;

        // True while the Video tab is up: gamma is live (suppression lifted) and the dimmed
        // backdrop is hidden so the brightness is judged on the actual scene.
        private bool videoLive;

        // Last device that performed any action -- drives which layout (gamepad vs keyboard)
        // the Control tab shows, and flips it live if the player switches mid-menu.
        private bool usingGamepad;

        [Preserve]
        public PauseMenuController(
            IInputService inputService,
            PauseMenuView view,
            IGraphicsSettingsService graphicsSettings,
            IControlSchemeService controlScheme,
            ScreenFader screenFader)
        {
            this.inputService     = inputService;
            this.view             = view;
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

            // Settings content is driven by OptionsTabController (Video / Control / Sound), the
            // same way as the main menu's Options screen.
            this.view.ControlsPanel.Setup(this.controlScheme, this.view.PlayCursor, this.view.PlayConfirm);
            this.view.OptionsTabs.ActiveTabChanged += OnTabChanged;

            InputSystem.onActionChange += OnActionChange;

            this.view.HideAll();
        }

        private void OnPauseToggle(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case PauseState.Closed: this.view.PlayConfirm(); Open(); break;
                case PauseState.Main:   this.view.PlayCancel();  Resume(); break;
                // Options: ignore -- must back out to Main first.
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
            this.view.ShowOptions();

            // The tab controller owns navigation from here -- nothing is selected in the EventSystem.
            EventSystem.current.SetSelectedGameObject(null);

            // Always open on the mapping of the device in hand; the Keyboard/Gamepad row only
            // overrides it until Settings is reopened or the player switches device.
            this.view.ControlsPanel.SetDevice(this.usingGamepad);
            this.view.OptionsTabs.Open(); // raises ActiveTabChanged(0) -> Video mode
        }

        private void CloseOptions()
        {
            this.view.OptionsTabs.Close();
            SetVideoMode(false);

            this.state = PauseState.Main;
            this.view.ShowMain();
            EventSystem.current.SetSelectedGameObject(this.view.OptionsButton.gameObject);
        }

        private void OnTabChanged(int tab)
        {
            if (this.state != PauseState.Options) return;
            SetVideoMode(tab == 0);
        }

        // Balances the Push taken by Open(): Video tab up = Pop (live gamma, no dim), anything
        // else = Push again (neutral backdrop, CRT/PSX look back on).
        private void SetVideoMode(bool on)
        {
            if (on == this.videoLive) return;
            this.videoLive = on;

            if (on) this.graphicsSettings.PopGammaSuppression();
            else    this.graphicsSettings.PushGammaSuppression();

            this.view.FadeInventoryVolume(!on);
            this.view.SetBackdropVisible(!on);
            this.view.SetVideoUiMode(on);
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
            if (this.state == PauseState.Options)
                this.view.ControlsPanel.SetDevice(gamepad);
        }

        private async UniTaskVoid QuitToMenuAsync()
        {
            // Submit can fire again while the scene load is in flight -- each extra call would
            // stack another fade + load on top of the first.
            if (this.quitting) return;
            this.quitting = true;
            this.view.QuitButton.interactable = false;

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
            this.view.OptionsTabs.ActiveTabChanged -= OnTabChanged;
            InputSystem.onActionChange -= OnActionChange;
        }
    }
}
