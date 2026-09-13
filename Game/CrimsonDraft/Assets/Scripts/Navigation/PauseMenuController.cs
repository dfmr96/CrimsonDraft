#nullable enable

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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

        private enum PauseState { Closed, Main, Options }

        private readonly IInputService          inputService;
        private readonly PauseMenuView          view;
        private readonly IAudioSettingsService  audioSettings;
        private readonly IGraphicsSettingsService graphicsSettings;
        private readonly IControlSchemeService  controlScheme;
        private readonly ScreenFader            screenFader;

        private PauseState state = PauseState.Closed;

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

            this.view.ResumeButton.onClick.AddListener(Resume);
            this.view.OptionsButton.onClick.AddListener(OpenOptions);
            this.view.QuitButton.onClick.AddListener(() => QuitToMenuAsync().Forget());

            this.view.MasterSlider.onValueChanged.AddListener(this.audioSettings.SetMasterVolume);
            this.view.SfxSlider.onValueChanged.AddListener(this.audioSettings.SetSfxVolume);
            this.view.MusicSlider.onValueChanged.AddListener(this.audioSettings.SetMusicVolume);
            this.view.GammaSlider.onValueChanged.AddListener(this.graphicsSettings.SetGamma);

            // Toggles share a ToggleGroup (mutually exclusive) -- only react to the one turning
            // ON, or a single click would fire both listeners (the one switching off too).
            this.view.ModernToggle.onValueChanged.AddListener(isOn => { if (isOn) this.controlScheme.SetScheme(ControlScheme.Modern); });
            this.view.ClassicToggle.onValueChanged.AddListener(isOn => { if (isOn) this.controlScheme.SetScheme(ControlScheme.Classic); });

            this.view.HideAll();
        }

        private void OnPauseToggle(InputAction.CallbackContext _)
        {
            switch (this.state)
            {
                case PauseState.Closed: Open(); break;
                case PauseState.Main:   Resume(); break;
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
            this.view.SetSliderValues(
                this.audioSettings.MasterVolume,
                this.audioSettings.SfxVolume,
                this.audioSettings.MusicVolume);
            this.view.SetGammaValue(this.graphicsSettings.Gamma);
            this.view.SetControlToggle(this.controlScheme.CurrentScheme == ControlScheme.Classic);
            this.view.ShowOptions();
            EventSystem.current.SetSelectedGameObject(this.view.FirstOptionsSelectable);
        }

        private void CloseOptions()
        {
            this.state = PauseState.Main;
            this.view.ShowMain();
            EventSystem.current.SetSelectedGameObject(this.view.OptionsButton.gameObject);
        }

        private async UniTaskVoid QuitToMenuAsync()
        {
            this.state = PauseState.Closed;
            Time.timeScale = 1f;
            this.graphicsSettings.PopGammaSuppression();
            this.view.FadeInventoryVolume(false);
            await this.screenFader.FadeOutAsync();
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            await this.screenFader.FadeInAsync();
        }

        private void OnBack(InputAction.CallbackContext _)
        {
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
        }
    }
}
