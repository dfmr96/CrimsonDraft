using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class GridCursor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InventoryGridGroup gridGroup;
        [SerializeField] private RectTransform      selectorRect;
        [SerializeField] private ItemContextMenu    contextMenu;
        [SerializeField] private ItemTooltip        tooltip;
        [SerializeField] private InspectPanel       inspectPanel;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Selector Sizing")]
        [SerializeField] private float selectorPadding = 1f;

        [Header("Hold Colors")]
        [SerializeField] private Color colorCanPlace    = new Color(0f, 1f, 0f, 0.7f);
        [SerializeField] private Color colorCannotPlace = new Color(1f, 0f, 0f, 0.7f);
        [SerializeField] private Color colorNormalItem  = Color.white;

        // Grid state
        private int        currentGridIndex;
        private Vector2Int currentCell;
        private Image      selectorImage;
        private Vector2Int lastDir;
        private float      nextMoveTime;
        private bool       holding;

        // Held item state
        private InventoryItem heldItem;
        private InventoryGrid heldFromGrid;

        private InventoryGrid CurrentGrid => gridGroup.GetGrid(currentGridIndex);

        private static readonly Color ColorSelectorNormal = Color.white;
        private static readonly Color ColorSelectorOnItem = Color.yellow;

        // Input
        private InputAction confirmAction;
        private InputAction cancelAction;
        private InputAction pickupAction;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            if (gridGroup == null)
                gridGroup = GetComponentInParent<InventoryGridGroup>();

            // Confirm: C or Gamepad X/Cross (south)
            confirmAction = new InputAction("Confirm", InputActionType.Button);
            confirmAction.AddBinding("<Keyboard>/c");
            confirmAction.AddBinding("<Gamepad>/buttonSouth");
            confirmAction.performed += OnConfirm;

            // Cancel: V or Circle (east)
            cancelAction = new InputAction("Cancel", InputActionType.Button);
            cancelAction.AddBinding("<Keyboard>/v");
            cancelAction.AddBinding("<Gamepad>/buttonEast");
            cancelAction.performed += OnCancel;

            // Pickup/Place: X key or Square (west)
            pickupAction = new InputAction("Pickup", InputActionType.Button);
            pickupAction.AddBinding("<Keyboard>/x");
            pickupAction.AddBinding("<Gamepad>/buttonWest");
            pickupAction.performed += OnPickup;
        }

        void OnDestroy()
        {
            confirmAction.performed -= OnConfirm;
            cancelAction.performed  -= OnCancel;
            pickupAction.performed  -= OnPickup;
            confirmAction.Dispose();
            cancelAction.Dispose();
            pickupAction.Dispose();
        }

        void OnEnable()
        {
            confirmAction?.Enable();
            cancelAction?.Enable();
            pickupAction?.Enable();
        }

        void OnDisable()
        {
            confirmAction?.Disable();
            cancelAction?.Disable();
            pickupAction?.Disable();
        }

        void Start()
        {
            selectorImage = selectorRect.GetComponent<Image>();

            if (contextMenu  != null) contextMenu.OnClose  += OnMenuClosed;
            if (inspectPanel != null) inspectPanel.OnClose += OnInspectClosed;

            AttachSelectorToGrid(CurrentGrid);
            PlaceSelectorAt(currentCell);
        }

        // ── Update ───────────────────────────────────────────────────────────

        void Update()
        {
            if (inspectPanel != null && inspectPanel.IsOpen)
                return;

            Vector2Int dir = ReadDirection();

            if (contextMenu != null && contextMenu.IsOpen)
            {
                HandleMenuNavigation(dir);
                return;
            }

            HandleGridNavigation(dir);

            if (heldItem != null)
                UpdateHeldItemVisual();
        }

        // ── Navigation ───────────────────────────────────────────────────────

        void HandleGridNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero)
            {
                holding = false;
                lastDir = Vector2Int.zero;
                return;
            }

            if (dir != lastDir)
            {
                TryMove(dir);
                lastDir      = dir;
                holding      = true;
                nextMoveTime = Time.unscaledTime + initialRepeatDelay;
            }
            else if (holding && Time.unscaledTime >= nextMoveTime)
            {
                TryMove(dir);
                nextMoveTime = Time.unscaledTime + repeatInterval;
            }
        }

        void HandleMenuNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero)
            {
                holding = false;
                lastDir = Vector2Int.zero;
                return;
            }

            if (dir.y != 0 && dir != lastDir)
            {
                contextMenu.NavigateMenu(dir.y);
                lastDir      = dir;
                holding      = true;
                nextMoveTime = Time.unscaledTime + initialRepeatDelay;
            }
            else if (dir.y != 0 && holding && Time.unscaledTime >= nextMoveTime)
            {
                contextMenu.NavigateMenu(dir.y);
                nextMoveTime = Time.unscaledTime + repeatInterval;
            }
        }

        void TryMove(Vector2Int dir)
        {
            Vector2Int next = currentCell + new Vector2Int(dir.x, -dir.y);

            next.y = ((next.y % CurrentGrid.Rows) + CurrentGrid.Rows) % CurrentGrid.Rows;

            if (next.x < 0)
            {
                currentGridIndex = (currentGridIndex - 1 + gridGroup.Count) % gridGroup.Count;
                next.x = CurrentGrid.Columns - 1;
                next.y = Mathf.Clamp(next.y, 0, CurrentGrid.Rows - 1);
                AttachSelectorToGrid(CurrentGrid);
            }
            else if (next.x >= CurrentGrid.Columns)
            {
                currentGridIndex = (currentGridIndex + 1) % gridGroup.Count;
                next.x = 0;
                next.y = Mathf.Clamp(next.y, 0, CurrentGrid.Rows - 1);
                AttachSelectorToGrid(CurrentGrid);
            }

            currentCell = next;
            PlaceSelectorAt(currentCell);
        }

        // ── Input Callbacks ──────────────────────────────────────────────────

        void OnConfirm(InputAction.CallbackContext ctx)
        {
            // While holding: rotate the item instead of opening the menu
            if (heldItem != null)
            {
                heldItem.Rotate();
                UpdateHeldItemVisual();
                PlaceSelectorAt(currentCell);
                return;
            }

            if (contextMenu == null) return;

            if (contextMenu.IsOpen)
            {
                contextMenu.ConfirmSelection();
                return;
            }

            InventoryItem item = CurrentGrid.GetItemAt(currentCell);
            if (item != null)
            {
                contextMenu.Open(item);
                if (tooltip != null)
                    tooltip.ShowAboveSelector(selectorRect);
            }
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (contextMenu != null && contextMenu.IsOpen)
            {
                contextMenu.Close();
                return;
            }

            // Drop held item back to original position (cancel move)
            if (heldItem != null)
                CancelPickup();
        }

        void OnPickup(InputAction.CallbackContext ctx)
        {
            if (contextMenu  != null && contextMenu.IsOpen)  return;
            if (inspectPanel != null && inspectPanel.IsOpen) return;

            if (heldItem == null)
                TryPickUp();
            else
                TryPlace();
        }

        void OnMenuClosed()
        {
            holding = false;
            lastDir = Vector2Int.zero;
        }

        void OnInspectClosed()
        {
            holding = false;
            lastDir = Vector2Int.zero;
            PlaceSelectorAt(currentCell);
        }

        // ── Pick Up / Place ──────────────────────────────────────────────────

        void TryPickUp()
        {
            InventoryItem item = CurrentGrid.GetItemAt(currentCell);
            if (item == null) return;

            // Snap cursor to item's top-left so visual matches immediately
            currentCell = item.GridOrigin;
            PlaceSelectorAt(currentCell);

            heldFromGrid = CurrentGrid;
            heldItem     = item;

            CurrentGrid.RemoveItem(item);

            // Bring item on top visually
            item.transform.SetAsLastSibling();

            UpdateHeldItemVisual();
        }

        void TryPlace()
        {
            InventoryGrid targetGrid = CurrentGrid;

            // 1. Check bounds
            if (!targetGrid.IsWithinBounds(currentCell, heldItem.GridSize))
                return;

            // 2. Check what's in the area
            bool multipleItems;
            InventoryItem overlapping = targetGrid.GetOverlappingItem(
                currentCell, heldItem.GridSize, out multipleItems);

            // Multiple different items → blocked
            if (multipleItems)
                return;

            if (overlapping == null)
            {
                // 3a. Empty space → place
                PlaceHeldItem(targetGrid, currentCell);
            }
            else
            {
                // 3b. One item → try swap
                targetGrid.RemoveItem(overlapping);

                if (targetGrid.CanPlace(currentCell, heldItem.GridSize))
                {
                    // Place held item, then pick up the other
                    PlaceHeldItem(targetGrid, currentCell);

                    // Pick up the swapped item from its origin
                    heldItem     = overlapping;
                    heldFromGrid = targetGrid;
                    overlapping.transform.SetAsLastSibling();
                    UpdateHeldItemVisual();
                }
                else
                {
                    // No room even after removing → restore the overlapping item
                    targetGrid.PlaceItem(overlapping);
                }
            }
        }

        void PlaceHeldItem(InventoryGrid targetGrid, Vector2Int origin)
        {
            heldItem.SetGridOrigin(origin);
            heldItem.GetComponent<Image>().color = colorNormalItem;

            var rt = heldItem.GetComponent<RectTransform>();
            rt.SetParent(targetGrid.transform, false);
            rt.anchoredPosition = GetItemPosition(heldItem, origin, targetGrid);

            targetGrid.PlaceItem(heldItem);

            heldItem     = null;
            heldFromGrid = null;

            PlaceSelectorAt(currentCell);
        }

        void CancelPickup()
        {
            if (heldItem == null) return;

            // Restore to original grid and position
            heldItem.GetComponent<Image>().color = colorNormalItem;

            var rt = heldItem.GetComponent<RectTransform>();
            rt.SetParent(heldFromGrid.transform, false);
            rt.anchoredPosition = GetItemPosition(heldItem, heldItem.GridOrigin, heldFromGrid);

            heldFromGrid.PlaceItem(heldItem);

            heldItem     = null;
            heldFromGrid = null;

            PlaceSelectorAt(currentCell);
        }

        // ── Held Item Visual ─────────────────────────────────────────────────

        // Returns the corrected anchoredPosition for an item accounting for rotation offset.
        Vector2 GetItemPosition(InventoryItem item, Vector2Int cell, InventoryGrid grid)
        {
            Vector2 pos = grid.CellToLocal(cell);
            if (item.Rotation == 1)
            {
                // -90° rotation around top-left pivot shifts visual left by sizeDelta.y
                // Compensate by shifting right by the same amount
                pos.x += item.GetComponent<RectTransform>().sizeDelta.y;
            }
            return pos;
        }

        void UpdateHeldItemVisual()
        {
            if (heldItem == null) return;

            var rt = heldItem.GetComponent<RectTransform>();

            // Reparent to current grid if needed
            if (rt.parent != CurrentGrid.transform)
                rt.SetParent(CurrentGrid.transform, false);

            rt.anchoredPosition = GetItemPosition(heldItem, currentCell, CurrentGrid);

            // Color feedback
            bool canPlace = CurrentGrid.IsWithinBounds(currentCell, heldItem.GridSize)
                         && CurrentGrid.CanPlace(currentCell, heldItem.GridSize);

            // Allow swap with a single item too
            if (!canPlace && CurrentGrid.IsWithinBounds(currentCell, heldItem.GridSize))
            {
                bool multi;
                InventoryItem overlap = CurrentGrid.GetOverlappingItem(
                    currentCell, heldItem.GridSize, out multi);

                if (!multi && overlap != null)
                {
                    // Check if held fits after removing the overlap
                    CurrentGrid.RemoveItem(overlap);
                    canPlace = CurrentGrid.CanPlace(currentCell, heldItem.GridSize);
                    CurrentGrid.PlaceItem(overlap);
                }
            }

            heldItem.GetComponent<Image>().color = canPlace ? colorCanPlace : colorCannotPlace;
        }

        // ── Visual ───────────────────────────────────────────────────────────

        void AttachSelectorToGrid(InventoryGrid grid)
        {
            selectorRect.SetParent(grid.transform, false);
            selectorRect.anchorMin = new Vector2(0.5f, 0.5f);
            selectorRect.anchorMax = new Vector2(0.5f, 0.5f);
            selectorRect.pivot     = new Vector2(0f, 1f);
        }

        void PlaceSelectorAt(Vector2Int cell)
        {
            InventoryItem item = CurrentGrid.GetItemAt(cell);

            if (selectorImage != null)
                selectorImage.color = item != null ? ColorSelectorOnItem : ColorSelectorNormal;

            Vector2Int size   = item != null ? item.GridSize   : Vector2Int.one;
            Vector2Int origin = item != null ? item.GridOrigin : cell;

            selectorRect.anchoredPosition = CurrentGrid.CellToLocal(origin)
                + new Vector2(selectorPadding, -selectorPadding);

            selectorRect.sizeDelta = new Vector2(
                size.x * CurrentGrid.CellSize - selectorPadding * 2f,
                size.y * CurrentGrid.CellSize - selectorPadding * 2f);

            // Tooltip
            if (tooltip != null && (contextMenu == null || !contextMenu.IsOpen))
            {
                if (item != null)
                {
                    bool hasSecondary  = !string.IsNullOrEmpty(item.Data.secondaryName);
                    string displayName = (!item.IsInspected && hasSecondary)
                        ? item.Data.secondaryName
                        : item.Data.primaryName;

                    tooltip.ShowAtItem(displayName, item.GetComponent<RectTransform>());
                }
                else
                {
                    tooltip.Hide();
                }
            }
        }

        // ── Input Reading ────────────────────────────────────────────────────

        Vector2Int ReadDirection()
        {
            int x = 0, y = 0;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed)  x -= 1;
                if (kb.rightArrowKey.isPressed) x += 1;
                if (kb.upArrowKey.isPressed)    y += 1;
                if (kb.downArrowKey.isPressed)  y -= 1;
            }

            if (x == 0 && y == 0)
            {
                var gp = Gamepad.current;
                if (gp != null)
                {
                    if (gp.dpad.left.isPressed  || gp.leftStick.left.isPressed)  x -= 1;
                    if (gp.dpad.right.isPressed || gp.leftStick.right.isPressed) x += 1;
                    if (gp.dpad.up.isPressed    || gp.leftStick.up.isPressed)    y += 1;
                    if (gp.dpad.down.isPressed  || gp.leftStick.down.isPressed)  y -= 1;
                }
            }

            if (x == 0 && y == 0) return Vector2Int.zero;
            if (Mathf.Abs(x) >= Mathf.Abs(y)) return x > 0 ? Vector2Int.right : Vector2Int.left;
            return y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        // ── Public ───────────────────────────────────────────────────────────

        public Vector2Int  CurrentCell         => currentCell;
        public InventoryGrid CurrentGrid_Public => CurrentGrid;
        public bool        IsHoldingItem        => heldItem != null;
    }
}
