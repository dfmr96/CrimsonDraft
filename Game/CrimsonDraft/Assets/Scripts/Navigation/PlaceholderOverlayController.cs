#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Navigation
{
    public sealed class PlaceholderOverlayController : IInitializable, IDisposable
    {
        private enum Overlay { None, Map, Pause }

        private readonly IInputService          inputService;
        private readonly PlaceholderOverlayView view;

        private Overlay current = Overlay.None;

        [Preserve]
        public PlaceholderOverlayController(IInputService inputService, PlaceholderOverlayView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenMap.performed  += OnOpenMap;
            this.inputService.Pause.performed    += OnPause;
            this.inputService.UIBack.performed   += OnBack;
            this.inputService.UICancel.performed += OnBack;

        }

        private void OnOpenMap(InputAction.CallbackContext _)
        {
            if (this.current != Overlay.None) return;
            this.current = Overlay.Map;
            this.view.ShowMap();
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
        }

        private void OnPause(InputAction.CallbackContext _)
        {
            if (this.current != Overlay.None) return;
            this.current = Overlay.Pause;
            this.view.ShowPause();
            Time.timeScale = 0f;
            this.inputService.SwitchToUI();
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (this.current == Overlay.None) return;
            CloseOverlay();
        }

        private void CloseOverlay()
        {
            this.current = Overlay.None;
            this.view.HideAll();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenMap.performed  -= OnOpenMap;
            this.inputService.Pause.performed    -= OnPause;
            this.inputService.UIBack.performed   -= OnBack;
            this.inputService.UICancel.performed -= OnBack;
        }
    }
}
