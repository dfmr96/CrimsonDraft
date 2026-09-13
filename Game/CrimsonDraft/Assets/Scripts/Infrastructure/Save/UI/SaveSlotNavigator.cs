#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Infrastructure.Save.UI
{
    /// <summary>
    /// Cursor-driven navigation + confirm flow shared by the in-game save menu
    /// (SaveController) and the main menu load menu (MainMenuController). Deliberately has
    /// no IInputService dependency -- the owning controller wires its own InputAction
    /// subscriptions and forwards to Handle*, since SwitchToUI/SwitchToGameplay and any
    /// extra side effects (e.g. pausing gameplay) differ per caller.
    /// </summary>
    public sealed class SaveSlotNavigator
    {
        private readonly ISaveSlotListView             view;
        private readonly string                        confirmVerb;
        private readonly Action<int>                   onConfirmSlot;
        private readonly Func<SaveSlotSummary, bool>    canConfirm;
        private readonly Action?                        onClosed;

        private IReadOnlyList<SaveSlotSummary> slots = Array.Empty<SaveSlotSummary>();
        private int  cursorIndex;
        private bool isConfirming;

        public bool IsOpen { get; private set; }
        public bool IsConfirming => this.isConfirming;

        public SaveSlotNavigator(
            ISaveSlotListView          view,
            string                     confirmVerb,
            Action<int>                onConfirmSlot,
            Func<SaveSlotSummary, bool>? canConfirm = null,
            Action?                    onClosed = null)
        {
            this.view          = view;
            this.confirmVerb   = confirmVerb;
            this.onConfirmSlot = onConfirmSlot;
            this.canConfirm    = canConfirm ?? (_ => true);
            this.onClosed      = onClosed;
        }

        public void Open(IReadOnlyList<SaveSlotSummary> slots)
        {
            this.slots        = slots;
            this.cursorIndex  = 0;
            this.isConfirming = false;
            this.IsOpen       = true;
            this.view.Show(this.slots, this.cursorIndex);
        }

        public void HandleNavigate(Vector2 direction)
        {
            if (!this.IsOpen || this.isConfirming || this.slots.Count == 0) return;

            int delta = direction.y > 0.5f ? -1 : direction.y < -0.5f ? 1 : 0;
            if (delta == 0) return;

            this.cursorIndex = (this.cursorIndex + delta + this.slots.Count) % this.slots.Count;
            this.view.Show(this.slots, this.cursorIndex);
        }

        public void HandleConfirm()
        {
            if (!this.IsOpen || this.slots.Count == 0) return;

            if (this.isConfirming)
            {
                // Close (hide the view, switch input maps) before invoking the callback: the
                // callback may trigger a scene load (e.g. loading a save), which can destroy
                // the view's GameObject before we'd get a chance to hide it.
                int slot = this.slots[this.cursorIndex].slot;
                Close();
                this.onConfirmSlot(slot);
                return;
            }

            var summary = this.slots[this.cursorIndex];
            if (!this.canConfirm(summary)) return;

            this.isConfirming = true;
            this.view.ShowConfirm(summary, this.confirmVerb);
        }

        public void HandleBack()
        {
            if (!this.IsOpen) return;

            if (this.isConfirming)
            {
                this.isConfirming = false;
                this.view.Show(this.slots, this.cursorIndex);
                return;
            }

            Close();
        }

        public void Close()
        {
            if (!this.IsOpen) return;
            this.IsOpen       = false;
            this.isConfirming = false;
            this.view.Hide();
            this.onClosed?.Invoke();
        }
    }
}
