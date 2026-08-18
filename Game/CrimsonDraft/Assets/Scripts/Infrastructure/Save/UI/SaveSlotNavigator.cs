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
        private readonly SaveSlotListView              view;
        private readonly string                        confirmVerb;
        private readonly Action<int>                   onConfirmSlot;
        private readonly Func<SaveSlotSummary, bool>    canConfirm;
        private readonly Action?                        onClosed;

        private IReadOnlyList<SaveSlotSummary> slots = Array.Empty<SaveSlotSummary>();
        private int  cursorIndex;
        private bool isConfirming;

        public bool IsOpen { get; private set; }

        public SaveSlotNavigator(
            SaveSlotListView           view,
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
                int slot = this.slots[this.cursorIndex].slot;
                this.onConfirmSlot(slot);
                Close();
                return;
            }

            var summary = this.slots[this.cursorIndex];
            if (!this.canConfirm(summary)) return;

            this.isConfirming = true;
            this.view.ShowConfirm($"{this.confirmVerb} slot {summary.slot + 1}? (Confirm / Cancel)");
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
