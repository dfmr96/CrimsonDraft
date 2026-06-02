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

        [Header("Audio")]
        [SerializeField] private InventorySoundManager sfx = null!;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Selector Sizing")]
        [SerializeField] private float selectorPadding = 1f;

        [Header("Hold Colors")]
        [SerializeField] private Color colorCanPlace    = new Color(0f, 1f, 0f, 0.7f);
        [SerializeField] private Color colorCannotPlace = new Color(1f, 0f, 0f, 0.7f);
        [SerializeField] private Color colorNormalItem  = Color.white;

        [Inject] private IInputService inputService = null!;

        // Combine mode — set by InventoryHUDController
        public bool IsCombineMode { get; set; }

        // Events consumed by InventoryHUDController
        public event System.Action<InventoryItemView>?               OnCellConfirmed;
        public event System.Action<InventoryItemView>?               OnCombineTargetConfirmed;
        public event System.Action<InventoryItemView, InventoryGrid>? OnItemMovedToNewGrid;
        public event System.Action?                                   OnCombineCancelled;

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
            this.inputService.InventoryConfirm.performed += OnConfirm;
            this.inputService.InventoryCancel.performed  += OnCancel;
            this.inputService.InventoryPickup.performed  += OnPickup;
        }

        void OnDisable()
        {
            this.inputService.InventoryConfirm.performed -= OnConfirm;
            this.inputService.InventoryCancel.performed  -= OnCancel;
            this.inputService.InventoryPickup.performed  -= OnPickup;
        }

        void Start()
        {
            this.selectorImage = this.selectorRect.GetComponent<Image>();

            if (this.contextMenu != null)
                this.contextMenu.OnClose += OnMenuClosed;
            if (this.inspectPanel != null) this.inspectPanel.OnClose += OnInspectClosed;

            AttachSelectorToGrid(CurrentGrid);
            PlaceSelectorAt(this.currentCell);
        }

        // ── Update ───────────────────────────────────────────────────────────

        void Update()
        {
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
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
                this.sfx?.PlayMenuNavigate();
                this.lastDir      = dir;
                this.holding      = true;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (dir.y != 0 && this.holding && Time.unscaledTime >= this.nextMoveTime)
            {
                this.contextMenu.NavigateMenu(dir.y);
                this.sfx?.PlayMenuNavigate();
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        void TryMove(Vector2Int dir)
        {
            Vector2Int next = this.currentCell + new Vector2Int(dir.x, -dir.y);

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

            if (this.heldItem != null)
                this.sfx?.PlayItemMove();
            else if (CurrentGrid.GetItemAt(this.currentCell) != null)
                this.sfx?.PlayCursorOnItem();
            else
                this.sfx?.PlayCursorMove();
        }

        // ── Input Callbacks ──────────────────────────────────────────────────

        void OnConfirm(InputAction.CallbackContext ctx)
        {
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
                this.sfx?.PlayItemRotate();
                UpdateHeldItemVisual();
                PlaceSelectorAt(this.currentCell);
                return;
            }

            if (this.contextMenu == null) return;

            if (this.contextMenu.IsOpen)
            {
                this.sfx?.PlayMenuConfirm();
                this.contextMenu.ConfirmSelection();
                return;
            }

            InventoryItemView? item = CurrentGrid.GetItemAt(this.currentCell);
            if (item != null)
            {
                this.sfx?.PlayMenuOpen();
                OnCellConfirmed?.Invoke(item);
                if (this.tooltip != null)
                    this.tooltip.ShowAboveSelector(this.selectorRect);
            }
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (this.IsCombineMode)
            {
                this.IsCombineMode = false;
                OnCombineCancelled?.Invoke();
                this.sfx?.PlayMenuCancel();
                return;
            }

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayMenuCancel();
                this.contextMenu.Close();
                return;
            }

            if (this.heldItem != null)
            {
                this.sfx?.PlayMenuCancel();
                CancelPickup();
            }
        }

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
                this.sfx?.PlayItemPlaceInvalid();
                return;
            }

            this.currentCell = item.GridOrigin;
            PlaceSelectorAt(this.currentCell);

            this.sfx?.PlayItemPickup();
            this.heldFromGrid = CurrentGrid;
            this.heldItem     = item;

            CurrentGrid.RemoveItem(item);
            item.transform.SetAsLastSibling();
            UpdateHeldItemVisual();
        }

        void TryPlace()
        {
            InventoryGrid targetGrid = CurrentGrid;

            if (!targetGrid.IsWithinBounds(this.currentCell, this.heldItem!.GridSize))
            {
                this.sfx?.PlayItemPlaceInvalid();
                return;
            }

            bool multipleItems;
            InventoryItemView? overlapping = targetGrid.GetOverlappingItem(
                this.currentCell, this.heldItem.GridSize, out multipleItems);

            if (multipleItems)
                return;

            if (overlapping == null)
            {
                this.sfx?.PlayItemPlace();
                PlaceHeldItem(targetGrid, this.currentCell);
            }
            else
            {
                targetGrid.RemoveItem(overlapping);

                if (targetGrid.CanPlace(this.currentCell, this.heldItem.GridSize))
                {
                    this.sfx?.PlayItemSwap();
                    PlaceHeldItem(targetGrid, this.currentCell);

                    this.heldItem     = overlapping;
                    this.heldFromGrid = targetGrid;
                    overlapping.transform.SetAsLastSibling();
                    UpdateHeldItemVisual();
                }
                else
                {
                    this.sfx?.PlayItemPlaceInvalid();
                    targetGrid.PlaceItem(overlapping);
                }
            }
        }

        void PlaceHeldItem(InventoryGrid targetGrid, Vector2Int origin)
        {
            if (targetGrid != this.heldFromGrid)
            {
                var weapon = this.heldItem!.BoundItem as WeaponItem;
                if (weapon != null && weapon.IsEquipped)
                    OnItemMovedToNewGrid?.Invoke(this.heldItem, this.heldFromGrid!);
            }

            this.heldItem!.SetGridOrigin(origin);
            this.heldItem.GetComponent<Image>().color = this.colorNormalItem;

            var rt = this.heldItem.GetComponent<RectTransform>();
            rt.SetParent(targetGrid.transform, false);
            rt.anchoredPosition = GetItemPosition(this.heldItem, origin, targetGrid);

            targetGrid.PlaceItem(this.heldItem);

            this.heldItem     = null;
            this.heldFromGrid = null;

            PlaceSelectorAt(this.currentCell);
        }

        void CancelPickup()
        {
            if (this.heldItem == null) return;

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

            this.heldItem.GetComponent<Image>().color = canPlace ? this.colorCanPlace : this.colorCannotPlace;
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

            if (this.selectorImage != null)
                this.selectorImage.color = item != null ? ColorSelectorOnItem : ColorSelectorNormal;

            Vector2Int size   = item != null ? item.GridSize   : Vector2Int.one;
            Vector2Int origin = item != null ? item.GridOrigin : cell;

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

        public void CancelAll()
        {
            if (this.IsCombineMode) { this.IsCombineMode = false; OnCombineCancelled?.Invoke(); }
            if (this.heldItem != null)                                    CancelPickup();
            if (this.contextMenu  != null && this.contextMenu.IsOpen)     this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)    this.inspectPanel.Close();
            this.tooltip?.Hide();
            this.holding = false;
            this.lastDir = Vector2Int.zero;
        }
    }
}
