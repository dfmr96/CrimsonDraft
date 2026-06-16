#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class PuzzleViewController : IInitializable, IDisposable
    {
        private const float NavCooldown = 0.2f;

        private readonly IInputService inputService;

        private GameObject?      spawnedCanvas;
        private INavigablePuzzle? navigablePuzzle;
        private bool              isOpen;
        private float       lastNavTime = float.MinValue;

        [Preserve]
        public PuzzleViewController(IInputService inputService)
        {
            this.inputService = inputService;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnCancel;
        }

        public void Open(GameObject canvasPrefab, Action? onSolved = null)
        {
            this.spawnedCanvas   = UnityEngine.Object.Instantiate(canvasPrefab);
            this.navigablePuzzle = this.spawnedCanvas.GetComponentInChildren<INavigablePuzzle>();
            this.isOpen          = true;
            this.lastNavTime     = float.MinValue;
            Time.timeScale       = 0f;
            this.inputService.SwitchToUI();

            if (this.navigablePuzzle != null)
                this.navigablePuzzle.OnSolved = () => { Close(); onSolved?.Invoke(); };
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen || this.navigablePuzzle == null) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            var   dir = ctx.ReadValue<Vector2>();
            float x   = dir.x;
            float y   = dir.y;

            if      (x >  0.5f) this.navigablePuzzle.MoveRight();
            else if (x < -0.5f) this.navigablePuzzle.MoveLeft();
            else if (y < -0.5f) this.navigablePuzzle.MoveDown();
            else if (y >  0.5f) this.navigablePuzzle.MoveUp();
            else return;

            this.lastNavTime = Time.unscaledTime;
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.navigablePuzzle == null) return;
            this.navigablePuzzle.Toggle();
        }

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
        }

        private void Close()
        {
            if (this.spawnedCanvas != null)
                UnityEngine.Object.Destroy(this.spawnedCanvas);
            this.spawnedCanvas    = null;
            this.navigablePuzzle  = null;
            this.isOpen        = false;
            Time.timeScale     = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UICancel.performed   -= OnCancel;
        }
    }
}
