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
    public sealed class PoiController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly PoiDialogView view;

        private string[] lines = Array.Empty<string>();
        private int      lineIndex;
        private bool     isOpen;
        private Action?  onClose;

        [Preserve]
        public PoiController(IInputService inputService, PoiDialogView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UIConfirm.performed += OnConfirm;
        }

        public void Open(string[] poiLines, Action? onClose = null)
        {
            this.lines     = poiLines;
            this.lineIndex = 0;
            this.isOpen    = true;
            this.onClose   = onClose;
            Time.timeScale  = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.lines[0]);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;

            this.lineIndex++;

            if (this.lineIndex >= this.lines.Length)
            {
                Close();
                return;
            }

            this.view.Show(this.lines[this.lineIndex]);
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            this.onClose?.Invoke();
            this.onClose = null;
        }

        void IDisposable.Dispose()
        {
            this.inputService.UIConfirm.performed -= OnConfirm;
        }
    }
}
