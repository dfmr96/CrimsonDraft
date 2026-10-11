#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        [Header("Selector Sprites")]
        [SerializeField] private Sprite? selectorSpriteNormal;
        [SerializeField] private Sprite? selectorSpriteHold; // shown while selecting a combine target

        // Plays on the selector when an item is used/combined. Its clip should end with an
        // Animation Event calling SelectorAnimationRelay.OnUseAnimationComplete.
        [Header("Use Feedback")]
        [SerializeField] private Animator? selectorAnimator;
        [SerializeField] private float     useAnimationTimeout = 1.5f; // safety net if the event never fires

        [Inject] private IInventoryService inventoryService = null!;
        [Inject] private IInputService     inputService     = null!;
        [Inject] private CombatSfxData     sfx              = null!;

        public event Action<InventoryItem?>? OnItemUsed;
        public event Action?      OnCancelled;

        private int        operatorSlot;
        private Vector2Int currentCell;
        private bool       isActive;
        private Vector2Int lastDir;
        private float      nextMoveTime;
        private AmmoBoxItem? pendingCombineAmmo;
        private InventoryItemView? combineSourceView; // ammo box view being tinted while pending
        private CanvasGroup canvasGroup  = null!;
        private Image       selectorImage = null!;
        private Action?     pendingUseCallback;
        private bool        useAnimationCompleted = true;
        private InspectPanel? inspectPanel;
        private bool        inputBound;

        private ContainerGridPresenter? presenter;

        private static readonly Color ColorSelectorNormal     = Color.white;
        private static readonly Color ColorSelectorOnItem     = Color.yellow;
        private static readonly Color ColorCombineSourceTint  = new Color(154f / 255f, 159f / 255f, 92f / 255f, 0.9f); // #9A9F5C
        private static readonly int   UseTriggerHash          = Animator.StringToHash("Use");

        void Awake()
        {
            this.canvasGroup   = GetComponent<CanvasGroup>();
            this.selectorImage = this.selectorRect.GetComponentInChildren<Image>();
            if (this.selectorAnimator != null)
            {
                var relay = this.selectorAnimator.GetComponent<SelectorAnimationRelay>();
                if (relay == null) relay = this.selectorAnimator.gameObject.AddComponent<SelectorAnimationRelay>();
                relay.Init(this);
            }
            SetVisible(false);
        }

        void Start()
        {
            // InspectPanel lives in GAMEPLAYCORE (DontDestroyOnLoad), only available at runtime.
            // Wire it to the context menu here so Inspect works in combat.
            this.inspectPanel = FindObjectOfType<InspectPanel>(true);
            if (this.inspectPanel != null)
                this.inspectPanel.OnClose += OnInspectClosed;

            if (this.contextMenu != null)
            {
                var field = typeof(ItemContextMenu).GetField(
                    "inspectPanel",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field?.GetValue(this.contextMenu) == null && this.inspectPanel != null)
                    field?.SetValue(this.contextMenu, this.inspectPanel);
            }

            // OnEnable runs before VContainer injection at scene start — subscribe here if missed
            OnEnable();
        }

        void OnInspectClosed(string? selectItemId)
        {
            this.lastDir = Vector2Int.zero;
        }

        void OnEnable()
        {
            if (this.inputService == null || this.inputBound) return;
            this.inputService.CombatConfirm.performed += OnConfirmInput;
            this.inputService.CombatCancel.performed  += OnCancelInput;
            if (this.contextMenu != null)
            {
                this.contextMenu.OnUseRequested     += HandleUse;
                this.contextMenu.OnCombineRequested += HandleCombine;
            }
            this.inputBound = true;
        }

        void OnDisable()
        {
            this.pendingUseCallback    = null;
            this.useAnimationCompleted = true;
            if (!this.inputBound || this.inputService == null) return;
            this.inputService.CombatConfirm.performed -= OnConfirmInput;
            this.inputService.CombatCancel.performed  -= OnCancelInput;
            if (this.contextMenu != null)
            {
                this.contextMenu.OnUseRequested     -= HandleUse;
                this.contextMenu.OnCombineRequested -= HandleCombine;
            }
            this.inputBound = false;
        }

        void OnDestroy()
        {
            if (this.inspectPanel != null)
                this.inspectPanel.OnClose -= OnInspectClosed;
        }

        void Update()
        {
            if (!this.isActive) return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;

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
            this.pendingCombineAmmo = null;

            RepositionToOperator(operatorOverviewRect);
            BindGrid(opSlot);
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
            this.isActive              = false;
            this.pendingCombineAmmo    = null;
            this.combineSourceView     = null;
            this.pendingUseCallback    = null;
            this.useAnimationCompleted = true;
            if (this.contextMenu != null && this.contextMenu.IsOpen)
                this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
                this.inspectPanel.Close();
            UnbindGrid();
            SetVisible(false);
        }

        // ── Grid binding ─────────────────────────────────────────────────────

        private void BindGrid(int opSlot)
        {
            UnbindGrid();
            this.presenter = new ContainerGridPresenter(
                this.inventoryService.GetContainer(ContainerId.Operator(opSlot)), this.grid, this.itemViewPrefab);
            this.presenter.Rendered += UpdateSelector;
        }

        private void UnbindGrid()
        {
            if (this.presenter == null) return;
            this.presenter.Rendered -= UpdateSelector;
            this.presenter.Dispose();
            this.presenter = null;
        }

        private InventoryItemView? ViewAtCursor() => this.presenter?.ViewAt(this.currentCell);

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
            if (this.presenter == null) return;
            var container = this.presenter.Container;
            Vector2Int next = this.currentCell + new Vector2Int(dir.x, -dir.y);

            if (container.GetItemAt(this.currentCell) is { } under)
            {
                var placement = container.GetPlacement(under)!;
                if      (dir.x > 0) next.x = placement.Origin.x + placement.Footprint.x;
                else if (dir.x < 0) next.x = placement.Origin.x - 1;
                else if (dir.y > 0) next.y = placement.Origin.y - 1;
                else if (dir.y < 0) next.y = placement.Origin.y + placement.Footprint.y;
            }

            next.x = Mathf.Clamp(next.x, 0, container.Width - 1);
            next.y = Mathf.Clamp(next.y, 0, container.Height - 1);
            this.currentCell = next;
            UpdateSelector();
            this.sfx?.PlayCursor(gameObject);
        }

        private void UpdateSelector()
        {
            if (this.presenter == null) return;
            var under     = this.presenter.Container.GetItemAt(this.currentCell);
            var placement = under != null ? this.presenter.Container.GetPlacement(under) : null;
            Vector2Int size   = placement?.Footprint ?? Vector2Int.one;
            Vector2Int origin = placement?.Origin    ?? this.currentCell;

            bool isCombining = this.pendingCombineAmmo != null;
            this.selectorImage.color = isCombining ? Color.white
                : under != null ? ColorSelectorOnItem
                : ColorSelectorNormal;

            if (this.selectorSpriteNormal != null && this.selectorSpriteHold != null)
                this.selectorImage.sprite = isCombining ? this.selectorSpriteHold : this.selectorSpriteNormal;

            this.selectorRect.anchoredPosition = this.grid.CellToLocal(origin);
            this.selectorRect.sizeDelta        = new Vector2(size.x * this.grid.CellSize, size.y * this.grid.CellSize);
        }

        // ── Input handlers ───────────────────────────────────────────────────

        private void OnConfirmInput(InputAction.CallbackContext _)
        {
            if (!this.isActive) return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) return;

            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayDecide(gameObject);
                this.contextMenu.ConfirmSelection();
                return;
            }

            if (this.pendingCombineAmmo != null)
            {
                InventoryItemView? target = ViewAtCursor();
                if (target != null && target.Data.ItemType == ItemType.Weapon
                    && this.inventoryService.CanReload(this.pendingCombineAmmo, this.operatorSlot))
                {
                    this.sfx?.PlayDecide(gameObject);
                    ExecuteReload();
                }
                return;
            }

            InventoryItemView? view = ViewAtCursor();
            if (view == null) return;

            var options = new ContextMenuOptions
            {
                CanUse     = view.Data is ConsumableData cd && cd.HealAmount > 0,
                CanCombine = view.Data.ItemType == ItemType.AmmoBox,
                CanEquip   = false,
                CanInspect = false,
            };

            // Nothing this item can do (no Use, no Combine in this menu) —
            // don't open an all-disabled submenu or let the turn be spent on it.
            if (!options.CanUse && !options.CanCombine)
            {
                this.sfx?.PlayInvalidAction(gameObject);
                return;
            }

            this.sfx?.PlayDecide(gameObject);
            this.contextMenu.Open(view, options);
        }

        private void OnCancelInput(InputAction.CallbackContext _)
        {
            if (!this.isActive) return;
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
            {
                this.inspectPanel.Close();
                return;
            }
            if (this.contextMenu != null && this.contextMenu.IsOpen)
            {
                this.sfx?.PlayCancel(gameObject);
                this.contextMenu.Close();
                return;
            }
            if (this.pendingCombineAmmo != null)
            {
                this.sfx?.PlayCancel(gameObject);
                this.pendingCombineAmmo = null;
                RevertCombineSourceTint();
                UpdateSelector();
                return;
            }
            this.sfx?.PlayCancel(gameObject);
            OnCancelled?.Invoke();
        }

        private void HandleUse(InventoryItemView view)
        {
            var item = view.BoundItem;
            PlayUseFeedback(() => OnItemUsed?.Invoke(item));
        }

        private void HandleCombine(InventoryItemView view)
        {
            if (view.BoundItem is not AmmoBoxItem ammo) return;
            this.pendingCombineAmmo = ammo;
            this.combineSourceView  = view;
            view.SetTint(ColorCombineSourceTint);
            UpdateSelector();
        }

        private void ExecuteReload()
        {
            var ammo = this.pendingCombineAmmo!;
            this.pendingCombineAmmo = null;
            RevertCombineSourceTint();
            this.inventoryService.TryReload(ammo, this.operatorSlot);
            PlayUseFeedback(() => OnItemUsed?.Invoke(null));
        }

        private void RevertCombineSourceTint()
        {
            if (this.combineSourceView != null) this.combineSourceView.ResetTint();
            this.combineSourceView = null;
        }

        // Plays the selector's "use" animation and hands off to the caller (which closes
        // the panel) once it finishes. Completion normally arrives via an Animation Event
        // on the clip (SelectorAnimationRelay.OnUseAnimationComplete); the timeout below is
        // just a safety net in case that event is missing or misconfigured.
        private void PlayUseFeedback(Action onComplete)
        {
            this.isActive = false;

            if (this.selectorAnimator == null)
            {
                onComplete();
                return;
            }

            this.useAnimationCompleted = false;
            this.pendingUseCallback    = onComplete;
            this.selectorAnimator.ResetTrigger(UseTriggerHash);
            this.selectorAnimator.SetTrigger(UseTriggerHash);
            UseAnimationTimeoutFallback().Forget();
        }

        // Called by SelectorAnimationRelay when the use-animation clip's Animation Event fires.
        internal void OnSelectorAnimationComplete()
        {
            if (this.useAnimationCompleted) return;
            this.useAnimationCompleted = true;

            var callback = this.pendingUseCallback;
            this.pendingUseCallback = null;
            callback?.Invoke();
        }

        private async UniTaskVoid UseAnimationTimeoutFallback()
        {
            await UniTask.WaitForSeconds(this.useAnimationTimeout, ignoreTimeScale: true);
            if (!this.useAnimationCompleted)
            {
                Debug.LogWarning("[CombatInventory] Selector use animation timed out — forcing continue.");
                OnSelectorAnimationComplete();
            }
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
