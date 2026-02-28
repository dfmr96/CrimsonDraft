#nullable enable

using System;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;

namespace CrimsonDraft.Navigation.UI
{
    public sealed class InventoryController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly InventoryView view;

        private bool isOpen;

        [Preserve]
        public InventoryController(IInputService inputService, InventoryView view)
        {
            this.inputService = inputService;
            this.view = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.OpenInventory.performed += OnOpenInventory;
            this.inputService.UICancel.performed      += OnUICancel;
        }

        private void OnOpenInventory(InputAction.CallbackContext _)
        {
            if (this.isOpen)
                return;

            this.isOpen = true;
            this.inputService.SwitchToUI();
            this.view.Show();
        }

        private void OnUICancel(InputAction.CallbackContext _)
        {
            if (!this.isOpen)
                return;

            this.isOpen = false;
            this.view.Hide();
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.OpenInventory.performed -= OnOpenInventory;
            this.inputService.UICancel.performed      -= OnUICancel;
        }
    }
}
