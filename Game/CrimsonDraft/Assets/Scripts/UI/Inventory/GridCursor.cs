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
        private bool           pickupHighlight; // the selector rests on a world pickup's item, not on the cursor's cell
        private bool           onLogoSlot;
        private bool           transferMode;
        private bool           holdingDirection;
        private Vector2Int     lastDir;
        private float          nextMoveTime;

        public event Action<InventoryItemView>? OnCellConfirmed;
        public event Action<InventoryItemView>? OnCombineTargetConfirmed;
        public event Action?                    OnCombineCancelled;
        public event Action?                    OnCloseRequested;

        public SelectorView Selector => this.selector;

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
            this.onLogoSlot    = false;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            this.inputService.InventoryNextTab.performed += OnDebugNextItem;
            this.inputService.InventoryPrevTab.performed += OnDebugPrevItem;
#endif
            this.inputBound = true;
        }

        private void OnDisable()
        {
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;
            this.inputService.InventoryPickup.performed  -= OnPickup;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            this.inputService.InventoryNextTab.performed -= OnDebugNextItem;
            this.inputService.InventoryPrevTab.performed -= OnDebugPrevItem;
#endif
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
            if (this.onLogoSlot)
            {
                if (dir.y > 0)
                {
                    this.tabManager?.EnterTabBar();
                }
                else if (dir.y < 0)
                {
                    // Bio page has no grid underneath: Down only works on the inventory page.
                    if (this.partyPanel != null && this.partyPanel.BioMode) return;
                    ExitLogoSlot();
                    this.sfx.PlayCursor(this.gameObject);
                }
                else
                {
                    MoveLogo(dir.x);
                }
                return;
            }

            if (Navigator.Move(dir, this.IsHoldingItem) == NavigationExit.Up)
            {
                if (this.transferMode) return;
                if (!TryEnterLogoSlot()) this.tabManager?.EnterTabBar();
                return;
            }

            Refresh();
            this.sfx.PlayCursor(this.gameObject);
        }

        private void Refresh()
        {
            if (this.pickupHighlight) return;
            if (this.navigator == null && (this.presenters == null || this.inventory == null)) return;
            if (this.onLogoSlot || !this.selector.IsVisible) return;

            var presenter = CurrentPresenter;
            var cell      = Navigator.Cell;
            var held      = this.inventory.Held;

            if (held != null)
            {
                var footprint = ItemPlacement.FootprintOf(held.Data.GridSize, this.inventory.HeldRotation);
                this.selector.ShowAtCell(presenter.Grid, cell, null, footprint, holdStyle: true, showTooltip: false);
                // A spawned (debug) item is named while it is cycled; a picked-up one needs no label.
                if (this.inventory.HeldIsSpawned) this.selector.ShowTooltipAtSelector(held.Data.DisplayName);
                else                              this.selector.HideTooltip();
                this.heldView.Show(held, this.inventory.HeldRotation, presenter.Grid, cell,
                    CanDropAt(presenter.Container, footprint, cell, allowSwap: !this.inventory.HeldIsSpawned));
                return;
            }

            this.heldView.Hide();
            bool showTooltip = this.contextMenu == null || !this.contextMenu.IsOpen;
            this.selector.ShowAtCell(presenter.Grid, cell, presenter.ViewAt(cell), null, this.isCombineMode, showTooltip);
        }

        private static bool CanDropAt(ItemContainer container, Vector2Int footprint, Vector2Int origin, bool allowSwap)
        {
            if (!container.IsWithinBounds(footprint, origin)) return false;
            var overlapping = container.GetOverlapping(footprint, origin);
            return overlapping.Count == 0 || (allowSwap && overlapping.Count == 1 && !overlapping.First().IsEquipped);
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

            if (this.onLogoSlot)
            {
                ConfirmLogo();
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
            if (this.onLogoSlot) return;
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

            Refresh();
        }

        // ── Public API (TabManager / OpenClose / HUD) ────────────────────────

        // Called by TabManager.OnCancelTab — the sole subscriber to InventoryCancel — so this
        // tab's own submenus/held-item state get first chance to consume Cancel.
        public bool TryConsumeCancel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TryDiscardDebugItem()) return true;
