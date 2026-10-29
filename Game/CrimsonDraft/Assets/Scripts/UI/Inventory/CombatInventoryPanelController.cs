#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using CrimsonDraft.Audio;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CombatInventoryPanelController : MonoBehaviour, ICombatInventoryView
    {
        // One inventory per operator, each under its own card. Only the acting operator's is
        // interactive (it gets the cursor); the others are shown dimmed, as read-only context.
        [Serializable]
        private sealed class OperatorGridSlot
        {
            public RectTransform root = null!;
            public InventoryGrid grid = null!;
        }

        [SerializeField] private OperatorGridSlot[] slots         = Array.Empty<OperatorGridSlot>();
        [SerializeField] private InventoryItemView itemViewPrefab = null!;
        [SerializeField] private ItemContextMenu   contextMenu    = null!;
        [SerializeField] private RectTransform     selectorRect   = null!;

        [Header("Navigation Feel")]
        [SerializeField] private float initialRepeatDelay = 0.4f;
        [SerializeField] private float repeatInterval     = 0.1f;

        [Header("Selector Sprites")]
        [SerializeField] private Sprite? selectorSpriteNormal;
        [SerializeField] private Sprite? selectorSpriteHold; // shown while selecting a combine target
        [Tooltip("Frame drawn over the cursor while it is on an item (same 'hover' as the exploration inventory).")]
        [SerializeField] private Sprite? selectorHoverSprite;

        // Plays on the selector when an item is used/combined. Its clip should end with an
        // Animation Event calling SelectorAnimationRelay.OnUseAnimationComplete.
        [Header("Use Feedback")]
        [SerializeField] private Animator? selectorAnimator;
        [SerializeField] private float     useAnimationTimeout = 1.5f; // safety net if the event never fires

        [Header("Open Animation")]
        [SerializeField, Range(0f, 1f)] private float openStartScale = 0.5f;
        [SerializeField] private float openDuration = 0.18f;
        [SerializeField] private float closeDuration = 0.12f;

        [Header("Dimming")]
        [SerializeField, Range(0f, 1f)] private float inactiveAlpha = 0.35f;
        [Tooltip("Canvas units between the bottom of the operator cards and the top of each inventory frame.")]
        [SerializeField] private float cardGap = 3f;
        [Tooltip("Inventory of a slot with no operator (MIA): much dimmer than the other operators' ones.")]
        [SerializeField, Range(0f, 1f)] private float missingAlpha = 0.15f;

        [Inject] private IInventoryService inventoryService = null!;
        [Inject] private IOperatorRoster   roster           = null!;
        [Inject] private IInputService     inputService     = null!;
        [Inject] private CombatSfxData     sfx              = null!;

        public event Action<InventoryItem?, int>? OnItemUsed; // item (null = reload), target operator slot
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

        private ContainerGridPresenter?[] presenters = Array.Empty<ContainerGridPresenter?>();
        private float[] slotLocalY = Array.Empty<float>();
        private Vector3[] slotRestPos = Array.Empty<Vector3>();
        private Vector3[] slotPivot   = Array.Empty<Vector3>(); // top-center of each frame, in the slot's parent space

        private ContainerGridPresenter? presenter =>
            this.operatorSlot >= 0 && this.operatorSlot < this.presenters.Length ? this.presenters[this.operatorSlot] : null;
        private InventoryGrid? ActiveGrid =>
            this.operatorSlot >= 0 && this.operatorSlot < this.slots.Length ? this.slots[this.operatorSlot].grid : null;

        private Image? hoverImage;
        private static readonly int   UseTriggerHash          = Animator.StringToHash("Use");

        void Awake()
        {
            this.canvasGroup   = GetComponent<CanvasGroup>();
            this.selectorImage = this.selectorRect.GetComponentInChildren<Image>();
            this.hoverImage    = CreateHover();
            this.slotLocalY    = this.slots.Select(x => x.root.localPosition.y).ToArray();
            this.slotRestPos   = new Vector3[this.slots.Length];
            this.slotPivot     = new Vector3[this.slots.Length];
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
                this.contextMenu.OnUseOnRequested   += HandleUseOn;
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
                this.contextMenu.OnUseOnRequested   -= HandleUseOn;
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

        public void Show(int opSlot, IReadOnlyList<RectTransform> operatorCardRects)
        {
            this.operatorSlot      = opSlot;
            this.currentCell       = Vector2Int.zero;
            this.lastDir           = Vector2Int.zero;
            this.isActive          = true;
            this.pendingCombineAmmo = null;

            PositionSlots(operatorCardRects);
            PlayOpenAnimation();
            BindGrids();
            SetVisible(true);

            // The one selector is shared: it lives inside whichever grid is the interactive one.
            this.selectorRect.SetParent(ActiveGrid!.transform, false);
            this.selectorRect.SetAsLastSibling();
            UpdateSelector();
        }

        // Each slot is centered horizontally under its operator's card, with its frame's top edge a fixed gap below
        // the cards' bottom edge (the strip has just grown, so the authored Y no longer lines up).
        private void PositionSlots(IReadOnlyList<RectTransform> operatorCardRects)
        {
            // The acting card may still be easing down from its focus lift, so take the lowest bottom edge as the real one.
            float cardBottom = float.MaxValue;
            var   corners4   = new Vector3[4];
            for (int i = 0; i < operatorCardRects.Count; i++)
            {
                operatorCardRects[i].GetWorldCorners(corners4);
                cardBottom = Mathf.Min(cardBottom, corners4[0].y);
            }
            for (int i = 0; i < this.slots.Length; i++)
            {
                var root = this.slots[i].root;
                // A slot without an operator (MIA) still shows its empty inventory, heavily dimmed and untouchable.
                bool shown = i < operatorCardRects.Count;
                root.gameObject.SetActive(shown);
                if (!shown) continue;

                root.DOKill();
                root.localScale = Vector3.one;

                var corners = new Vector3[4];
                operatorCardRects[i].GetWorldCorners(corners);
                var worldCenter = (corners[0] + corners[2]) * 0.5f;

                // Local space on purpose: a ScreenSpaceCamera canvas only settles its scale after Awake,
                // so a world-space Y captured at startup would be wrong by the time this runs.
                float localX = root.parent.InverseTransformPoint(worldCenter).x;
                root.localPosition = new Vector3(localX, this.slotLocalY[i], 0f);

                if (root.childCount > 0)
                {
                    float canvasScale = root.GetComponentInParent<Canvas>().rootCanvas.transform.lossyScale.y;
                    var   frame       = (RectTransform)root.GetChild(0);
                    frame.GetWorldCorners(corners4);
                    float deltaWorld  = (cardBottom - this.cardGap * canvasScale) - corners4[1].y;
                    var   pos         = root.localPosition;
                    pos.y += root.parent.InverseTransformVector(new Vector3(0f, deltaWorld, 0f)).y;
                    root.localPosition = pos;
                }

                this.slotRestPos[i] = root.localPosition;

                if (!root.TryGetComponent(out CanvasGroup group)) group = root.gameObject.AddComponent<CanvasGroup>();
                bool missing = !IsSlotPresent(i);
                group.alpha          = missing ? this.missingAlpha : i == this.operatorSlot ? 1f : this.inactiveAlpha;
                group.interactable   = !missing;
                group.blocksRaycasts = !missing;
            }
        }

        // Each inventory starts small and grows to full size, scaling about the top-center of its frame
        // (the edge glued to its card) so it reads as a window opening out of the card.
        private void PlayOpenAnimation()
        {
            for (int i = 0; i < this.slots.Length; i++)
            {
                var root = this.slots[i].root;
                root.DOKill();
                if (!root.gameObject.activeSelf || root.childCount == 0) continue;

                var parent  = (RectTransform)root.parent;
                var frame   = (RectTransform)root.GetChild(0);
                var corners = new Vector3[4];
                frame.GetWorldCorners(corners);
                Vector3 pivot = parent.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
                this.slotPivot[i] = pivot;

                Vector3 basePos = this.slotRestPos[i];
                float   start   = this.openStartScale;

                void Apply(float scale)
                {
                    root.localScale    = Vector3.one * scale;
                    root.localPosition = pivot + (basePos - pivot) * scale;
                }

                Apply(start);
                DOVirtual.Float(start, 1f, this.openDuration, Apply)
                    .SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(root);
            }
        }

        private Tween? closeCall;

        // The opening animation backwards: each inventory shrinks back toward the edge glued to its card,
        // then everything hides and onComplete fires.
        public void Close(Action onComplete)
        {
            this.isActive           = false;
            this.pendingCombineAmmo = null;
            if (this.contextMenu != null && this.contextMenu.IsOpen) this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen) this.inspectPanel.Close();

            for (int i = 0; i < this.slots.Length; i++)
            {
                var root = this.slots[i].root;
                root.DOKill();
                if (!root.gameObject.activeSelf || root.childCount == 0) continue;

                // Pivot/rest position were captured when opening, so closing mid-open still shrinks about the same point.
                Vector3 pivot   = this.slotPivot[i];
                Vector3 basePos = this.slotRestPos[i];
                float   end     = this.openStartScale;

                DOVirtual.Float(root.localScale.x, end, this.closeDuration, scale =>
                    {
                        root.localScale    = Vector3.one * scale;
                        root.localPosition = pivot + (basePos - pivot) * scale;
                    })
                    .SetEase(Ease.InQuad).SetUpdate(true).SetTarget(root);
            }

            this.closeCall?.Kill();
            this.closeCall = DOVirtual.DelayedCall(this.closeDuration, () =>
            {
                this.closeCall = null;
                Hide();
                onComplete();
            }, ignoreTimeScale: true);
        }

        public void Hide()
        {
            this.closeCall?.Kill();
            this.closeCall = null;

            foreach (var slot in this.slots)
            {
                slot.root.DOKill();
                slot.root.localScale = Vector3.one;
            }

            this.isActive              = false;
            this.pendingCombineAmmo    = null;
            this.combineSourceView     = null;
            this.pendingUseCallback    = null;
            this.useAnimationCompleted = true;
            if (this.contextMenu != null && this.contextMenu.IsOpen)
                this.contextMenu.Close();
            if (this.inspectPanel != null && this.inspectPanel.IsOpen)
                this.inspectPanel.Close();
            UnbindGrids();
            SetVisible(false);
        }

        // ── Grid binding ─────────────────────────────────────────────────────

        private void BindGrids()
        {
            UnbindGrids();
            this.presenters = new ContainerGridPresenter?[this.slots.Length];
            for (int i = 0; i < this.slots.Length; i++)
            {
                if (!this.slots[i].root.gameObject.activeSelf || !IsSlotPresent(i)) continue;

                var presenter = new ContainerGridPresenter(
                    this.inventoryService.GetContainer(ContainerId.Operator(i)), this.slots[i].grid, this.itemViewPrefab);
                if (i == this.operatorSlot) presenter.Rendered += UpdateSelector;
                this.presenters[i] = presenter;
            }
        }

        private bool IsSlotPresent(int i) => i < this.roster.Count && this.roster[i].IsPresent;

        private void UnbindGrids()
        {
            for (int i = 0; i < this.presenters.Length; i++)
            {
                var presenter = this.presenters[i];
                if (presenter == null) continue;
                presenter.Rendered -= UpdateSelector;
                presenter.Dispose();
            }
            this.presenters = Array.Empty<ContainerGridPresenter?>();
        }

        private InventoryItemView? ViewAtCursor() => this.presenter?.ViewAt(this.currentCell);

        // The exploration cursor lights a "hover" frame over the item under it; built here because this
        // selector is a wrapper with the Animator-driven image as its child.
        private Image? CreateHover()
        {
            if (this.selectorHoverSprite == null) return null;

            var go = new GameObject("hover", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(this.selectorImage.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var image           = go.GetComponent<Image>();
            image.sprite        = this.selectorHoverSprite;
            image.type          = Image.Type.Sliced;
            image.raycastTarget = false;
            var color           = SelectorView.ColorSelector;
            color.a             = 0.039f;
            image.color         = color;
            go.SetActive(false);
            return image;
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

            // Same look as the exploration inventory: olive cursor, a hover frame over an item (or while a
            // combine target is being picked), and the hold sprite for that picking.
            bool isCombining = this.pendingCombineAmmo != null;
            this.selectorImage.color = SelectorView.ColorSelector;
            if (this.hoverImage != null)
                this.hoverImage.gameObject.SetActive(isCombining || under != null);

            if (this.selectorSpriteNormal != null && this.selectorSpriteHold != null)
                this.selectorImage.sprite = isCombining ? this.selectorSpriteHold : this.selectorSpriteNormal;

            this.selectorRect.anchoredPosition = ActiveGrid!.CellToLocal(origin);
            this.selectorRect.sizeDelta        = new Vector2(size.x * ActiveGrid!.CellSize, size.y * ActiveGrid!.CellSize);
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
                if (target != null && target.Data is WeaponData
                    && this.inventoryService.CanReload(this.pendingCombineAmmo, this.operatorSlot))
                {
                    this.sfx?.PlayDecide(gameObject);
                    ExecuteReload();
                }
                return;
            }

            InventoryItemView? view = ViewAtCursor();
            if (view == null) return;

            var healTargets = view.Data is ConsumableData ? BuildHealTargets() : null;
            var options = new ContextMenuOptions
            {
                CanUse     = view.Data is ConsumableData cd && cd.HealAmount > 0 && healTargets!.Any(t => t.Enabled),
                UseTargets = healTargets,
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
                if (!this.contextMenu.TryBackFromTargets()) this.contextMenu.Close();
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
            int slot = this.operatorSlot;
            PlayUseFeedback(() => OnItemUsed?.Invoke(item, slot));
        }

        // The context menu listed the operators; the item is applied to the one picked, not necessarily the acting one.
        private void HandleUseOn(InventoryItemView view, int targetSlot)
        {
            var item = view.BoundItem;
            PlayUseFeedback(() => OnItemUsed?.Invoke(item, targetSlot));
        }

        // Every present operator is listed; only a living one can be picked (same rule as the exploration inventory).
        private IReadOnlyList<UseTarget> BuildHealTargets()
        {
            var targets = new List<UseTarget>();
            for (int i = 0; i < this.roster.Count; i++)
            {
                var op = this.roster[i];
                if (!op.IsPresent) continue;
                targets.Add(new UseTarget(i, op.Data?.CombatName ?? $"OP {i + 1}", op.IsAlive));
            }
            return targets;
        }

        private void HandleCombine(InventoryItemView view)
        {
            if (view.BoundItem is not AmmoBoxItem ammo) return;
            this.pendingCombineAmmo = ammo;
            this.combineSourceView  = view;
            SelectorView.MarkCombineSource(view, this.selectorSpriteNormal); // red frame on the item that goes
            UpdateSelector();
        }

        private void ExecuteReload()
        {
            var ammo   = this.pendingCombineAmmo!;
            var source = this.combineSourceView;
            var target = ViewAtCursor();
            int slot   = this.operatorSlot;

            // The copy of the ammo that flies onto the weapon is made now, while both views still exist
            // (the grid re-renders as soon as the reload goes through).
            var flight = source != null && target != null ? CombineFlyVisual.Begin(source, target) : null;

            this.pendingCombineAmmo = null;
            RevertCombineSourceTint();
            this.isActive = false;

            if (!this.inventoryService.TryReload(ammo, slot))
            {
                flight?.Cancel();
                this.isActive = true;
                this.sfx?.PlayInvalidAction(gameObject);
                UpdateSelector();
                return;
            }

            if (flight == null) PlayUseFeedback(() => OnItemUsed?.Invoke(null, slot));
            else                flight.Launch(() => PlayUseFeedback(() => OnItemUsed?.Invoke(null, slot)));
        }

        private void RevertCombineSourceTint()
        {
            SelectorView.ClearCombineMarker(this.combineSourceView);
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
