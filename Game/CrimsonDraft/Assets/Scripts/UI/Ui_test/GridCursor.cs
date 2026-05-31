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
        [SerializeField] private RectTransform selectorRect;
        [SerializeField] private ItemContextMenu contextMenu;
        [SerializeField] private ItemTooltip    tooltip;
        [SerializeField] private InspectPanel   inspectPanel;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Selector Sizing")]
        [SerializeField] private float selectorPadding = 1f;

        // State
        private int         currentGridIndex;
        private Vector2Int  currentCell;
        private Image       selectorImage;
        private Vector2Int  lastDir;
        private float       nextMoveTime;
        private bool        holding;

        private InventoryGrid CurrentGrid => gridGroup.GetGrid(currentGridIndex);

        // Colors
        private static readonly Color ColorNormal = Color.white;
        private static readonly Color ColorOnItem  = Color.yellow;

        // Input actions (confirm / cancel only — navigation uses direct polling)
        private InputAction confirmAction;
        private InputAction cancelAction;

        // ── Unity lifecycle ──────────────────────────────────────────────────

        void Awake()
        {
            if (gridGroup == null)
                gridGroup = GetComponentInParent<InventoryGridGroup>();

            confirmAction = new InputAction("Confirm", InputActionType.Button);
            confirmAction.AddBinding("<Keyboard>/c");
            confirmAction.AddBinding("<Gamepad>/buttonSouth");

            cancelAction = new InputAction("Cancel", InputActionType.Button);
            cancelAction.AddBinding("<Keyboard>/v");
            cancelAction.AddBinding("<Gamepad>/buttonWest");

            confirmAction.performed += OnConfirm;
            cancelAction.performed  += OnCancel;
        }

        void OnEnable()
        {
            confirmAction?.Enable();
            cancelAction?.Enable();
        }

        void OnDisable()
        {
            confirmAction?.Disable();
            cancelAction?.Disable();
        }

        void OnDestroy()
        {
            confirmAction.performed -= OnConfirm;
            cancelAction.performed  -= OnCancel;
            confirmAction.Dispose();
            cancelAction.Dispose();
        }

        void Start()
        {
            selectorImage = selectorRect.GetComponent<Image>();

            if (contextMenu != null)
                contextMenu.OnClose += OnMenuClosed;

            AttachSelectorToGrid(CurrentGrid);
            PlaceSelectorAt(currentCell);
        }

        void Update()
        {
            // Full lock while inspect panel is open — only InspectPanel handles its own close
            if (inspectPanel != null && inspectPanel.IsOpen)
                return;

            Vector2Int dir = ReadDirection();

            if (contextMenu != null && contextMenu.IsOpen)
            {
                HandleMenuNavigation(dir);
                return;
            }

            HandleGridNavigation(dir);
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

        // ── Input ────────────────────────────────────────────────────────────

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

        void OnConfirm(InputAction.CallbackContext ctx)
        {
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
                // Move tooltip above the selector so it doesn't overlap the menu
                if (tooltip != null)
                    tooltip.ShowAboveSelector(selectorRect);
            }
        }

        void OnCancel(InputAction.CallbackContext ctx)
        {
            if (contextMenu != null && contextMenu.IsOpen)
                contextMenu.Close();
        }

        void OnMenuClosed()
        {
            holding = false;
            lastDir = Vector2Int.zero;
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
                selectorImage.color = item != null ? ColorOnItem : ColorNormal;

            Vector2Int size   = item != null ? item.GridSize   : Vector2Int.one;
            Vector2Int origin = item != null ? item.GridOrigin : cell;

            selectorRect.anchoredPosition = CurrentGrid.CellToLocal(origin)
                + new Vector2(selectorPadding, -selectorPadding);

            selectorRect.sizeDelta = new Vector2(
                size.x * CurrentGrid.CellSize - selectorPadding * 2f,
                size.y * CurrentGrid.CellSize - selectorPadding * 2f);

            // Tooltip — only update position when menu is closed
            if (tooltip != null && (contextMenu == null || !contextMenu.IsOpen))
            {
                if (item != null)
                {
                    // Show secondary name until inspected, then always primary
                    bool hasSecondary = !string.IsNullOrEmpty(item.Data.secondaryName);
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

        // ── Public ───────────────────────────────────────────────────────────

        public Vector2Int CurrentCell              => currentCell;
        public InventoryGrid CurrentGrid_Public    => CurrentGrid;
    }
}
