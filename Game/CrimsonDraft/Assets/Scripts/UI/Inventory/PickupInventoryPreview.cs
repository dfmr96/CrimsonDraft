#nullable enable

using System;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// The "take it?" preview of a world pickup, drawn in the inventory itself: the inventory opens
    /// behind the prompt and the item sits, ghosted, in the exact cell it would take (so the player
    /// sees both where it goes and how much room it uses). A pickup that only tops up a stack
    /// pulses that stack instead. The ghost goes away the moment the inventory really changes.
    /// </summary>
    public sealed class PickupInventoryPreview : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private Color ghostTint = new Color(154f / 255f, 159f / 255f, 92f / 255f, 1f);
        [SerializeField] private float pulseMinAlpha = 0.35f;
        [SerializeField] private float pulseMaxAlpha = 1f;
        [SerializeField] private float pulseSpeed    = 4f;

        [Inject] private ISubscriber<PickupPreviewStartedEvent> startedSubscriber = null!;
        [Inject] private ISubscriber<PickupPreviewEndedEvent>   endedSubscriber   = null!;
        [Inject] private IInventoryService                      inventory         = null!;
        [Inject] private InventoryOpenCloseController           openClose         = null!;
        [Inject] private InventoryPresenters                    presenters        = null!;
        [Inject] private InventoryGridGroup                     gridGroup         = null!;
        [Inject] private InspectPanel                           inspectPanel      = null!;
        [Inject] private IInputService                          input             = null!;
        [Inject] private GridCursor                             cursor            = null!;

        private IDisposable? startedSubscription;
        private IDisposable? endedSubscription;

        private InventoryItemView? ghost;     // a new item/stack, drawn where it would land
        private InventoryItemView? pulsing;   // or the existing stack the quantity would join
        private bool               openedByPreview;
        private Tween?             pendingClose; // the inventory closing once the Inspect panel has shrunk away

        public void Initialize()
        {
            this.startedSubscription = this.startedSubscriber.Subscribe(OnStarted);
            this.endedSubscription   = this.endedSubscriber.Subscribe(_ => OnEnded());
            this.inventory.ContentsChanged += OnContentsChanged;
        }

        public void Dispose()
        {
            this.startedSubscription?.Dispose();
            this.endedSubscription?.Dispose();
            this.inventory.ContentsChanged -= OnContentsChanged;
        }

        private void OnStarted(PickupPreviewStartedEvent e)
        {
            this.cursor.ClearPickupHighlight();
            Clear();
            EndOpened();

            if (!this.openClose.IsOpen)
            {
                this.openClose.OpenForPickup();
                this.openedByPreview = true;
            }

            ShowWhereItWouldLand(e.Item);

            // Model + name up top; the prompt the pickup starts next shows in the same panel. It pops
            // out of the ghost (or the stack it merges into) so the player sees where the item goes.
            var origin = this.ghost != null ? this.ghost : this.pulsing;
            this.inspectPanel.OpenForPickup(e.Item, origin != null ? (RectTransform)origin.transform : null);

            // The cursor's selector marks the item too, on top of its blink.
            var grid = origin != null ? origin.GetComponentInParent<InventoryGrid>() : null;
            if (origin != null && grid != null) this.cursor.ShowPickupHighlight(origin, grid);
        }

        private void ShowWhereItWouldLand(ItemData item)
        {
            var preview = this.inventory.PreviewAdd(item);
            if (preview == null || !this.presenters.Has(preview.Value.Container)) return;

            var grid = this.presenters.Get(preview.Value.Container).Grid;
            if (preview.Value.MergeTarget != null)
            {
                this.pulsing = this.presenters.FindView(preview.Value.MergeTarget);
                return;
            }

            // The ghost is the item as it would be placed (unrotated, like TryAdd), never added to a container.
            var placement = new ItemPlacement(InventoryItemFactory.Create(item), preview.Value.Origin, 0);
            this.ghost = Instantiate(this.gridGroup.ItemViewPrefab, grid.transform);
            this.ghost.Bind(placement, grid);
            this.ghost.transform.SetAsLastSibling();
            this.ghost.SetTint(this.ghostTint);
        }

        private void OnEnded()
        {
            bool owned = this.openedByPreview;
            this.cursor.ClearPickupHighlight();
            Clear();
            this.inspectPanel.ClosePickup();

            // The inventory stays up just long enough for the panel to shrink back into the grid.
            if (owned)
            {
                this.pendingClose?.Kill();
                this.pendingClose = DOVirtual.DelayedCall(this.inspectPanel.CloseDuration, EndOpened, ignoreTimeScale: true);
            }

            // The Inspect runner hands input back to the inventory when its prompt ends; this
            // pickup never opened it for the player, so it goes straight back to the game.
            if (owned) this.input.SwitchToGameplay();
        }

        // The item was taken (or the grid changed some other way): the real thing is drawn now.
        private void OnContentsChanged() => Clear();

        private void EndOpened()
        {
            this.pendingClose?.Kill();
            this.pendingClose = null;
            if (!this.openedByPreview) return;
            this.openedByPreview = false;
            this.openClose.ClosePickup();
        }

        private void Clear()
        {
            if (this.ghost != null) Destroy(this.ghost.gameObject);
            if (this.pulsing != null) this.pulsing.ResetTint();
            this.ghost   = null;
            this.pulsing = null;
        }

        private void Update()
        {
            var view = this.ghost != null ? this.ghost : this.pulsing;
            if (view == null) return;

            float t     = Mathf.Sin(Time.unscaledTime * this.pulseSpeed) * 0.5f + 0.5f;
            var   color = this.ghost != null ? this.ghostTint : Color.white;
            color.a = Mathf.Lerp(this.pulseMinAlpha, this.pulseMaxAlpha, t);
            view.SetTint(color);
        }
    }
}
