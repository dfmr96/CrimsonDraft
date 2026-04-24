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
        private Action?  onCancel;

        [Preserve]
        public PoiController(IInputService inputService, PoiDialogView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UIConfirm.performed += OnConfirm;
            this.inputService.UICancel.performed  += OnCancel;
        }

        public void Open(string[] poiLines, Action? onClose = null, Action? onCancel = null)
        {
            this.lines     = poiLines;
            this.lineIndex = 0;
            this.isOpen    = true;
            this.onClose   = onClose;
            this.onCancel  = onCancel;
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

        private void OnCancel(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.onCancel == null) return;
            var action    = this.onCancel;
            this.onClose  = null;
            this.onCancel = null;
            this.isOpen   = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            action.Invoke();
        }

        private void Close()
        {
            this.isOpen   = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
            var action    = this.onClose;
            this.onClose  = null;
            this.onCancel = null;
            action?.Invoke();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UIConfirm.performed -= OnConfirm;
            this.inputService.UICancel.performed  -= OnCancel;
        }
    }
}
