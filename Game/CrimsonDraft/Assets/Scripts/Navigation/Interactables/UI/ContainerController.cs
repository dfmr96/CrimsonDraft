#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables.UI;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class ContainerController : IInitializable, IDisposable
    {
        private readonly IInputService inputService;
        private readonly ContainerView view;

        private List<ItemData>    containerItems   = new();
        private IInventoryService inventoryService = null!;
        private int               cursorIndex;
        private bool              isOpen;

        [Preserve]
        public ContainerController(IInputService inputService, ContainerView view)
        {
            this.inputService = inputService;
            this.view         = view;
        }

        void IInitializable.Initialize()
        {
            this.inputService.UINavigate.performed += OnNavigate;
            this.inputService.UIConfirm.performed  += OnConfirm;
            this.inputService.UIBack.performed     += OnBack;
        }

        public void Open(ContainerData data, IInventoryService inventory)
        {
            if (data.Emptied) return;

            this.inventoryService = inventory;
            this.containerItems   = data.Items.ToList();
            this.cursorIndex      = 0;
            this.isOpen           = true;
            Time.timeScale         = 0f;
            this.inputService.SwitchToUI();
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isOpen || this.containerItems.Count == 0) return;

            var dir = ctx.ReadValue<Vector2>();
            int delta = dir.y > 0.5f ? -1 : dir.y < -0.5f ? 1 : 0;
            if (delta == 0) return;

            this.cursorIndex = (this.cursorIndex + delta + this.containerItems.Count) % this.containerItems.Count;
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isOpen || this.containerItems.Count == 0) return;

            var item = this.containerItems[this.cursorIndex];
            this.inventoryService.AddItem(item);
            this.containerItems.RemoveAt(this.cursorIndex);

            if (this.containerItems.Count == 0)
            {
                Close();
                return;
            }

            this.cursorIndex = Mathf.Min(this.cursorIndex, this.containerItems.Count - 1);
            this.view.Show(this.containerItems, this.cursorIndex);
        }

        private void OnBack(InputAction.CallbackContext _)
        {
            if (!this.isOpen) return;
            Close();
        }

        private void Close()
        {
            this.isOpen = false;
            this.view.Hide();
            Time.timeScale = 1f;
            this.inputService.SwitchToGameplay();
        }

        void IDisposable.Dispose()
        {
            this.inputService.UINavigate.performed -= OnNavigate;
            this.inputService.UIConfirm.performed  -= OnConfirm;
            this.inputService.UIBack.performed     -= OnBack;
        }
    }
}
