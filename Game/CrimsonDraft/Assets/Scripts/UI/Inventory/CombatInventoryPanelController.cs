#nullable enable

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Audio;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CombatInventoryPanelController : MonoBehaviour, ICombatInventoryView
    {
        [SerializeField] private InventoryGrid     grid           = null!;
        [SerializeField] private InventoryItemView itemViewPrefab = null!;
        [SerializeField] private ItemContextMenu   contextMenu    = null!;
        [SerializeField] private RectTransform     selectorRect   = null!;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Use Feedback")]
        [SerializeField] private int   useFeedbackBlinkCount    = 3;
        [SerializeField] private float useFeedbackBlinkInterval = 0.08f;
        [SerializeField] private Color useFeedbackColor         = Color.white;

        [Inject] private IInventoryService inventoryService = null!;
        [Inject] private IInputService     inputService     = null!;
        [Inject] private CombatSfxData     sfx              = null!;

        public event Action<int>? OnItemUsed;
        public event Action?      OnCancelled;

        private int        operatorSlot;
        private Vector2Int currentCell;
        private bool       isActive;
        private Vector2Int lastDir;
        private float      nextMoveTime;
        private int        pendingCombineSlot = -1; // ammo box slot awaiting a weapon target
        private CanvasGroup canvasGroup  = null!;
        private Image       selectorImage = null!;

        private readonly List<InventoryItemView> spawnedViews = new();

        private static readonly Color ColorSelectorNormal  = Color.white;
        private static readonly Color ColorSelectorOnItem  = Color.yellow;
        private static readonly Color ColorSelectorCombine = new Color(0.4f, 0.9f, 1f, 1f);

        void Awake()
        {
            this.canvasGroup   = GetComponent<CanvasGroup>();
            this.selectorImage = this.selectorRect.GetComponentInChildren<Image>();
            SetVisible(false);
        }

        void Start()
        {
            // InspectPanel lives in GAMEPLAYCORE (DontDestroyOnLoad), only available at runtime.
            // Wire it to the context menu here so Inspect works in combat.
            if (this.contextMenu == null) return;
            var field = typeof(ItemContextMenu).GetField(
                "inspectPanel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field?.GetValue(this.contextMenu) != null) return;
            var panel = FindObjectOfType<InspectPanel>(true);
            if (panel != null) field?.SetValue(this.contextMenu, panel);
        }

        void OnEnable()
        {
            if (this.inputService == null) return;
            this.inputService.CombatConfirm.performed += OnConfirmInput;
            this.inputService.CombatCancel.performed  += OnCancelInput;
            if (this.contextMenu != null)
            {
                this.contextMenu.OnUseRequested     += HandleUse;
                this.contextMenu.OnCombineRequested += HandleCombine;
            }
        }

        void OnDisable()
        {
            DOTween.Kill(this.selectorImage);
            if (this.inputService == null) return;
            this.inputService.CombatConfirm.performed -= OnConfirmInput;
            this.inputService.CombatCancel.performed  -= OnCancelInput;
            if (this.contextMenu != null)
            {
                this.contextMenu.OnUseRequested     -= HandleUse;
                this.contextMenu.OnCombineRequested -= HandleCombine;
            }
        }

        void Update()
        {
            if (!this.isActive) return;

            var dir = ReadDirection();
            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                HandleMenuNavigation(dir);
                return;
            }

            HandleGridNavigation(dir);
        }

        // ── ICombatInventoryView ─────────────────────────────────────────────

        public void Show(int opSlot, RectTransform operatorOverviewRect)
        {
            this.operatorSlot      = opSlot;
            this.currentCell       = Vector2Int.zero;
            this.lastDir           = Vector2Int.zero;
            this.isActive          = true;
            this.pendingCombineSlot = -1;

            RepositionToOperator(operatorOverviewRect);
            PopulateGrid(opSlot);
            SetVisible(true);
            UpdateSelector();
        }

        // Keeps the panel's configured Y, but centers it horizontally on the
        // selected operator's overview panel.
        private void RepositionToOperator(RectTransform operatorOverviewRect)
        {
            var panel   = (RectTransform)this.transform;
            var hudRoot = (RectTransform)this.transform.parent;

            var corners = new Vector3[4];
            operatorOverviewRect.GetWorldCorners(corners);
            var center   = (corners[0] + corners[2]) * 0.5f;
            var localPos = hudRoot.InverseTransformPoint(center);

            float pivotCorrX = (panel.pivot.x - 0.5f) * panel.rect.width;
            panel.localPosition = new Vector3(
                localPos.x + pivotCorrX,
                panel.localPosition.y,
                panel.localPosition.z);
        }

        public void Hide()
        {
            DOTween.Kill(this.selectorImage);
            this.isActive          = false;
            this.pendingCombineSlot = -1;
            if (this.contextMenu != null && this.contextMenu.IsOpen)
                this.contextMenu.Close();
            ClearGrid();
            SetVisible(false);
        }

        // ── Grid population ──────────────────────────────────────────────────

        private void PopulateGrid(int opSlot)
        {
            ClearGrid();
            int start = opSlot * 4;
            int end   = Mathf.Min(start + 4, this.inventoryService.SlotCount);

            // Pass 1: items with a saved 2D position go to their exact cell.
            for (int i = start; i < end; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty || slot.Item == null) continue;
                if (slot.GridCol < 0 || slot.GridRow < 0) continue;

                SpawnItemView(slot.Item, slot.GridCol, slot.GridRow, slot.GridRotation);
            }

            // Pass 2: unpositioned items (e.g. picked up without opening the nav
            // inventory) scan the whole grid for the first free cell — mirrors
            // InventoryPopulator.TryFindSlot so items wrap across rows.
            for (int i = start; i < end; i++)
            {
                var slot = this.inventoryService.Slots[i];
                if (slot.IsEmpty || slot.Item == null) continue;
                if (slot.GridCol >= 0 && slot.GridRow >= 0) continue;

                if (TryFindFreeCell(slot.Item.Data.GridSize, out var origin))
                    SpawnItemView(slot.Item, origin.x, origin.y, 0);
                else
                    Debug.LogWarning($"[CombatInventory] No free cell for {slot.Item.Data.DisplayName}");
            }
        }

        private bool TryFindFreeCell(Vector2Int size, out Vector2Int origin)
        {
            int maxCol = this.grid.Columns - size.x;
            int maxRow = this.grid.Rows    - size.y;
            if (maxCol < 0 || maxRow < 0) { origin = default; return false; }

            for (int row = 0; row <= maxRow; row++)
                for (int col = 0; col <= maxCol; col++)
                {
                    var o = new Vector2Int(col, row);
                    if (this.grid.CanPlace(o, size)) { origin = o; return true; }
                }

            origin = default;
            return false;
        }

        private void SpawnItemView(InventoryItem item, int col, int row, int rotation)
        {
            var origin = new Vector2Int(col, row);
            var size   = rotation == 0
                ? item.Data.GridSize
                : new Vector2Int(item.Data.GridSize.y, item.Data.GridSize.x);

            if (!this.grid.CanPlace(origin, size))
            {
                Debug.LogWarning($"[CombatInventory] Cannot place {item.Data.DisplayName} at ({col},{row})");
                return;
            }

            var view = Instantiate(this.itemViewPrefab, this.grid.transform);
            view.Initialize(item, origin, this.grid.CellSize);
            view.SetOwnerGrid(this.grid);

            var rt = view.GetComponent<RectTransform>();
            rt.anchoredPosition = this.grid.CellToLocal(origin);
            this.grid.PlaceItem(view);

            if (rotation == 1)
            {
                this.grid.RemoveItem(view);
                view.Rotate();
                this.grid.PlaceItem(view);
                var pos = this.grid.CellToLocal(origin);
                pos.x += rt.sizeDelta.y;
                rt.anchoredPosition = pos;
            }

            view.RefreshQuantity();
            this.spawnedViews.Add(view);
        }

        private void ClearGrid()
        {
            foreach (var v in this.spawnedViews)
            {
                if (v == null) continue;
                this.grid.RemoveItem(v);
                Destroy(v.gameObject);
            }
            this.spawnedViews.Clear();
        }

        // ── Navigation ───────────────────────────────────────────────────────

        private void HandleGridNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero) { this.lastDir = Vector2Int.zero; return; }

            if (dir != this.lastDir)
            {
                TryMove(dir);
                this.lastDir      = dir;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (Time.unscaledTime >= this.nextMoveTime)
            {
                TryMove(dir);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        private void HandleMenuNavigation(Vector2Int dir)
        {
            if (dir == Vector2Int.zero) { this.lastDir = Vector2Int.zero; return; }

            if (dir.y != 0 && dir != this.lastDir)
            {
                this.contextMenu.NavigateMenu(dir.y);
                this.sfx?.PlayCursor(gameObject);
                this.lastDir      = dir;
                this.nextMoveTime = Time.unscaledTime + this.initialRepeatDelay;
            }
            else if (dir.y != 0 && Time.unscaledTime >= this.nextMoveTime)
            {
                this.contextMenu.NavigateMenu(dir.y);
                this.sfx?.PlayCursor(gameObject);
                this.nextMoveTime = Time.unscaledTime + this.repeatInterval;
            }
        }

        private void TryMove(Vector2Int dir)
        {
            Vector2Int next = this.currentCell + new Vector2Int(dir.x, -dir.y);

            InventoryItemView? underCursor = this.grid.GetItemAt(this.currentCell);
            if (underCursor != null)
            {
                var o = underCursor.GridOrigin;
                var s = underCursor.GridSize;
                if      (dir.x > 0) next.x = o.x + s.x;
                else if (dir.x < 0) next.x = o.x - 1;
                else if (dir.y > 0) next.y = o.y - 1;
                else if (dir.y < 0) next.y = o.y + s.y;
            }

            next.x = Mathf.Clamp(next.x, 0, this.grid.Columns - 1);
            next.y = Mathf.Clamp(next.y, 0, this.grid.Rows    - 1);
            this.currentCell = next;
            UpdateSelector();
            this.sfx?.PlayCursor(gameObject);
        }

        private void UpdateSelector()
        {
            InventoryItemView? item   = this.grid.GetItemAt(this.currentCell);
            Vector2Int         size   = item != null ? item.GridSize   : Vector2Int.one;
            Vector2Int         origin = item != null ? item.GridOrigin : this.currentCell;

            Color selectorColor;
            if (this.pendingCombineSlot >= 0)
                selectorColor = ColorSelectorCombine;
            else if (item != null)
                selectorColor = ColorSelectorOnItem;
            else
                selectorColor = ColorSelectorNormal;

            this.selectorImage.color           = selectorColor;
            this.selectorRect.anchoredPosition = this.grid.CellToLocal(origin);
            this.selectorRect.sizeDelta        = new Vector2(
                size.x * this.grid.CellSize,
                size.y * this.grid.CellSize);
        }

        // ── Input handlers ───────────────────────────────────────────────────

        private void OnConfirmInput(InputAction.CallbackContext _)
        {
            if (!this.isActive) return;

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayDecide(gameObject);
                this.contextMenu.ConfirmSelection();
                return;
            }

            // ── Combine mode: player is selecting the target weapon ───────────
            if (this.pendingCombineSlot >= 0)
            {
                InventoryItemView? target = this.grid.GetItemAt(this.currentCell);
                if (target != null && target.Data.ItemType == ItemType.Weapon
                    && this.inventoryService.CanReload(this.pendingCombineSlot, this.operatorSlot))
                {
                    this.sfx?.PlayDecide(gameObject);
                    ExecuteReload(this.pendingCombineSlot);
                }
                // If target isn't valid (full, wrong caliber), stay in combine mode
                return;
            }

            // ── Normal confirm: open context menu ─────────────────────────────
            InventoryItemView? view = this.grid.GetItemAt(this.currentCell);
            if (view == null) return;

            this.sfx?.PlayDecide(gameObject);
            int slotIndex = FindSlotIndex(view);
            var options = new ContextMenuOptions
            {
                CanUse     = view.Data is ConsumableData cd && cd.HealAmount > 0,
                CanCombine = slotIndex >= 0 && view.Data.ItemType == ItemType.AmmoBox,
                CanEquip   = false,
            };
            this.contextMenu.Open(view, options);
        }

        private void OnCancelInput(InputAction.CallbackContext _)
        {
            if (!this.isActive) return;
            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayCancel(gameObject);
                this.contextMenu.Close();
                return;
            }
            // Exit combine mode back to normal grid navigation
            if (this.pendingCombineSlot >= 0)
            {
                this.sfx?.PlayCancel(gameObject);
                this.pendingCombineSlot = -1;
                UpdateSelector();
                return;
            }
            this.sfx?.PlayCancel(gameObject);
            OnCancelled?.Invoke();
        }

        private void HandleUse(InventoryItemView view)
        {
            int slotIndex = FindSlotIndex(view);
            if (slotIndex < 0)
            {
                Debug.LogWarning("[CombatInventory] Used item not found in operator's inventory slots");
                return;
            }
            PlayUseFeedback(() => OnItemUsed?.Invoke(slotIndex));
        }

        private void HandleCombine(InventoryItemView view)
        {
            int slotIndex = FindSlotIndex(view);
            if (slotIndex < 0)
            {
                Debug.LogWarning("[CombatInventory] Combine: ammo box not found in operator's slots");
                return;
            }
            this.pendingCombineSlot = slotIndex;
            UpdateSelector();
        }

        // Called from the combine-pending confirm path
        private void ExecuteReload(int ammoSlotIndex)
        {
            this.inventoryService.ReloadOperator(ammoSlotIndex, this.operatorSlot);
            this.pendingCombineSlot = -1;
            // -1 signals "turn consumed, but no item to remove" to the orchestrator
            PlayUseFeedback(() => OnItemUsed?.Invoke(-1));
        }

        // Blinks the selector a few times to give the player feedback that the
        // action landed, then hands off to the caller (which closes the panel).
        private void PlayUseFeedback(Action onComplete)
        {
            this.isActive = false;

            Color baseColor = this.selectorImage.color;
            DOTween.Kill(this.selectorImage);

            var sequence = DOTween.Sequence().SetTarget(this.selectorImage);
            for (int i = 0; i < this.useFeedbackBlinkCount; i++)
            {
                sequence.Append(this.selectorImage.DOColor(this.useFeedbackColor, this.useFeedbackBlinkInterval));
                sequence.Append(this.selectorImage.DOColor(baseColor, this.useFeedbackBlinkInterval));
            }
            sequence.OnComplete(() => onComplete());
        }

        private int FindSlotIndex(InventoryItemView view)
        {
            int start = this.operatorSlot * 4;
            int end   = Mathf.Min(start + 4, this.inventoryService.SlotCount);
            for (int i = start; i < end; i++)
                if (this.inventoryService.Slots[i].Item == view.BoundItem) return i;
            return -1;
        }

        private Vector2Int ReadDirection()
        {
            Vector2 raw = this.inputService.CombatNavigate.ReadValue<Vector2>();
            if (raw.sqrMagnitude < 0.01f) return Vector2Int.zero;

            float absX = Mathf.Abs(raw.x);
            float absY = Mathf.Abs(raw.y);
            if (absX >= absY) return raw.x > 0 ? Vector2Int.right : Vector2Int.left;
            return raw.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        private void SetVisible(bool visible)
        {
            this.canvasGroup.alpha          = visible ? 1f : 0f;
            this.canvasGroup.interactable   = visible;
            this.canvasGroup.blocksRaycasts = visible;
        }
    }
}