#endif
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
                if (!this.contextMenu.TryBackFromTargets()) this.contextMenu.Close();
                return true;
            }

            // The Bio page is a sub menu of the logo: Cancel closes it first and the cursor stays on the logo.
            if (this.onLogoSlot && this.partyPanel != null && this.partyPanel.BioMode)
            {
                this.partyPanel.SetBioMode(false);
                this.sfx.PlayCancel(this.gameObject);
                return true;
            }

            // The logo slot is just a place the cursor can rest, not sub menus: Cancel there
            // is not consumed, so TabManager moves up to the tab bar (the same as pressing Up there)
            // and one more Cancel closes the inventory.

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // ── Debug item spawner (RE 1.5 style) ────────────────────────────────
        // Hold Select (Tab) and press R1/L1 (E/Q) to put a catalog item in the hand and cycle it;
        // releasing Select keeps the item. Select + Cancel throws a spawned item away.

        [Inject] private IObjectResolver resolver = null!;
        private DebugItemCatalog? debugCatalog;

        private static bool DebugModifierHeld =>
            (Gamepad.current != null && Gamepad.current.selectButton.isPressed)
            || (Keyboard.current != null && Keyboard.current.tabKey.isPressed);

        private void OnDebugNextItem(InputAction.CallbackContext _) => CycleDebugItem(1);
        private void OnDebugPrevItem(InputAction.CallbackContext _) => CycleDebugItem(-1);

        private void CycleDebugItem(int delta)
        {
            if (!DebugModifierHeld || !CanSpawnDebugItem()) return;

            // Never replace an item the player picked up: it would be lost.
            if (this.IsHoldingItem && !this.inventory.HeldIsSpawned)
            {
                this.sfx.PlayInvalidAction(this.gameObject);
                return;
            }

            if (this.debugCatalog == null && this.resolver.TryResolve<ItemDatabase>(out var database))
                this.debugCatalog = new DebugItemCatalog(database.AllItems);
            if (this.debugCatalog == null) return;

            if (!this.inventory.HeldIsSpawned) this.debugCatalog.Reset();
            var data = this.debugCatalog.Step(delta);
            if (data == null || !this.inventory.TrySpawnHeld(DebugItemCatalog.CreateFull(data))) return;

            this.sfx.PlayCursor(this.gameObject);
            Refresh();
        }

        private bool CanSpawnDebugItem() =>
            !this.onLogoSlot && !this.isCombineMode && this.selector.IsVisible
            && (this.tabManager == null || (!this.tabManager.IsTabBarActive && !this.tabManager.IsConsumingTabInput))
            && (this.contextMenu == null || !this.contextMenu.IsOpen)
            && (this.inspectPanel == null || !this.inspectPanel.IsOpen);

        private bool TryDiscardDebugItem()
        {
            if (!this.inventory.HeldIsSpawned || !DebugModifierHeld) return false;
            this.inventory.CancelHeld();
            this.sfx.PlayCancel(this.gameObject);
            return true;
        }
#endif

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
            this.onLogoSlot = false;
            this.partyPanel?.SetBioMode(false, animate: false);
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
            this.onLogoSlot       = false;
            this.pickupHighlight  = false;
            this.partyPanel?.SetBioMode(false, animate: false);
            this.selector.SetVisible(true);
            Refresh();
        }

        /// <summary>
        /// Parks the selector on an item drawn for a world pickup's "take it?" prompt (the ghost or the stack it
        /// joins). The cursor's own cell is left alone and the selector stays there until
        /// <see cref="ClearPickupHighlight"/>.
        /// </summary>
        public void ShowPickupHighlight(InventoryItemView view, InventoryGrid grid)
        {
            this.pickupHighlight = true;
            this.selector.SetVisible(true);
            this.selector.ShowAtCell(grid, view.GridOrigin, view, null, false, false);
        }

        public void ClearPickupHighlight()
        {
            if (!this.pickupHighlight) return;
            this.pickupHighlight = false;
            this.selector.SetVisible(false);
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

                this.onLogoSlot  = false;
                this.partyPanel?.SetBioMode(false, animate: false);
                Navigator.Reset(container.Id, placement.Origin);
                Refresh();
                return true;
            }
            return false;
        }

        // ── Operator widget ──────────────────────────────────────────────────

        private OperatorWidgetView? CurrentWidget()
        {
            int op = CurrentOperatorIndex;
            return op >= 0 && this.partyPanel != null ? this.partyPanel.GetWidget(op) : null;
        }

        // ── Logo slot ────────────────────────────────────────────────────────

        // The logo sits above the grid: grid -> logo -> tab bar. It is skipped
        // while an item is held or a combine is pending, so those flows can't be interrupted.
        private bool TryEnterLogoSlot()
        {
            if (this.transferMode || this.IsHoldingItem || this.isCombineMode) return false;
            var widget = CurrentWidget();
            if (widget == null || !widget.HasLogo) return false;

            this.onLogoSlot  = true;
            this.selector.ShowOnLogo(widget);
            return true;
        }

        private void ExitLogoSlot()
        {
            this.onLogoSlot = false;
            Refresh();
        }

        // Left/Right hops to the neighbouring card's logo (the only way to move sideways on the Bio page).
        private void MoveLogo(int dx)
        {
            if (dx == 0 || !this.presenters.BuildLinks().TryGetValue(Navigator.Grid, out var links)) return;

            var next = dx > 0 ? links.Right : links.Left;
            if (next == null) return;

            var previous = Navigator.Grid;
            Navigator.Reset(next.Value, Navigator.Cell);
            var widget = CurrentWidget();
            if (widget == null || !widget.HasLogo)
            {
                Navigator.Reset(previous, Navigator.Cell);
                return;
            }

            this.selector.ShowOnLogo(widget);
            this.sfx.PlayCursor(this.gameObject);
        }

        private void ConfirmLogo()
        {
            if (this.partyPanel == null) return;
            this.sfx.PlayDecide(this.gameObject);
            this.partyPanel.ToggleBio();
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
