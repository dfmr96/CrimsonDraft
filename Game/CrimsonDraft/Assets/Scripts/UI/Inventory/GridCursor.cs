#nullable enable

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class GridCursor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryGridGroup  gridGroup    = null!;
        [SerializeField] private RectTransform       selectorRect = null!;
        [SerializeField] private ItemContextMenu     contextMenu  = null!;
        [SerializeField] private ItemTooltip         tooltip      = null!;
        [SerializeField] private InspectPanel        inspectPanel = null!;
        [SerializeField] private TabManager?         tabManager;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Selector Sizing")]
        [SerializeField] private float selectorPadding = 1f;

        [Header("Hold Colors")]
        [SerializeField] private Color colorHoldTint    = new Color(154f / 255f, 159f / 255f, 92f / 255f, 1f); // #9A9F5C
        [SerializeField] private float alphaCannotPlace = 100f / 255f;
        [SerializeField] private Color colorNormalItem  = Color.white;

        [Header("Selector Sprites")]
        [SerializeField] private Sprite? selectorSpriteNormal;
        [SerializeField] private Sprite? selectorSpriteHold; // shown while moving/combining

        [Inject] private IInputService    inputService = null!;
        [Inject] private InventorySfxData sfx          = null!;
        private bool inputBound;

        // Combine mode — set by InventoryHUDController
        private bool isCombineMode;
        public bool IsCombineMode
        {
            get => this.isCombineMode;
            set
            {
                this.isCombineMode = value;
                PlaceSelectorAt(this.currentCell);
            }
        }

        // Events consumed by InventoryHUDController
        public event System.Action<InventoryItemView>?               OnCellConfirmed;
        public event System.Action<InventoryItemView>?               OnCombineTargetConfirmed;
        public event System.Action<InventoryItemView, InventoryGrid>? OnItemMovedToNewGrid;
        public event System.Action<InventoryItemView>?               OnItemPlaced;
        public event System.Action?                                   OnCombineCancelled;
        public event System.Action?                                   OnCloseRequested;
        public event System.Action<InventoryItemView>?                OnSplitCancelled;

        // Grid state
        private int        currentGridIndex;
        private Vector2Int currentCell;
        private Image      selectorImage = null!;
        private Vector2Int lastDir;
        private float      nextMoveTime;
        private bool       holding;

        // Held item state
        private InventoryItemView? heldItem;
        private InventoryGrid?     heldFromGrid;
        private bool               isSplitPhantomHeld;

        private InventoryGrid CurrentGrid => this.gridGroup.GetGrid(this.currentGridIndex);

        private static readonly Color ColorSelectorNormal = Color.white;
        private static readonly Color ColorSelectorOnItem = Color.yellow;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            if (this.gridGroup == null)
                this.gridGroup = GetComponentInParent<InventoryGridGroup>();
        }

        void OnEnable()
        {
            if (this.inputService == null || this.inputBound) return;
            this.inputService.InventoryConfirm.performed += OnConfirm;
            this.inputService.InventoryCancel.performed  += OnCancel;
            this.inputService.InventoryPickup.performed  += OnPickup;
            this.inputBound = true;
        }

        void OnDisable()
        {
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.InventoryConfirm.performed -= OnConfirm;
            this.inputService.InventoryCancel.performed  -= OnCancel;
            this.inputService.InventoryPickup.performed  -= OnPickup;
            this.inputBound = false;
        }

        void Start()
        {
            this.selectorImage = this.selectorRect.GetComponent<Image>();

            if (this.contextMenu != null)
                this.contextMenu.OnClose += OnMenuClosed;
            if (this.inspectPanel != null) this.inspectPanel.OnClose += OnInspectClosed;

            AttachSelectorToGrid(CurrentGrid);
            PlaceSelectorAt(this.currentCell);

            // OnEnable runs before VContainer injection at scene start — subscribe here if missed
            OnEnable();
        }

        // ── Update ───────────────────────────────────────────────────────────

        void Update()
        {
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
                return;

            // Tab bar has focus — grid cursor yields
            if (this.tabManager != null && this.tabManager.IsTabBarActive)
                return;

            Vector2Int dir = ReadDirection();

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                HandleMenuNavigation(dir);
                return;
            }

            HandleGridNavigation(dir);

            if (this.heldItem != null)
                UpdateHeldItemVisual();
        }

        // ── Navigation ───────────────────────────────────────────────────────

        void HandleGridNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero)
            {
                this.holding = false;
                this.lastDir = Vector2Int.zero;
                return;
            }

            if (dir != this.lastDir)
            {
                TryMove(dir);
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                TryMove(dir);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        void HandleMenuNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero)
            {
                this.holding = false;
                this.lastDir = Vector2Int.zero;
                return;
            }

            if (dir.y != 0 && dir != this.lastDir)
            {
                this.contextMenu.NavigateMenu(dir.y);
                this.sfx?.PlayCursor(gameObject);
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (dir.y != 0 && this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                this.contextMenu.NavigateMenu(dir.y);
                this.sfx?.PlayCursor(gameObject);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        void TryMove(Vector2Int dir)
        {
            Vector2Int next = this.currentCell + new Vector2Int(dir.x, -dir.y);

            // When not holding an item, skip to the far edge of the current item
            // so multi-cell items don't require multiple presses to navigate past them.
            if (this.heldItem == null)
            {
                InventoryItemView? underCursor = CurrentGrid.GetItemAt(this.currentCell);
                if (underCursor != null)
                {
                    Vector2Int o = underCursor.GridOrigin;
                    Vector2Int s = underCursor.GridSize;

                    if      (dir.x > 0) next.x = o.x + s.x;      // skip past right edge
                    else if (dir.x < 0) next.x = o.x - 1;         // skip past left edge
                    else if (dir.y < 0) next.y = o.y + s.y;       // skip past bottom edge (screen down = grid +y)
                    else if (dir.y > 0) next.y = o.y - 1;         // skip past top edge
                }
            }

            next.y = ((next.y % CurrentGrid.Rows) + CurrentGrid.Rows) % CurrentGrid.Rows;

            if (next.x < 0)
            {
                this.currentGridIndex = (this.currentGridIndex - 1 + this.gridGroup.Count) % this.gridGroup.Count;
                next.x = CurrentGrid.Columns - 1;
                next.y = Mathf.Clamp(next.y, 0, CurrentGrid.Rows - 1);
                AttachSelectorToGrid(CurrentGrid);
            }
            else if (next.x >= CurrentGrid.Columns)
            {
                this.currentGridIndex = (this.currentGridIndex + 1) % this.gridGroup.Count;
                next.x = 0;
                next.y = Mathf.Clamp(next.y, 0, CurrentGrid.Rows - 1);
                AttachSelectorToGrid(CurrentGrid);
            }

            this.currentCell = next;
            PlaceSelectorAt(this.currentCell);

            this.sfx?.PlayCursor(gameObject);
        }

        // ── Input Callbacks ──────────────────────────────────────────────────

        void OnConfirm(InputAction.CallbackContext ctx)
        {
            if (this.tabManager != null && (this.tabManager.IsTabBarActive || this.tabManager.IsConsumingTabInput)) return;

            if (this.IsCombineMode)
            {
                InventoryItemView? target = CurrentGrid.GetItemAt(this.currentCell);
                if (target != null)
                    OnCombineTargetConfirmed?.Invoke(target);
                return;
            }

            if (this.heldItem != null)
            {
                this.heldItem.Rotate();
                this.sfx?.PlayCursor(gameObject);
                UpdateHeldItemVisual();
                PlaceSelectorAt(this.currentCell);
                return;
            }

            if (this.contextMenu == null) return;

            if (this.contextMenu.IsOpen)
            {
                this.sfx?.PlayDecide(gameObject);
                this.contextMenu.ConfirmSelection();
                return;
            }

            InventoryItemView? item = CurrentGrid.GetItemAt(this.currentCell);
            if (item != null)
            {
                this.sfx?.PlayDecide(gameObject);
                OnCellConfirmed?.Invoke(item);
                if (this.tooltip != null)
                    this.tooltip.ShowAboveSelector(this.selectorRect);
            }
            else
            {
                this.sfx?.PlayInvalidAction(gameObject);
            }
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (this.tabManager != null && this.tabManager.IsTabBarActive) return;

            if (this.IsCombineMode)
            {
                this.IsCombineMode = false;
                OnCombineCancelled?.Invoke();
                this.sfx?.PlayCancel(gameObject);
                return;
            }

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayCancel(gameObject);
                this.contextMenu.Close();
                return;
            }

            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
            {
                this.inspectPanel.Close();
                return;
            }

            if (this.heldItem != null)
            {
                TryPlace();
                return;
            }

            if (this.tabManager != null)
                this.tabManager.EnterTabBar();
            else
                OnCloseRequested?.Invoke();
        }

        public void RequestClose() => OnCloseRequested?.Invoke();

        void OnPickup(InputAction.CallbackContext ctx)
        {
            if (this.contextMenu  != null && this.contextMenu.IsOpen)  return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;

            if (this.heldItem == null)
                TryPickUp();
            else
                TryPlace();
        }

        void OnMenuClosed()
        {
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }

        void OnInspectClosed()
        {
            this.holding = false;
            this.lastDir = Vector2Int.zero;
            PlaceSelectorAt(this.currentCell);
        }

        // ── Pick Up / Place ──────────────────────────────────────────────────

        void TryPickUp()
        {
            InventoryItemView? item = CurrentGrid.GetItemAt(this.currentCell);
            if (item == null) return;

            if (item.BoundItem.IsEquipped)
            {
                this.sfx?.PlayCancel(gameObject);
                return;
            }

            this.currentCell = item.GridOrigin;

            this.sfx?.PlayDecide(gameObject);
            this.heldFromGrid = CurrentGrid;
            this.heldItem     = item;
            PlaceSelectorAt(this.currentCell);

            CurrentGrid.RemoveItem(item);
            item.transform.SetAsLastSibling();
            UpdateHeldItemVisual();
        }

        public void BeginHoldingSplitItem(InventoryItemView view, InventoryGrid fromGrid)
        {
            this.heldFromGrid       = fromGrid;
            this.heldItem           = view;
            this.isSplitPhantomHeld = true;

            view.SetGridOrigin(this.currentCell);
            view.transform.SetAsLastSibling();
            PlaceSelectorAt(this.currentCell);
            UpdateHeldItemVisual();
        }

        void TryPlace()
        {
            InventoryGrid targetGrid = CurrentGrid;

            if (!targetGrid.IsWithinBounds(this.currentCell, this.heldItem!.GridSize))
            {
                this.sfx?.PlayCancel(gameObject);
                return;
            }

            bool multipleItems;
            InventoryItemView? overlapping = targetGrid.GetOverlappingItem(
                this.currentCell, this.heldItem.GridSize, out multipleItems);

            if (multipleItems)
                return;

            if (overlapping != null && overlapping.BoundItem.IsEquipped)
            {
                this.sfx?.PlayCancel(gameObject);
                return;
            }

            if (overlapping == null)
            {
                this.sfx?.PlayDecide(gameObject);
                PlaceHeldItem(targetGrid, this.currentCell);
            }
            else
            {
                targetGrid.RemoveItem(overlapping);

                if (targetGrid.CanPlace(this.currentCell, this.heldItem.GridSize))
                {
                    this.sfx?.PlayDecide(gameObject);
                    Vector2Int    originBeforeSwap   = this.heldItem!.GridOrigin;
                    InventoryGrid fromGridBeforeSwap = this.heldFromGrid!;
                    PlaceHeldItem(targetGrid, this.currentCell);

                    this.heldItem     = overlapping;
                    this.heldFromGrid = fromGridBeforeSwap;
                    overlapping.SetGridOrigin(originBeforeSwap);
                    overlapping.transform.SetAsLastSibling();
                    UpdateHeldItemVisual();
                }
                else
                {
                    this.sfx?.PlayCancel(gameObject);
                    targetGrid.PlaceItem(overlapping);
                }
            }
        }

        void PlaceHeldItem(InventoryGrid targetGrid, Vector2Int origin)
        {
            if (targetGrid != this.heldFromGrid)
                OnItemMovedToNewGrid?.Invoke(this.heldItem!, this.heldFromGrid!);

            this.heldItem!.SetGridOrigin(origin);
            this.heldItem.SetOwnerGrid(targetGrid);
            this.heldItem.GetComponent<Image>().color = this.colorNormalItem;

            var rt = this.heldItem.GetComponent<RectTransform>();
            rt.SetParent(targetGrid.transform, false);
            rt.anchoredPosition = GetItemPosition(this.heldItem, origin, targetGrid);

            targetGrid.PlaceItem(this.heldItem);

            OnItemPlaced?.Invoke(this.heldItem);

            this.heldItem           = null;
            this.heldFromGrid       = null;
            this.isSplitPhantomHeld = false;

            PlaceSelectorAt(this.currentCell);
        }

        void CancelPickup()
        {
            if (this.heldItem == null) return;

            if (this.isSplitPhantomHeld)
            {
                InventoryItemView phantom = this.heldItem;

                this.heldItem           = null;
                this.heldFromGrid       = null;
                this.isSplitPhantomHeld = false;

                OnSplitCancelled?.Invoke(phantom);
                PlaceSelectorAt(this.currentCell);
                return;
            }

            this.heldItem.GetComponent<Image>().color = this.colorNormalItem;

            var rt = this.heldItem.GetComponent<RectTransform>();
            rt.SetParent(this.heldFromGrid!.transform, false);
            rt.anchoredPosition = GetItemPosition(this.heldItem, this.heldItem.GridOrigin, this.heldFromGrid);

            this.heldFromGrid.PlaceItem(this.heldItem);

            this.heldItem     = null;
            this.heldFromGrid = null;

            PlaceSelectorAt(this.currentCell);
        }

        // ── Held Item Visual ─────────────────────────────────────────────────

        Vector2 GetItemPosition(InventoryItemView item, Vector2Int cell, InventoryGrid grid)
        {
            Vector2 pos = grid.CellToLocal(cell);
            if (item.Rotation == 1)
                pos.x += item.GetComponent<RectTransform>().sizeDelta.y;
            return pos;
        }

        void UpdateHeldItemVisual()
        {
            if (this.heldItem == null) return;

            var rt = this.heldItem.GetComponent<RectTransform>();

            if (rt.parent != CurrentGrid.transform)
                rt.SetParent(CurrentGrid.transform, false);

            rt.anchoredPosition = GetItemPosition(this.heldItem, this.currentCell, CurrentGrid);

            bool canPlace = CurrentGrid.IsWithinBounds(this.currentCell, this.heldItem.GridSize)
                         && CurrentGrid.CanPlace(this.currentCell, this.heldItem.GridSize);

            if (!canPlace && CurrentGrid.IsWithinBounds(this.currentCell, this.heldItem.GridSize))
            {
                bool multi;
                InventoryItemView? overlap = CurrentGrid.GetOverlappingItem(
                    this.currentCell, this.heldItem.GridSize, out multi);

                if (!multi && overlap != null)
                {
                    CurrentGrid.RemoveItem(overlap);
                    canPlace = CurrentGrid.CanPlace(this.currentCell, this.heldItem.GridSize);
                    CurrentGrid.PlaceItem(overlap);
                }
            }

            Color tint = this.colorHoldTint;
            if (!canPlace) tint.a = this.alphaCannotPlace;
            this.heldItem.GetComponent<Image>().color = tint;
        }

        // ── Visual ───────────────────────────────────────────────────────────

        void AttachSelectorToGrid(InventoryGrid grid)
        {
            this.selectorRect.SetParent(grid.transform, false);
            this.selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
            this.selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
            this.selectorRect.pivot     = new Vector2(0f, 1f);
        }

        void PlaceSelectorAt(Vector2Int cell)
        {
            InventoryItemView? item = CurrentGrid.GetItemAt(cell);
            bool isHolding = this.heldItem != null || this.isCombineMode;

            if (this.selectorImage != null)
            {
                this.selectorImage.color = isHolding ? Color.white
                    : item != null ? ColorSelectorOnItem
                    : ColorSelectorNormal;

                if (this.selectorSpriteNormal != null && this.selectorSpriteHold != null)
                    this.selectorImage.sprite = isHolding ? this.selectorSpriteHold : this.selectorSpriteNormal;
            }

            Vector2Int size   = this.heldItem != null ? this.heldItem.GridSize
                              : item          != null ? item.GridSize
                              : Vector2Int.one;
            Vector2Int origin = this.heldItem != null ? cell
                              : item          != null ? item.GridOrigin
                              : cell;

            this.selectorRect.anchoredPosition = CurrentGrid.CellToLocal(origin)
                + new Vector2(this.selectorPadding, -this.selectorPadding);

            this.selectorRect.sizeDelta = new Vector2(
                size.x * CurrentGrid.CellSize - this.selectorPadding * 2f,
                size.y * CurrentGrid.CellSize - this.selectorPadding * 2f);

            if (this.tooltip != null && (this.contextMenu == null || !this.contextMenu.IsOpen))
            {
                if (item != null)
                {
                    bool hasSecondary  = !string.IsNullOrEmpty(item.Data.SecondaryName);
                    string displayName = (!item.IsInspected && hasSecondary)
                        ? item.Data.SecondaryName
                        : item.Data.DisplayName;

                    this.tooltip.ShowAtItem(displayName, item.GetComponent<RectTransform>());
                }
                else
                {
                    this.tooltip.Hide();
                }
            }
        }

        // ── Input Reading ────────────────────────────────────────────────────

        Vector2Int ReadDirection()
        {
            Vector2 raw = this.inputService.InventoryNavigate.ReadValue<Vector2>();

            if (raw.sqrMagnitude < 0.01f) return Vector2Int.zero;

            float absX = Mathf.Abs(raw.x);
            float absY = Mathf.Abs(raw.y);
            if (absX >= absY) return raw.x > 0 ? Vector2Int.right : Vector2Int.left;
            return raw.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        // ── Public ───────────────────────────────────────────────────────────

        public Vector2Int    CurrentCell        => this.currentCell;
        public InventoryGrid CurrentGrid_Public => CurrentGrid;
        public bool          IsHoldingItem      => this.heldItem != null;

        public void ResetCursorToOrigin()
        {
            this.currentCell      = Vector2Int.zero;
            this.currentGridIndex = 0;
            this.holding          = false;
            this.lastDir          = Vector2Int.zero;
            AttachSelectorToGrid(CurrentGrid);
            this.selectorRect.gameObject.SetActive(true);
            PlaceSelectorAt(this.currentCell);
        }

        public void HideSelectorForTabBar()
        {
            this.selectorRect.gameObject.SetActive(false);
            this.tooltip?.Hide();
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }

        public void ShowSelectorAfterTabBar()
        {
            this.currentCell = new Vector2Int(this.currentCell.x, 0);
            this.selectorRect.gameObject.SetActive(true);
            PlaceSelectorAt(this.currentCell);
        }

        public void CancelAll()
        {
            if (this.IsCombineMode) { this.IsCombineMode = false; OnCombineCancelled?.Invoke(); }
            if (this.heldItem != null)                                    CancelPickup();
            if (this.contextMenu  != null && this.contextMenu.IsOpen)     this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)    this.inspectPanel.Close();
            this.tabManager?.ResetTabBar();
            this.tooltip?.Hide();
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }

        public int GetOperatorOf(InventoryItemView view)
            => view.OwnerGrid != null ? this.gridGroup.IndexOf(view.OwnerGrid) : -1;

        public InventoryGrid? GetGridForOperator(int operatorIndex)
            => this.gridGroup.GetGrid(operatorIndex);

        public InventoryItemView? FindView(InventoryItem item)
        {
            for (int g = 0; g < this.gridGroup.Count; g++)
            {
                var grid = this.gridGroup.GetGrid(g);
                if (grid == null) continue;
                for (int c = 0; c < grid.Columns; c++)
                    for (int r = 0; r < grid.Rows; r++)
                    {
                        var view = grid.GetItemAt(new Vector2Int(c, r));
                        if (view != null && view.BoundItem == item) return view;
                    }
            }
            return null;
        }
    }
}
