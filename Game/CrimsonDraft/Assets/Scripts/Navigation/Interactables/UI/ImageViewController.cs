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
    public sealed class ImageViewController : IInitializable, IDisposable
    {
        private const float NavCooldown = 0.2f;

        private readonly IInputService inputService;

        private GameObject? spawnedCanvas;
        private PuzzleView? puzzleView;
        private bool        isOpen;
        private float       lastNavTime = float.MinValue;

        [Preserve]
        public ImageViewController(IInputService inputService)
        {
            this.inputService = inputService;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UICancel.performed   += OnCancel;
        }

        public void Open(GameObject canvasPrefab)
        {
            this.spawnedCanvas = UnityEngine.Object.Instantiate(canvasPrefab);
            this.puzzleView    = this.spawnedCanvas.GetComponentInChildren<PuzzleView>();
            this.isOpen        = true;
            this.lastNavTime   = float.MinValue;
            Time.timeScale     = 0f;
            this.inputService.SwitchToUI();
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen || this.puzzleView == null) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            float x = ctx.ReadValue<Vector2>().x;
            if      (x >  0.5f) this.puzzleView.MoveRight();
            else if (x < -0.5f) this.puzzleView.MoveLeft();
            else return;

            this.lastNavTime = Time.unscaledTime;
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.puzzleView == null) return;
            this.puzzleView.Toggle();
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
            this.spawnedCanvas = null;
            this.puzzleView    = null;
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
