#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using Yarn.Unity;
using CrimsonDraft.Rendering.Outline;

namespace CrimsonDraft.Navigation.Interactables
{
    // Cursor navigation inside the jewelry box's inspection view: one stop per gem slot (an
    // empty slot shows a transparent "ghost" of the gem, outlined with the shared selection
    // outline), plus the Dog Key once the yellow gem has opened its drawer.
    //
    // Gems themselves are placed from the inventory, outside the inspection, through the
    // box's ItemSocketInteractable (one required item per slot -- slots[i] must match
    // requiredItems[i]). This panel only reacts to SlotFilled: swaps the ghost for the real
    // gem and slides that slot's box open on local Z. The key slot's box additionally makes
    // the key pickable once it has finished moving.
    public sealed class JewelryBoxPanel : MonoBehaviour
    {
        private const float NavCooldown = 0.2f;

        [Serializable]
        private sealed class Slot
        {
            public Transform  ghost = null!;  // GemXxx_Socket -- transparent placeholder
            public GameObject gem   = null!;  // GemXxx -- the real gem, shown once placed
            public Transform  box   = null!;  // drawer that slides open
        }

        [SerializeField] private ItemSocketInteractable socket = null!;
        [SerializeField] private Slot[] slots = Array.Empty<Slot>();

        [SerializeField] private float boxOpenLocalZ = 0.5f;
        [SerializeField, Min(0f)] private float boxOpenDuration = 0.6f;

        // Opening this slot's box (the yellow gem's) enables the key below.
        [SerializeField] private int keySlotIndex = 1;
        [SerializeField] private PickupInteractable? keyPickup;

        [SerializeField] private DialogueReference emptySocketDialogue = new();

        private readonly SelectionOutlineHighlight highlight = new();
        private readonly bool[] filled = new bool[8];

        private InteractionContext? context;
        private bool  isActive;
        private bool  isBusy;
        private bool  keyAvailable;
        private int   cursor;
        private int   lastDialogueEndFrame = -1;
        private float lastNavTime = float.MinValue;

        void Awake()
        {
            foreach (var slot in this.slots)
            {
                slot.gem.SetActive(false);
                slot.ghost.gameObject.SetActive(true);
            }

            this.socket.SlotFilled += OnSlotFilled;
        }

        void OnDestroy()
        {
            if (this.socket != null) this.socket.SlotFilled -= OnSlotFilled;
            UnsubscribeInput();
        }

        // ── Slot state ─────────────────────────────────────────────────────

        private void OnSlotFilled(int index, bool animate)
        {
            if (index < 0 || index >= this.slots.Length) return;

            this.filled[index] = true;
            var slot = this.slots[index];
            slot.ghost.gameObject.SetActive(false);
            slot.gem.SetActive(true);

            OpenBoxAsync(index, animate).Forget();
        }

        private async UniTaskVoid OpenBoxAsync(int index, bool animate)
        {
            var box    = this.slots[index].box;
            var from   = box.localPosition;
            var target = new Vector3(from.x, from.y, this.boxOpenLocalZ);

            if (animate && this.boxOpenDuration > 0f)
            {
                float t = 0f;
                while (t < this.boxOpenDuration)
                {
                    t += Time.deltaTime;
                    box.localPosition = Vector3.Lerp(from, target, Mathf.Clamp01(t / this.boxOpenDuration));
                    await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
                }
            }
            box.localPosition = target;

            if (index == this.keySlotIndex)
                this.keyAvailable = true;
        }

        // ── Activation ─────────────────────────────────────────────────────

        public void Activate(InteractionContext ctx)
        {
            if (this.isActive) return;

            this.context     = ctx;
            this.isActive    = true;
            this.isBusy      = false;
            this.cursor      = 0;
            this.lastNavTime = float.MinValue;

            ctx.InputService.UINavigate.performed += OnNavigate;
            ctx.InputService.UIConfirm.performed  += OnConfirm;
            UpdateHighlight();
        }

        public void Deactivate()
        {
            if (!this.isActive) return;

            this.isActive = false;
            UnsubscribeInput();
            this.highlight.Clear();
            this.context = null;
        }

        private void UnsubscribeInput()
        {
            if (this.context == null) return;
            this.context.InputService.UINavigate.performed -= OnNavigate;
            this.context.InputService.UIConfirm.performed  -= OnConfirm;
        }

        // ── Targets ────────────────────────────────────────────────────────

        // Slots first (in order), then the key if it's currently pickable.
        private List<Transform> BuildTargets(out int keyIndex)
        {
            var targets = new List<Transform>();
            for (int i = 0; i < this.slots.Length; i++)
                targets.Add(this.filled[i] ? this.slots[i].gem.transform : this.slots[i].ghost);

            keyIndex = -1;
            if (this.keyAvailable && this.keyPickup != null && this.keyPickup.gameObject.activeSelf)
            {
                keyIndex = targets.Count;
                targets.Add(this.keyPickup.transform);
            }
            return targets;
        }

        private void UpdateHighlight()
        {
            var targets = BuildTargets(out _);
            if (targets.Count == 0) return;
            this.cursor = Mathf.Clamp(this.cursor, 0, targets.Count - 1);
            this.highlight.Show(targets[this.cursor]);
        }

        // ── Input ──────────────────────────────────────────────────────────

        private void OnNavigate(InputAction.CallbackContext ctx)
        {
            if (!this.isActive || this.isBusy) return;
            if (Time.unscaledTime - this.lastNavTime < NavCooldown) return;

            var dir  = ctx.ReadValue<Vector2>();
            int step = dir.x > 0.5f || dir.y < -0.5f ? 1
                     : dir.x < -0.5f || dir.y > 0.5f ? -1
                     : 0;
            if (step == 0) return;

            var targets = BuildTargets(out _);
            this.cursor = Mathf.Clamp(this.cursor + step, 0, targets.Count - 1);
            this.lastNavTime = Time.unscaledTime;
            UpdateHighlight();
        }

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (!this.isActive || this.isBusy || this.context == null) return;
            // The Confirm that dismisses a dialogue line also reaches us -- ignore it.
            if (this.lastDialogueEndFrame == Time.frameCount) return;

            BuildTargets(out int keyIndex);
            var ctx = this.context;

            if (this.cursor == keyIndex && this.keyPickup != null)
            {
                this.isBusy = true;
                // Force-clear so the outline isn't left on an object the pickup disables.
                this.highlight.Clear();
                this.keyPickup.Interact(ctx, onClosed: ResumeAfterDialogue);
                return;
            }

            if (this.cursor < this.slots.Length && !this.filled[this.cursor])
            {
                this.isBusy = true;
                ctx.DialogueService.StartDialogue(
                    this.emptySocketDialogue.nodeName ?? "",
                    onComplete: ResumeAfterDialogue);
            }
        }

        // The dialogue service hands input back to Gameplay when it ends (see
        // GeneratorSwitchPanel.ShowMissingPowerPrompt) -- put the UI map back so the cursor
        // keeps working inside the still-open inspection camera.
        private void ResumeAfterDialogue()
        {
            this.lastDialogueEndFrame = Time.frameCount;
            this.isBusy = false;
            if (!this.isActive || this.context == null) return;

            this.context.InputService.SwitchToUI();
            UpdateHighlight();
        }
    }
}
