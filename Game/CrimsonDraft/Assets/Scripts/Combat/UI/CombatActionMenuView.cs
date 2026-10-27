#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Combat
{
    public sealed class CombatActionMenuView : MonoBehaviour, ICombatActionMenuView
    {
        #region Events

        public event Action<int>? OnOperatorSelected;
        public event Action<int>? OnOperatorFocused;

        #endregion

        #region Fields

        [SerializeField] private ActionMenuItem[]    operators       = Array.Empty<ActionMenuItem>();
        [SerializeField] private TMP_Text[]          operatorNameLabels = Array.Empty<TMP_Text>();
        [SerializeField] private TMP_Text[]          operatorAmmoLabels = Array.Empty<TMP_Text>();
        [SerializeField] private ECGSweepAnimator[]  operatorEcgAnimators = Array.Empty<ECGSweepAnimator>();
        [SerializeField] private Image[]          operatorWeaponIcons = Array.Empty<Image>();
        [SerializeField] private Image[]          operatorActionPendingIcons = Array.Empty<Image>();
        [SerializeField] private TMP_Text[]       operatorTurnOrderLabels = Array.Empty<TMP_Text>();
        [SerializeField] private RectTransform    selectorMark   = null!;
        [SerializeField] private Image       dimmingOverlay = null!;
        [SerializeField] private CanvasGroup operatorsGroup = null!;
        [SerializeField] private float pulseDuration = 0.6f;
        [SerializeField] private float pulseMinAlpha = 0.55f;
        [SerializeField] private Vector2 selectorPadding = new(8f, 8f);
        [Tooltip("Selector padding when it sits on an operator card (extra size over the card art, split evenly on both sides): 8 = 4 per side, tuned by eye so the visible frame sits 2px outside the card art.")]
        [SerializeField] private Vector2 cardSelectorPadding = new(8f, 8f);
        [SerializeField] private float resizeDuration = 0.12f;

        private Image selectorMarkImage = null!;
        private RectTransform? selectorFollowTarget;
        private Vector2        selectorFollowPadding;

        [Header("Inventory Lift")]
        [Tooltip("Canvas units the operator strip grows taller while the combat inventory is open: the inventory frame height (151.7) plus the 3px gap under the cards and the 3px border, minus what the cards do not follow.")]
        [SerializeField] private float inventoryExtraHeight = 154.7f;
        [SerializeField] private float inventoryLiftDuration = 0.2f;

        [Header("Weapon Icon")]
        [SerializeField] private float weaponIconCellSize = 32f; // px per GridSize unit (1×1 = 32px)

        [Header("Ammo Colors")]
        [SerializeField] private Color ammoFullColor    = new(0.247f, 0.663f, 1f, 1f);                       // #3FA9FF
        [SerializeField] private Color ammoPartialColor = new(0.6039216f, 0.62352943f, 0.36078432f, 1f);     // olive, same as the card tint
        [SerializeField] private Color ammoEmptyColor   = new(1f, 0.231f, 0.231f, 1f);                       // #FF3B3B

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();
        private bool     isMasterDimmed;
        private readonly HashSet<int> missingOperators = new();
        private readonly HashSet<int> dimmedOperators   = new(); // not ready: unselectable and skipped while navigating
        [Tooltip("Icon shown on the card of a slot with no operator (silhouette).")]
        [SerializeField] private Sprite? missingIcon;
        [SerializeField, Range(0f, 1f)] private float missingOverlayAlpha = 0.65f;
        private int      focusedOperatorIndex = -1;
        private int      lastPresentFocus     = -1;
        private readonly Dictionary<int, string>                 pendingNameByOperator  = new();
        private readonly Dictionary<int, (int current, int max)> pendingAmmoByOperator  = new();
        private readonly Dictionary<int, (float hpRatio, bool isAlive)> pendingHealthByOperator = new();
        private readonly Dictionary<int, WeaponItem?>            pendingWeaponByOperator = new();
        private readonly Dictionary<int, bool>                   pendingActionPendingByOperator = new();
        private readonly Dictionary<int, int>                    pendingTurnOrderByOperator = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.EnsureOperatorsWired();
            var le = this.selectorMark.GetComponent<LayoutElement>();
            if (le == null) le = this.selectorMark.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            this.selectorMarkImage = this.selectorMark.GetComponent<Image>();
            this.selectorMark.gameObject.SetActive(false);
            this.TryAutoWireOperatorNameLabels();
            this.TryAutoWireOperatorAmmoLabels();
            this.TryAutoWireOperatorEcgAnimators();
            this.TryAutoWireOperatorWeaponIcons();
            this.TryAutoWireOperatorActionPendingIcons();
            this.TryAutoWireOperatorTurnOrderLabels();
            this.ApplyPendingNameLabels();
            this.ApplyPendingAmmoLabels();
            this.ApplyPendingHealthIcons();
            this.ApplyPendingWeaponIcons();
            this.ApplyPendingActionPendingIcons();
            this.ApplyPendingTurnOrderLabels();
        }

        private void OnEnable()
        {
            this.submitHandlers   = new Action[this.operators.Length];
            this.selectedHandlers = new Action[this.operators.Length];
            this.ApplyPendingNameLabels();
            this.ApplyPendingAmmoLabels();
            this.ApplyPendingHealthIcons();
            this.ApplyPendingWeaponIcons();
            this.ApplyPendingActionPendingIcons();
            this.ApplyPendingTurnOrderLabels();

            for (int i = 0; i < this.operators.Length; i++)
            {
                int index = i;
                this.submitHandlers[i]   = () => this.OnOperatorSelected?.Invoke(index);
                this.selectedHandlers[i] = () =>
                {
                    if (this.isMasterDimmed) return;
                    if (this.missingOperators.Contains(index))
                    {
                        // Never lands on an absent slot: hand the focus back to the previous card.
                        if (this.lastPresentFocus >= 0)
                            this.FocusOperator(this.lastPresentFocus);
                        return;
                    }
                    this.lastPresentFocus = index;
                    this.SetFocusedOperatorIndex(index);
                    this.MoveSelector(index);
                    this.OnOperatorFocused?.Invoke(index);
                };

                this.operators[i].OnSubmit   += this.submitHandlers[i];
                this.operators[i].OnSelected += this.selectedHandlers[i];
            }

        }

        private void OnDisable()
        {
            this.selectorMark.DOKill();
            this.selectorMarkImage?.DOKill();
            this.selectorFollowTarget = null;
            if (this.inventoryLifted && this.inventoryLiftTarget != null)
                this.inventoryLiftTarget.sizeDelta = new Vector2(this.inventoryLiftTarget.sizeDelta.x, this.inventoryRestHeight);
            this.inventoryLiftTarget?.DOKill();
            this.inventoryLifted = false;

            for (int i = 0; i < this.operators.Length; i++)
            {
                this.operators[i].OnSubmit   -= this.submitHandlers[i];
                this.operators[i].OnSelected -= this.selectedHandlers[i];
            }

            this.submitHandlers   = Array.Empty<Action>();
            this.selectedHandlers = Array.Empty<Action>();
            this.focusedOperatorIndex = -1;
        }

        // The anchor can be mid-animation (e.g. OperatorFocusBounce lifting the newly
        // focused card) — re-sampling its position every frame instead of once keeps the
        // selector box glued to it instead of freezing at the pre-animation position.
        private void LateUpdate()
        {
            if (this.selectorFollowTarget == null || !this.selectorMark.gameObject.activeSelf)
                return;

            var parentRect   = (RectTransform)this.selectorMark.parent;
            Vector3 localPos = parentRect.InverseTransformPoint(this.selectorFollowTarget.position);
            this.selectorMark.localPosition = new Vector3(localPos.x, localPos.y, 0f);

            // The size is only tweened once, when the selector moves: if the canvas scale or the card layout
            // settles afterwards (or the tween ran under slow-mo) it would stay at a stale size, so after the
            // tween it keeps matching the target every frame.
            if (!DOTween.IsTweening(this.selectorMark))
                this.selectorMark.sizeDelta = this.ComputeSelectorSize(this.selectorFollowTarget, this.selectorFollowPadding);
        }

        #endregion

        #region Private

        // The OperatorOverview cards get deleted/duplicated while tuning the layout, which leaves the
        // serialized 'operators' array with holes and the left/right navigation pointing at destroyed
        // cards. Rebuild both from whatever cards are actually under the operators group, in order.
        private void EnsureOperatorsWired()
        {
            if (this.operatorsGroup == null) return;

            var found = this.operatorsGroup.GetComponentsInChildren<ActionMenuItem>(true);
            bool valid = found.Length == this.operators.Length;
            for (int i = 0; valid && i < found.Length; i++)
                valid = this.operators[i] == found[i];

            if (!valid)
                this.operators = found;

            for (int i = 0; i < this.operators.Length; i++)
            {
                var nav = this.operators[i].navigation;
                nav.mode          = Navigation.Mode.Explicit;
                nav.selectOnLeft  = i > 0 ? this.operators[i - 1] : null;
                nav.selectOnRight = i < this.operators.Length - 1 ? this.operators[i + 1] : null;
                this.operators[i].navigation = nav;
            }
        }

        private RectTransform? inventoryLiftTarget;
        private float          inventoryRestHeight;
        private bool           inventoryLifted;

        // Makes the operator strip taller (it is pinned at the bottom, so it grows upward and its
        // top-pinned cards ride up with it), leaving room below the cards for the combat inventory;
        // lowering plays the same animation backwards.
        public void SetInventoryLift(bool lifted, Action? onComplete = null)
        {
            if (this.operatorsGroup == null) { onComplete?.Invoke(); return; }

            if (this.inventoryLiftTarget == null)
            {
                this.inventoryLiftTarget = this.operatorsGroup.transform.parent as RectTransform;
                if (this.inventoryLiftTarget == null) { onComplete?.Invoke(); return; }
            }

            var target = this.inventoryLiftTarget;

            if (this.inventoryLifted == lifted)
            {
                onComplete?.Invoke();
                return;
            }

            target.DOKill();
            this.inventoryLifted = lifted;

            if (!lifted)
            {
                target.DOSizeDelta(new Vector2(target.sizeDelta.x, this.inventoryRestHeight), this.inventoryLiftDuration)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke());
                return;
            }

            this.inventoryRestHeight = target.sizeDelta.y;

            // Authored in canvas units; the strip may be scaled relative to the canvas.
            var root         = target.GetComponentInParent<Canvas>().rootCanvas.transform;
            float localScale = Mathf.Max(target.lossyScale.y, 0.0001f) / Mathf.Max(root.lossyScale.y, 0.0001f);
            float fullHeight = this.inventoryRestHeight + this.inventoryExtraHeight / localScale;

            target.DOSizeDelta(new Vector2(target.sizeDelta.x, fullHeight), this.inventoryLiftDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }

        public void RestoreOperatorFocus(int index) => this.SetFocusedOperatorIndex(index);

        private void MoveSelector(int index) =>
            MoveSelectorTo(this.GetOperatorOverviewRect(index));

        private void SetFocusedOperatorIndex(int index)
        {
            if (this.focusedOperatorIndex == index) return;

            this.GetOperatorBounce(this.focusedOperatorIndex)?.SetFocused(false);
            this.focusedOperatorIndex = index;
            this.GetOperatorBounce(index)?.SetFocused(true);
        }

        private OperatorFocusBounce? GetOperatorBounce(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent;
            return overview == null ? null : overview.GetComponent<OperatorFocusBounce>();
        }

        private void TryAutoWireOperatorNameLabels()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorNameLabels != null && this.operatorNameLabels.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorNameLabels[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorNameLabels = new TMP_Text[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var nameNode = overview.Find("OperatorName/Label");
                if (nameNode == null)
                    continue;

                this.operatorNameLabels[i] = nameNode.GetComponent<TMP_Text>();
            }
        }

        private void TryAutoWireOperatorAmmoLabels()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorAmmoLabels != null && this.operatorAmmoLabels.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorAmmoLabels[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorAmmoLabels = new TMP_Text[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var ammoNode = overview.Find("WeaponAmmo");
                if (ammoNode == null)
                    continue;

                this.operatorAmmoLabels[i] = ammoNode.GetComponent<TMP_Text>();
            }
        }

        private void TryAutoWireOperatorEcgAnimators()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorEcgAnimators != null && this.operatorEcgAnimators.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorEcgAnimators[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorEcgAnimators = new ECGSweepAnimator[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var ecgNode = overview.Find("ECG_BG");
                if (ecgNode == null)
                    continue;

                this.operatorEcgAnimators[i] = ecgNode.GetComponent<ECGSweepAnimator>();
            }
        }

        private void TryAutoWireOperatorWeaponIcons()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorWeaponIcons != null && this.operatorWeaponIcons.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorWeaponIcons[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorWeaponIcons = new Image[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var weaponNode = overview.Find("Weapon");
                if (weaponNode == null)
                    continue;

                this.operatorWeaponIcons[i] = weaponNode.GetComponent<Image>();
            }
        }

        private void TryAutoWireOperatorActionPendingIcons()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorActionPendingIcons != null && this.operatorActionPendingIcons.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorActionPendingIcons[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorActionPendingIcons = new Image[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var actionNode = overview.parent?.Find("Tactical/Action"); // Tactical is the card root's child, next to Visual
                if (actionNode == null)
                    continue;

                this.operatorActionPendingIcons[i] = actionNode.GetComponent<Image>();
            }
        }

        private void TryAutoWireOperatorTurnOrderLabels()
        {
            if (this.operators.Length == 0)
                return;

            bool hasAssignedAll = this.operatorTurnOrderLabels != null && this.operatorTurnOrderLabels.Length >= this.operators.Length;
            if (hasAssignedAll)
            {
                bool allFilled = true;
                for (int i = 0; i < this.operators.Length; i++)
                {
                    if (this.operatorTurnOrderLabels[i] == null)
                    {
                        allFilled = false;
                        break;
                    }
                }

                if (allFilled)
                    return;
            }

            this.operatorTurnOrderLabels = new TMP_Text[this.operators.Length];
            for (int i = 0; i < this.operators.Length; i++)
            {
                var item = this.operators[i];
                if (item == null)
                    continue;

                var overview = item.transform.parent;
                if (overview == null)
                    continue;

                var turnOrderNode = overview.parent?.Find("Tactical/Action/turn-order");
                if (turnOrderNode == null)
                    continue;

                this.operatorTurnOrderLabels[i] = turnOrderNode.GetComponent<TMP_Text>();
            }
        }

        #endregion

        #region ICombatActionMenuView

        public void FocusOperator(int index)
        {
            if (index >= 0 && index < this.operators.Length)
                FocusNextFrame(index).Forget();
        }

        // Guards against multiple operators becoming ready in the same ATB tick each
        // scheduling their own deferred focus (SetOperatorDimmed's needsFocus check sees a
        // stale null selection for all of them since none of the deferred calls has run yet)
        // — without this, whichever one resolves last silently steals focus from the first.
        private bool focusRequestPending;

        private async UniTaskVoid FocusNextFrame(int index)
        {
            this.focusRequestPending = true;
            await UniTask.DelayFrame(2);
            this.focusRequestPending = false;
            EventSystem.current.SetSelectedGameObject(this.operators[index].gameObject);
        }

        public void ClearFocus()
        {
            EventSystem.current?.SetSelectedGameObject(null);
            this.selectorMark.DOKill();
            this.selectorMarkImage?.DOKill();
            this.selectorFollowTarget = null;
            this.selectorMark.gameObject.SetActive(false);
        }

        // Drops the card's focus-lift without waiting for roster navigation to land on a
        // different operator — used once a command has actually been given (Shoot/Items/
        // Melee), since the card shouldn't stay raised for the rest of its turn while
        // it's no longer the one being browsed/decided on.
        public void ReleaseOperatorFocus(int index)
        {
            this.GetOperatorBounce(index)?.SetFocused(false);
            if (this.focusedOperatorIndex == index)
                this.focusedOperatorIndex = -1;
        }

        // Fire-and-forget: plays the shared use-flipbook over the operator's whole card.
        // Purely decorative, so nothing in the combat flow waits on it.
        public void PlayActionFeedback(int index) =>
            this.GetOperatorActionFeedback(index)?.Play();

        private OperatorActionFeedback? GetOperatorActionFeedback(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent;
            return overview == null ? null : overview.GetComponentInChildren<OperatorActionFeedback>(true);
        }

        // Pushed every frame from CombatOrchestrator as the operator's ATB gauge ticks
        // toward ready; purely visual, no gameplay state lives here.
        public void SetOperatorGauge(int index, float gauge01) =>
            this.GetOperatorGaugeBar(index)?.SetGauge01(this.missingOperators.Contains(index) ? 0f : gauge01);

        private OperatorGaugeBar? GetOperatorGaugeBar(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent;
            // The ATB bar lives in the card root's Tactical strip, not under Visual.
            return overview == null || overview.parent == null ? null : overview.parent.GetComponentInChildren<OperatorGaugeBar>(true);
        }

        public RectTransform GetOperatorAnchor(int index) =>
            this.GetOperatorOverviewRect(index);

        public RectTransform GetOperatorRect(int index) =>
            (RectTransform)this.operators[index].transform;

        // The "Visual" root keeps its original 130x72 footprint, while the card's art
        // (Black_BG) is what actually covers the portrait/ECG/weapon layout -- the command
        // panel and the selector box must hug that, not the stale root rect.
        public IReadOnlyList<RectTransform> GetOperatorOverviewRects()
        {
            var rects = new RectTransform[this.operators.Length];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = this.GetOperatorOverviewRect(i);
            return rects;
        }

        public RectTransform GetOperatorOverviewRect(int index)
        {
            var visual = (RectTransform)this.operators[index].transform.parent;
            var cardBounds = visual.Find(CardArtName) as RectTransform;
            return cardBounds != null ? cardBounds : visual;
        }

        public void SetDimmed(bool dimmed)
        {
            this.isMasterDimmed = dimmed;

            if (this.dimmingOverlay != null)
                this.dimmingOverlay.DOFade(dimmed ? 0.6f : 0f, 0.1f);

            if (this.operatorsGroup != null)
            {
                this.operatorsGroup.interactable   = !dimmed;
                this.operatorsGroup.blocksRaycasts = !dimmed;
            }

            if (dimmed)
            {
                this.selectorMark.DOKill();
                this.selectorMarkImage?.DOKill();
                this.selectorFollowTarget = null;
                this.selectorMark.gameObject.SetActive(false);
            }
        }

        public void SetOperatorDimmed(int index, bool dimmed)
        {
            if (index < 0 || index >= this.operators.Length) return;
            var item = this.operators[index];
            if (this.missingOperators.Contains(index)) return; // stays locked and dim, see SetOperatorMissing
            item.interactable = !dimmed;
            if (dimmed) this.dimmedOperators.Add(index);
            else        this.dimmedOperators.Remove(index);

            // The neighbours' links must skip this card too (or stop skipping it), not just its own.
            this.RefreshOperatorNavigation();

            if (!dimmed && EventSystem.current != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                bool needsFocus = !this.focusRequestPending && (selected == null ||
                    (selected.TryGetComponent<UnityEngine.UI.Selectable>(out var sel) && !sel.IsInteractable()));
                if (needsFocus)
                    FocusNextFrame(index).Forget();
            }

            Transform overview = item.transform.parent;
            if (overview == null) return;
            CanvasGroup cg = overview.GetComponent<CanvasGroup>();
            if (cg == null) cg = overview.gameObject.AddComponent<CanvasGroup>();
            cg.DOFade(dimmed ? 0.4f : 1f, 0.15f);

            var highlight = overview.GetComponent<OperatorTurnHighlight>();
            if (highlight != null) highlight.SetActive(!dimmed);
        }

        public bool IsOperatorFocused(int index)
        {
            if (index < 0 || index >= this.operators.Length) return false;

            if (this.focusedOperatorIndex == index) return true;

            return EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == this.operators[index].gameObject;
        }

        public void SetOperatorName(int index, string name)
        {
            if (this.missingOperators.Contains(index)) name = "????";
            this.pendingNameByOperator[index] = name;

            if (index < 0 || index >= this.operatorNameLabels.Length)
                return;

            var label = this.operatorNameLabels[index];
            if (label == null)
                return;

            label.text = name;
        }

        private void ApplyPendingNameLabels()
        {
            foreach (var kvp in this.pendingNameByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorNameLabels.Length)
                    continue;

                var label = this.operatorNameLabels[index];
                if (label == null)
                    continue;

                label.text = kvp.Value;
            }
        }

        public void SetOperatorLogo(int index, Sprite? logo)
        {
            if (logo == null || index < 0 || index >= this.operators.Length) return;

            // "Logo" is a child of Visual (the parent of the ActionMenuItem), so it moves with the focus bounce.
            var visual = this.operators[index].transform.parent;
            var image = visual != null ? visual.Find("Logo")?.GetComponent<Image>() : null;
            if (image != null) image.sprite = logo;
        }

        public void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo)
        {
            if (this.missingOperators.Contains(index)) return;
            this.pendingAmmoByOperator[index] = (currentAmmo, maxAmmo);

            if (index < 0 || index >= this.operatorAmmoLabels.Length)
                return;

            var label = this.operatorAmmoLabels[index];
            if (label == null)
                return;

            ApplyAmmoLabel(label, currentAmmo, maxAmmo);
        }

        private void ApplyPendingAmmoLabels()
        {
            foreach (var kvp in this.pendingAmmoByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorAmmoLabels.Length)
                    continue;

                var label = this.operatorAmmoLabels[index];
                if (label == null)
                    continue;

                ApplyAmmoLabel(label, kvp.Value.current, kvp.Value.max);
            }
        }

        // Only the rounds left are shown (no "/max"): blue when the magazine is full, red when empty,
        // olive in between.
        private void ApplyAmmoLabel(TMP_Text label, int currentAmmo, int maxAmmo)
        {
            int current = Mathf.Max(0, currentAmmo);
            int max     = Mathf.Max(1, maxAmmo);

            label.text  = current.ToString();
            label.color = current == 0 ? this.ammoEmptyColor
                : current >= max       ? this.ammoFullColor
                :                        this.ammoPartialColor;
        }

        public int OperatorSlotCount => this.operators.Length;

        // Slot with no operator: silhouette icon, "????" name, flatline ECG with "MIA", no weapon/ammo, empty turn
        // bar, and a dark overlay over the whole card. Never focusable.
        public void SetOperatorMissing(int index, bool missing)
        {
            if (index < 0 || index >= this.operators.Length) return;

            if (missing) this.missingOperators.Add(index);
            else         this.missingOperators.Remove(index);

            var item = this.operators[index];
            item.interactable = !missing;

            // Left/right links are explicit, so neighbours would still land on it: route them around the card.
            this.RefreshOperatorNavigation();

            var visual = item.transform.parent;
            if (visual == null) return;

            if (index < this.operatorEcgAnimators.Length && this.operatorEcgAnimators[index] != null)
                this.operatorEcgAnimators[index].enabled = !missing;

            // Alpha 0 (not SetActive): nothing of the placeholder art may show.
            foreach (var partName in MissingHiddenParts)
                SetPartHidden(visual.Find(partName), missing);
            SetPartHidden(visual.parent?.Find("Tactical/Action"), missing);
            SetPartHidden(visual.Find("Logo"), missing);

            var iconImage = visual.Find("Icon")?.GetComponent<Image>();
            if (iconImage != null)
            {
                if (missing)
                {
                    if (!this.originalIcons.ContainsKey(index)) this.originalIcons[index] = iconImage.sprite;
                    if (this.missingIcon != null) iconImage.sprite = this.missingIcon;
                    iconImage.preserveAspect = true;
                }
                else if (this.originalIcons.Remove(index, out var original))
                {
                    iconImage.sprite = original;
                }
            }

            if (missing)
            {
                const string unknownName = "????";
                this.pendingNameByOperator[index] = unknownName;
                if (index < this.operatorNameLabels.Length && this.operatorNameLabels[index] != null)
                    this.operatorNameLabels[index].text = unknownName;

                if (index < this.operatorAmmoLabels.Length && this.operatorAmmoLabels[index] != null)
                    this.operatorAmmoLabels[index].text = string.Empty;
                if (index < this.operatorWeaponIcons.Length && this.operatorWeaponIcons[index] != null)
                    this.operatorWeaponIcons[index].enabled = false;
                this.pendingAmmoByOperator.Remove(index);
                this.pendingWeaponByOperator.Remove(index);

                this.SetOperatorTurnOrder(index, -1);
                this.GetOperatorGaugeBar(index)?.SetGauge01(0f);

                // The ECG is switched off (animator stopped, trace and glow hidden); the status readout says MIA.
                var status = visual.Find("ECG_BG/CDG_Status")?.GetComponent<TMP_Text>();
                if (status != null) status.text = "MIA";
            }

            // The card is dimmed by a dark frame on top (like a dead operator's inventory), not by fading it.
            var overlay = visual.Find(MissingOverlayName) as RectTransform;
            if (overlay == null && missing)
            {
                var go = new GameObject(MissingOverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                overlay = (RectTransform)go.transform;
                overlay.SetParent(visual, false);
                var bounds = visual.Find("Black_BG") as RectTransform;
                if (bounds != null)
                {
                    overlay.anchorMin = bounds.anchorMin;
                    overlay.anchorMax = bounds.anchorMax;
                    overlay.pivot     = bounds.pivot;
                    overlay.offsetMin = bounds.offsetMin;
                    overlay.offsetMax = bounds.offsetMax;
                }
                else
                {
                    overlay.anchorMin = Vector2.zero;
                    overlay.anchorMax = Vector2.one;
                    overlay.offsetMin = overlay.offsetMax = Vector2.zero;
                }
                var overlayImage = go.GetComponent<Image>();
                overlayImage.color         = new Color(0f, 0f, 0f, this.missingOverlayAlpha);
                overlayImage.raycastTarget = false;
            }
            if (overlay != null)
            {
                overlay.gameObject.SetActive(missing);
                overlay.SetAsLastSibling();
            }

            var group = visual.GetComponent<CanvasGroup>();
            if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>();
            group.DOKill();
            group.alpha = 1f;

            if (!missing)
            {
                if (this.pendingHealthByOperator.TryGetValue(index, out var health))
                    this.SetOperatorHealth(index, health.hpRatio, health.isAlive);
                this.ApplyPendingNameLabels();
            }
        }

        private const string MissingOverlayName = "MissingOverlay";
        private static readonly string[] MissingHiddenParts = { "Weapon", "WeaponAmmo", "ECG_BG/ECG_move", "ECG_BG/Effect" };
        private readonly Dictionary<int, Sprite?> originalIcons = new();

        private static void SetPartHidden(Transform? part, bool hidden)
        {
            if (part == null) return;
            var group = part.GetComponent<CanvasGroup>();
            if (group == null) group = part.gameObject.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
        }

        // Next card in that direction the player can actually land on: present and ready. A card that is not
        // ready used to stay reachable through the explicit links (Selectable.Navigate only checks IsActive), and
        // since it has no links of its own the cursor got stuck on it.
        private ActionMenuItem? NextPresentOperator(int from, int step)
        {
            for (int i = from + step; i >= 0 && i < this.operators.Length; i += step)
                if (!this.missingOperators.Contains(i) && !this.dimmedOperators.Contains(i)) return this.operators[i];
            return null;
        }

        // Ready cards link left/right to the next ready card; cards that are not ready are not navigable at all.
        private void RefreshOperatorNavigation()
        {
            for (int i = 0; i < this.operators.Length; i++)
            {
                if (this.missingOperators.Contains(i)) continue;
                var nav = this.operators[i].navigation;
                if (this.dimmedOperators.Contains(i))
                {
                    nav.mode = Navigation.Mode.None;
                }
                else
                {
                    nav.mode          = Navigation.Mode.Explicit;
                    nav.selectOnLeft  = this.NextPresentOperator(i, -1);
                    nav.selectOnRight = this.NextPresentOperator(i, +1);
                }
                this.operators[i].navigation = nav;
            }
        }

        public void SetOperatorHealth(int index, float hpRatio, bool isAlive)
        {
            this.pendingHealthByOperator[index] = (hpRatio, isAlive);
            if (this.missingOperators.Contains(index)) return;

            if (index < 0 || index >= this.operatorEcgAnimators.Length)
                return;

            var animator = this.operatorEcgAnimators[index];
            if (animator == null)
                return;

            animator.SetHealthState(hpRatio, isAlive);
        }

        // Fire-and-forget: a quick punch on the card itself, distinct from the ECG line's
        // own health-state color/sprite (SetOperatorHealth) — this is the "ficha got hit"
        // reaction, played once per impact rather than tied to the current HP value.
        public void PlayOperatorDamageShake(int index) =>
            this.GetOperatorCardShake(index)?.PlayDamageShake();

        // Same fire-and-forget shot as the shake above, but on the ECG line's own CRT-static
        // burst -- reuses the array SetOperatorHealth already indexes into, no extra wiring.
        public void PlayOperatorHealFlash(int index)
        {
            if (index < 0 || index >= this.operatorEcgAnimators.Length) return;
            this.operatorEcgAnimators[index]?.FlashHeal();
        }

        public void PlayOperatorDamageGlitch(int index)
        {
            if (index < 0 || index >= this.operatorEcgAnimators.Length) return;
            this.operatorEcgAnimators[index]?.PlayDamageGlitch();
        }

        private OperatorCardShake? GetOperatorCardShake(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent; // "Visual"
            return overview == null ? null : overview.parent?.GetComponent<OperatorCardShake>();
        }

        private void ApplyPendingHealthIcons()
        {
            foreach (var kvp in this.pendingHealthByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorEcgAnimators.Length || this.missingOperators.Contains(index))
                    continue;

                var animator = this.operatorEcgAnimators[index];
                if (animator == null)
                    continue;

                animator.SetHealthState(kvp.Value.hpRatio, kvp.Value.isAlive);
            }
        }

        // weapon is the operator's ActiveWeapon (== PrimaryWeapon — the only firearm slot).
        public void SetOperatorWeapon(int index, WeaponItem? weapon)
        {
            this.pendingWeaponByOperator[index] = weapon;

            if (index < 0 || index >= this.operatorWeaponIcons.Length)
                return;

            var icon = this.operatorWeaponIcons[index];
            if (icon == null)
                return;

            ApplyWeaponIcon(icon, weapon);
        }

        private void ApplyPendingWeaponIcons()
        {
            foreach (var kvp in this.pendingWeaponByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorWeaponIcons.Length)
                    continue;

                var icon = this.operatorWeaponIcons[index];
                if (icon == null)
                    continue;

                ApplyWeaponIcon(icon, kvp.Value);
            }
        }

        // Shown while the operator has an action sitting in CombatActionQueue waiting its
        // turn (submitted Shoot/Item/Melee, not yet resolved) — hidden again once that
        // action actually leaves the queue. Starts off in the prefab: nobody has a queued
        // action until they act.
        public void SetOperatorActionPending(int index, bool pending)
        {
            this.pendingActionPendingByOperator[index] = pending;

            if (index < 0 || index >= this.operatorActionPendingIcons.Length)
                return;

            var icon = this.operatorActionPendingIcons[index];
            if (icon == null)
                return;

            // The "Action" node starts inactive in the prefab (it hosts both this icon and the
            // turn-order number as a child) -- toggling just Image.enabled left the whole badge
            // invisible forever since the GameObject itself never turned on.
            icon.gameObject.SetActive(pending);
        }

        private void ApplyPendingActionPendingIcons()
        {
            foreach (var kvp in this.pendingActionPendingByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorActionPendingIcons.Length)
                    continue;

                var icon = this.operatorActionPendingIcons[index];
                if (icon == null)
                    continue;

                icon.gameObject.SetActive(kvp.Value);
            }
        }

        // Shows the operator's 0-based position in CombatActionQueue (enemy actions ahead of
        // it count too, e.g. 2 enemy actions queued before it makes this "2"; 0 means it's
        // next) so the player can tell how long until a submitted command actually resolves.
        // position < 0 means "not queued" and clears the label -- pushed every frame from
        // CombatOrchestrator.RefreshTurnOrderLabels.
        public void SetOperatorTurnOrder(int index, int position)
        {
            if (this.missingOperators.Contains(index)) position = -1;
            this.pendingTurnOrderByOperator[index] = position;

            if (index < 0 || index >= this.operatorTurnOrderLabels.Length)
                return;

            var label = this.operatorTurnOrderLabels[index];
            if (label == null)
                return;

            ApplyTurnOrderLabel(label, position);
        }

        private void ApplyPendingTurnOrderLabels()
        {
            foreach (var kvp in this.pendingTurnOrderByOperator)
            {
                int index = kvp.Key;
                if (index < 0 || index >= this.operatorTurnOrderLabels.Length)
                    continue;

                var label = this.operatorTurnOrderLabels[index];
                if (label == null)
                    continue;

                ApplyTurnOrderLabel(label, kvp.Value);
            }
        }

        private static void ApplyTurnOrderLabel(TMP_Text label, int position) =>
            label.text = position >= 0 ? position.ToString() : string.Empty;

        private void ApplyWeaponIcon(Image icon, WeaponItem? weapon)
        {
            icon.enabled = weapon != null;
            if (weapon == null) return;

            icon.sprite         = weapon.Data.Icon;
            icon.preserveAspect = true;

            icon.rectTransform.sizeDelta = new Vector2(
                weapon.Data.GridSize.x * this.weaponIconCellSize,
                weapon.Data.GridSize.y * this.weaponIconCellSize);
        }

        // anchor is treated as "the rect to cover", not just a point — the selector box
        // resizes to hug it (roster cards, command rows, and item rows are all different
        // sizes) instead of staying pinned at whatever size it last had.
        private const string CardArtName = "Black_BG"; // GetOperatorOverviewRect: the card's art, what the selector hugs

        private Vector2 ComputeSelectorSize(RectTransform anchor, Vector2 padding)
        {
            // Measured in the selector's own local space. This canvas' world scale is tiny (~7e-5), so dividing a
            // world distance by lossyScale (clamped to 1e-4) made the box come out ~27% too small.
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 bottomLeft = this.selectorMark.InverseTransformPoint(corners[0]);
            Vector3 topRight   = this.selectorMark.InverseTransformPoint(corners[2]);
            return new Vector2(
                Mathf.Abs(topRight.x - bottomLeft.x) + padding.x,
                Mathf.Abs(topRight.y - bottomLeft.y) + padding.y);
        }

        public void MoveSelectorTo(RectTransform anchor)
        {
            this.selectorMark.gameObject.SetActive(true);
            this.selectorMark.DOKill();
            this.selectorMarkImage?.DOKill();
            this.selectorFollowTarget  = anchor;
            this.selectorFollowPadding = anchor.name == CardArtName ? this.cardSelectorPadding : this.selectorPadding;

            var parentRect   = (RectTransform)this.selectorMark.parent;
            Vector3 localPos = parentRect.InverseTransformPoint(anchor.position);
            this.selectorMark.localPosition = new Vector3(localPos.x, localPos.y, 0f);

            this.selectorMark.DOSizeDelta(this.ComputeSelectorSize(anchor, this.selectorFollowPadding), this.resizeDuration)
                .SetUpdate(true); // unscaled: combat opens in slow-mo and the box must not lag behind the card

            if (this.selectorMarkImage == null) return;

            Color c = this.selectorMarkImage.color;
            c.a = 1f;
            this.selectorMarkImage.color = c;
            this.selectorMarkImage.DOFade(this.pulseMinAlpha, this.pulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        #endregion
    }
}
