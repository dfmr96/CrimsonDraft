#nullable enable

using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class GridCursor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SelectorView    selector     = null!;
        [SerializeField] private HeldItemView    heldView     = null!;
        [SerializeField] private ItemContextMenu contextMenu  = null!;
        [SerializeField] private InspectPanel    inspectPanel = null!;
        [SerializeField] private TabManager?     tabManager;
        [SerializeField] private PartyPanelView? partyPanel;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Inject] private IInputService       inputService = null!;
        [Inject] private InventorySfxData    sfx          = null!;
        [Inject] private IInventoryService   inventory    = null!;
        [Inject] private InventoryPresenters presenters   = null!;

        private GridNavigator? navigator;
        private bool           inputBound;
        private bool           isCombineMode;
        private bool           onMeleeSlot;
        private bool           transferMode;
        private bool           holdingDirection;
        private Vector2Int     lastDir;
        private float          nextMoveTime;

        public event Action<InventoryItemView>? OnCellConfirmed;
        public event Action<InventoryItemView>? OnCombineTargetConfirmed;
        public event Action?                    OnCombineCancelled;
        public event Action?                    OnCloseRequested;

        public bool IsCombineMode
        {
            get => this.isCombineMode;
            set
            {
                this.isCombineMode = value;
                Refresh();
            }
        }

        public bool       IsHoldingItem => this.inventory.Held != null;
        public Vector2Int CurrentCell   => Navigator.Cell;

        public bool IsTransferMode => this.transferMode;
        public bool CanEnterTransferMode => this.presenters.HasStorage;

        public void EnterTransferMode()
        {
            if (!this.presenters.HasStorage)
                throw new System.InvalidOperationException("GridCursor: no StorageWindow is wired into the InventoryGridGroup.");

            this.presenters.RefreshAccess();
            this.transferMode  = true;
            this.isCombineMode = false;
            this.onMeleeSlot   = false;
            this.navigator     = new GridNavigator(
                this.inventory.GetContainer, this.presenters.BuildLinks(), ContainerId.Storage, this.presenters.BuildStorageRows());
            this.navigator.Reset(ContainerId.Storage, Vector2Int.zero);
            this.selector.SetVisible(true);
            Refresh();
        }

        public void ExitTransferMode()
        {
            this.transferMode = false;
            this.navigator    = null;
        }

        private GridNavigator Navigator => this.navigator ??=
            new GridNavigator(this.inventory.GetContainer, this.presenters.BuildLinks(), this.presenters.FirstGrid);

        private ContainerGridPresenter CurrentPresenter => this.presenters.Get(Navigator.Grid);

        private int CurrentOperatorIndex =>
            Navigator.Grid.Kind == ContainerKind.Operator ? Navigator.Grid.Index : -1;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (this.inputService == null || this.inputBound) return;
            this.inputService.InventoryConfirm.performed += OnConfirm;
            this.inputService.InventoryPickup.performed  += OnPickup;
            this.inputBound = true;
        }

        private void OnDisable()
        {
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;
            this.inputService.InventoryPickup.performed  -= OnPickup;
            this.inputBound = false;
        }

        private void Start()
        {
            if (this.contextMenu != null)  this.contextMenu.OnClose  += OnMenuClosed;
            if (this.inspectPanel != null) this.inspectPanel.OnClose += OnInspectClosed;
            this.presenters.Rendered  += OnGridRendered;
            this.inventory.HeldChanged += OnHeldChanged;

            Refresh();

            // OnEnable runs before VContainer injection at scene start — subscribe here if missed.
            OnEnable();
        }

        private void OnDestroy()
        {
            if (this.contextMenu != null)  this.contextMenu.OnClose  -= OnMenuClosed;
            if (this.inspectPanel != null) this.inspectPanel.OnClose -= OnInspectClosed;
            if (this.presenters != null)   this.presenters.Rendered  -= OnGridRendered;
            if (this.inventory != null)    this.inventory.HeldChanged -= OnHeldChanged;
        }

        private void Update()
        {
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;
            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;

            var dir = ReadDirection();
            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                Repeat(dir, NavigateMenu);
                return;
            }

            Repeat(dir, TryMove);
        }

        // ── Navigation ───────────────────────────────────────────────────────

        private void Repeat(Vector2Int dir, Action<Vector2Int> step)
        {
            if (dir == Vector2Int.zero)
            {
                this.holdingDirection = false;
                this.lastDir          = Vector2Int.zero;
                return;
            }

            if (dir != this.lastDir)
            {
                step(dir);
                this.lastDir          = dir;
                this.holdingDirection = true;
                this.nextMoveTime     = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (this.holdingDirection && Time.unscaledTime >= this.nextMoveTime)
            {
                step(dir);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        private void NavigateMenu(Vector2Int dir)
        {
            if (dir.y == 0) return;
            this.contextMenu.NavigateMenu(dir.y);
            this.sfx.PlayCursor(this.gameObject);
        }

        private void TryMove(Vector2Int dir)
        {
            if (this.onMeleeSlot)
            {
                if (dir.y < 0)
                {
                    ExitMeleeSlot();
                    this.sfx.PlayCursor(this.gameObject);
                }
                else if (dir.y > 0)
                {
                    this.tabManager?.EnterTabBar();
                }
                return;
            }

            if (Navigator.Move(dir, this.IsHoldingItem) == NavigationExit.Up)
            {
                if (this.transferMode) return;
                if (!TryEnterMeleeSlot()) this.tabManager?.EnterTabBar();
                return;
            }

            Refresh();
            this.sfx.PlayCursor(this.gameObject);
        }

        private void Refresh()
        {
            if (this.navigator == null && (this.presenters == null || this.inventory == null)) return;
            if (this.onMeleeSlot || !this.selector.IsVisible) return;

            var presenter = CurrentPresenter;
            var cell      = Navigator.Cell;
            var held      = this.inventory.Held;

            if (held != null)
            {
                var footprint = ItemPlacement.FootprintOf(held.Data.GridSize, this.inventory.HeldRotation);
                this.selector.ShowAtCell(presenter.Grid, cell, null, footprint, holdStyle: true, showTooltip: false);
                this.selector.HideTooltip();
                this.heldView.Show(held, this.inventory.HeldRotation, presenter.Grid, cell, CanDropAt(presenter.Container, footprint, cell));
                return;
            }

            this.heldView.Hide();
            bool showTooltip = this.contextMenu == null || !this.contextMenu.IsOpen;
            this.selector.ShowAtCell(presenter.Grid, cell, presenter.ViewAt(cell), null, this.isCombineMode, showTooltip);
        }

        private static bool CanDropAt(ItemContainer container, Vector2Int footprint, Vector2Int origin)
        {
            if (!container.IsWithinBounds(footprint, origin)) return false;
            var overlapping = container.GetOverlapping(footprint, origin);
            return overlapping.Count == 0 || (overlapping.Count == 1 && !overlapping.First().IsEquipped);
        }

        private void OnGridRendered(ContainerId id)
        {
            if (this.navigator != null && id == this.navigator.Grid) Refresh();
        }

        private void OnHeldChanged()
        {
            if (this.navigator != null) Refresh();
        }

        // ── Input callbacks ──────────────────────────────────────────────────

        private void OnConfirm(InputAction.CallbackContext _)
        {
            if (this.tabManager != null && (this.tabManager.IsTabBarActive || this.tabManager.IsConsumingTabInput)) return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;

            if (this.transferMode)
            {
                if (this.IsHoldingItem) Drop();
                else                    PickUpAtCursor();
                return;
            }

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx.PlayDecide(this.gameObject);
                this.contextMenu.ConfirmSelection();
                return;
            }

            if (this.onMeleeSlot)
            {
                ConfirmMeleeSlot();
                return;
            }

            if (this.IsHoldingItem)
            {
                Drop();
                return;
            }

            var view = CurrentPresenter.ViewAt(Navigator.Cell);
            if (!this.inventory.IsCarried(Navigator.Grid))
            {
                if (this.isCombineMode) this.sfx.PlayInvalidAction(this.gameObject);
                else                    PickUpAtCursor();
                return;
            }

            if (this.isCombineMode)
            {
                if (view != null) OnCombineTargetConfirmed?.Invoke(view);
                return;
            }

            if (view == null)
            {
                this.sfx.PlayInvalidAction(this.gameObject);
                return;
            }

            this.sfx.PlayDecide(this.gameObject);
            OnCellConfirmed?.Invoke(view);
            this.selector.ShowTooltipAboveSelector();
        }

        private void OnPickup(InputAction.CallbackContext _)
        {
            if (this.onMeleeSlot) return;
            if (this.contextMenu != null && this.contextMenu.IsOpen)   return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;

            if (this.IsHoldingItem)
            {
                this.inventory.RotateHeld();
                this.sfx.PlayCursor(this.gameObject);
                return;
            }

            PickUpAtCursor();
        }

        private void PickUpAtCursor()
        {
            var container = CurrentPresenter.Container;
            var item      = container.GetItemAt(Navigator.Cell);
            if (item == null) return;

            var origin = container.GetPlacement(item)!.Origin;
            if (!this.inventory.TryPickUp(item))
            {
                this.sfx.PlayCancel(this.gameObject);
                return;
            }

            Navigator.Reset(Navigator.Grid, origin);
            this.sfx.PlayDecide(this.gameObject);
            Refresh();
        }

        private void Drop()
        {
            var result = this.inventory.TryDrop(Navigator.Grid, Navigator.Cell);
            if (result == DropResult.Rejected) this.sfx.PlayCancel(this.gameObject);
            else                               this.sfx.PlayDecide(this.gameObject);
            Refresh();
        }

        private void OnMenuClosed()
        {
            this.holdingDirection = false;
            this.lastDir          = Vector2Int.zero;
        }

        private void OnInspectClosed(string? selectItemId)
        {
            this.holdingDirection = false;
            this.lastDir          = Vector2Int.zero;

            if (selectItemId != null && SelectItemById(selectItemId)) return;

            var widget = this.onMeleeSlot ? CurrentWidget() : null;
            if (widget != null) this.selector.ShowOnMelee(widget);
            else                Refresh();
        }

        // ── Public API (TabManager / OpenClose / HUD) ────────────────────────

        // Called by TabManager.OnCancelTab — the sole subscriber to InventoryCancel — so this
        // tab's own submenus/held-item state get first chance to consume Cancel.
        public bool TryConsumeCancel()
        {
            if (this.transferMode)
            {
                if (this.IsHoldingItem) Drop();
                else                    RequestClose();
                return true;
            }

            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
            {
                this.inspectPanel.Close();
                return true;
            }

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx.PlayCancel(this.gameObject);
                this.contextMenu.Close();
                return true;
            }

            if (this.onMeleeSlot)
            {
                ExitMeleeSlot();
                this.sfx.PlayCancel(this.gameObject);
                return true;
            }

            if (this.isCombineMode)
            {
                this.IsCombineMode = false;
                OnCombineCancelled?.Invoke();
                this.sfx.PlayCancel(this.gameObject);
                return true;
            }

            if (this.IsHoldingItem)
            {
                if (this.inventory.HeldIsSplit)
                {
                    this.inventory.CancelHeld();
                    this.sfx.PlayCancel(this.gameObject);
                }
                else
                {
                    Drop();
                }
                return true;
            }

            return false;
        }

        public void RequestClose() => OnCloseRequested?.Invoke();

        public void CancelAll()
        {
            if (this.isCombineMode)
            {
                this.isCombineMode = false;
                OnCombineCancelled?.Invoke();
            }

            this.inventory.CancelHeld();
            if (this.contextMenu != null && this.contextMenu.IsOpen)   this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) this.inspectPanel.Close();
            if (this.onMeleeSlot) ExitMeleeSlot();
            this.tabManager?.ResetTabBar();
            this.heldView.Hide();
            this.selector.HideTooltip();
            this.holdingDirection = false;
            this.lastDir          = Vector2Int.zero;
        }

        public void ResetCursorToOrigin()
        {
            if (!this.transferMode) this.navigator = null;
            this.presenters.RefreshAccess();
            Navigator.Reset(this.presenters.FirstGrid, Vector2Int.zero);
            this.holdingDirection = false;
            this.lastDir          = Vector2Int.zero;
            this.onMeleeSlot      = false;
            this.selector.SetVisible(true);
            Refresh();
        }

        public void HideSelectorForTabBar()
        {
            this.selector.SetVisible(false);
            this.holdingDirection = false;
            this.lastDir          = Vector2Int.zero;
        }

        public void ShowSelectorAfterTabBar()
        {
            Navigator.Reset(Navigator.Grid, new Vector2Int(Navigator.Cell.x, 0));
            this.selector.SetVisible(true);
            Refresh();
        }

        // Moves the cursor to the first item with the given id -- used after InspectPanel
        // grants a reward so the player lands right on it.
        public bool SelectItemById(string itemId)
        {
            foreach (var container in this.inventory.OperatorContainers)
            {
                if (!this.presenters.Has(container.Id) || !this.inventory.IsAccessible(container.Id)) continue;
                var placement = container.Placements.FirstOrDefault(p => p.Item.Data.ItemId == itemId);
                if (placement == null) continue;

                this.onMeleeSlot = false;
                Navigator.Reset(container.Id, placement.Origin);
                Refresh();
                return true;
            }
            return false;
        }

        // ── Melee slot ───────────────────────────────────────────────────────

        private OperatorWidgetView? CurrentWidget()
        {
            int op = CurrentOperatorIndex;
            return op >= 0 && this.partyPanel != null ? this.partyPanel.GetWidget(op) : null;
        }

        private bool TryEnterMeleeSlot()
        {
            if (!this.inventory.IsCarried(Navigator.Grid)) return false;
            var widget = CurrentWidget();
            if (widget == null || !widget.HasMeleeWeapon) return false;

            this.onMeleeSlot = true;
            this.selector.ShowOnMelee(widget);
            return true;
        }

        private void ExitMeleeSlot()
        {
            this.onMeleeSlot = false;
            this.selector.ClearMeleeHover();
            Refresh();
        }

        private void ConfirmMeleeSlot()
        {
            var widget    = CurrentWidget();
            var meleeData = widget != null ? widget.MeleeData : null;
            if (widget == null || meleeData == null || this.contextMenu == null) return;

            this.sfx.PlayDecide(this.gameObject);
            this.contextMenu.OpenForMeleeInspectOnly(widget.MeleeSlotRoot, meleeData);
        }

        // ── Input reading ────────────────────────────────────────────────────

        private Vector2Int ReadDirection()
        {
            Vector2 raw = this.inputService.InventoryNavigate.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.01f) return Vector2Int.zero;

            if (Mathf.Abs(raw.x) >= Mathf.Abs(raw.y)) return raw.x > 0 ? Vector2Int.right : Vector2Int.left;
            return raw.y > 0 ? Vector2Int.up : Vector2Int.down;
        }
    }
}
