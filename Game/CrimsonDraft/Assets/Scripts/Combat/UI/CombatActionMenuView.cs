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
        [SerializeField] private TMP_Text[]          operatorAmmoLabels = Array.Empty<TMP_Text>();
        [SerializeField] private ECGSweepAnimator[]  operatorEcgAnimators = Array.Empty<ECGSweepAnimator>();
        [SerializeField] private Image[]          operatorWeaponIcons = Array.Empty<Image>();
        [SerializeField] private Image[]          operatorFocusFireMarkers = Array.Empty<Image>();
        [SerializeField] private RectTransform    selectorMark   = null!;
        [SerializeField] private Image       dimmingOverlay = null!;
        [SerializeField] private CanvasGroup operatorsGroup = null!;
        [SerializeField] private float pulseDuration = 0.6f;
        [SerializeField] private float pulseMinAlpha = 0.55f;
        [SerializeField] private Vector2 selectorPadding = new(8f, 8f);
        [SerializeField] private float resizeDuration = 0.12f;

        private Image selectorMarkImage = null!;
        private RectTransform? selectorFollowTarget;

        [Header("Weapon Icon")]
        [SerializeField] private float weaponIconCellSize = 32f; // px per GridSize unit (1×1 = 32px)

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();
        private bool     isMasterDimmed;
        private int      focusedOperatorIndex = -1;
        private readonly Dictionary<int, (int current, int max)> pendingAmmoByOperator  = new();
        private readonly Dictionary<int, float>                  pendingHealthByOperator = new();
        private readonly Dictionary<int, WeaponItem?>            pendingWeaponByOperator = new();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            var le = this.selectorMark.GetComponent<LayoutElement>();
            if (le == null) le = this.selectorMark.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            this.selectorMarkImage = this.selectorMark.GetComponent<Image>();
            this.selectorMark.gameObject.SetActive(false);
            this.TryAutoWireOperatorAmmoLabels();
            this.TryAutoWireOperatorEcgAnimators();
            this.TryAutoWireOperatorWeaponIcons();
            this.ApplyPendingAmmoLabels();
            this.ApplyPendingHealthIcons();
            this.ApplyPendingWeaponIcons();
        }

        private void OnEnable()
        {
            this.submitHandlers   = new Action[this.operators.Length];
            this.selectedHandlers = new Action[this.operators.Length];
            this.ApplyPendingAmmoLabels();
            this.ApplyPendingHealthIcons();
            this.ApplyPendingWeaponIcons();

            for (int i = 0; i < this.operators.Length; i++)
            {
                int index = i;
                this.submitHandlers[i]   = () => this.OnOperatorSelected?.Invoke(index);
                this.selectedHandlers[i] = () =>
                {
                    if (this.isMasterDimmed) return;
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
        }

        #endregion

        #region Private

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
        // FocusFire), since the card shouldn't stay raised for the rest of its turn while
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
            this.GetOperatorGaugeBar(index)?.SetGauge01(gauge01);

        private OperatorGaugeBar? GetOperatorGaugeBar(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent;
            return overview == null ? null : overview.GetComponentInChildren<OperatorGaugeBar>(true);
        }

        // Grows/shrinks the operator card's own border to make room for the command
        // panel above it, instead of the command panel having its own separate frame.
        public void ExpandOperatorBorder(int index, bool expanded, Action? onComplete = null)
        {
            var panel = this.GetOperatorBorderPanel(index);
            if (panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            panel.SetExpanded(expanded, onComplete);
        }

        private OperatorBorderPanel? GetOperatorBorderPanel(int index)
        {
            if (index < 0 || index >= this.operators.Length) return null;
            var overview = this.operators[index].transform.parent;
            return overview == null ? null : overview.GetComponentInChildren<OperatorBorderPanel>(true);
        }

        public RectTransform GetOperatorAnchor(int index) =>
            this.GetOperatorOverviewRect(index);

        public RectTransform GetOperatorRect(int index) =>
            (RectTransform)this.operators[index].transform;

        public RectTransform GetOperatorOverviewRect(int index) =>
            (RectTransform)this.operators[index].transform.parent;

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
            item.interactable = !dimmed;

            var nav  = item.navigation;
            nav.mode = dimmed ? Navigation.Mode.None : Navigation.Mode.Automatic;
            item.navigation = nav;

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

        public void SetOperatorFocusFireMarked(int index, bool marked)
        {
            if (index < 0 || index >= this.operatorFocusFireMarkers.Length) return;
            var marker = this.operatorFocusFireMarkers[index];
            if (marker != null) marker.gameObject.SetActive(marked);
        }

        public void SetOperatorAmmo(int index, int currentAmmo, int maxAmmo)
        {
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

        private static void ApplyAmmoLabel(TMP_Text label, int currentAmmo, int maxAmmo)
        {
            int current = Mathf.Max(0, currentAmmo);
            int max = Mathf.Max(1, maxAmmo);
            if (current == 0)
            {
                label.text = $"<color=#FF3B3B>0</color>/{max}";
                return;
            }

            label.text = $"{current}/{max}";
        }

        public void SetOperatorHealth(int index, float hpRatio)
        {
            this.pendingHealthByOperator[index] = hpRatio;

            if (index < 0 || index >= this.operatorEcgAnimators.Length)
                return;

            var animator = this.operatorEcgAnimators[index];
            if (animator == null)
                return;

            animator.SetHealthState(hpRatio);
        }

        // Fire-and-forget: a quick punch on the card itself, distinct from the ECG line's
        // own health-state color/sprite (SetOperatorHealth) — this is the "ficha got hit"
        // reaction, played once per impact rather than tied to the current HP value.
        public void PlayOperatorDamageShake(int index) =>
            this.GetOperatorCardShake(index)?.PlayDamageShake();

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
                if (index < 0 || index >= this.operatorEcgAnimators.Length)
                    continue;

                var animator = this.operatorEcgAnimators[index];
                if (animator == null)
                    continue;

                animator.SetHealthState(kvp.Value);
            }
        }

        // weapon is the operator's ActiveWeapon (PrimaryWeapon ?? SecondaryWeapon) —
        // primary is always preferred when both slots are equipped.
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
        public void MoveSelectorTo(RectTransform anchor)
        {
            this.selectorMark.gameObject.SetActive(true);
            this.selectorMark.DOKill();
            this.selectorMarkImage?.DOKill();
            this.selectorFollowTarget = anchor;

            var parentRect   = (RectTransform)this.selectorMark.parent;
            Vector3 localPos = parentRect.InverseTransformPoint(anchor.position);
            this.selectorMark.localPosition = new Vector3(localPos.x, localPos.y, 0f);

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            float worldWidth  = Vector3.Distance(corners[0], corners[3]);
            float worldHeight = Vector3.Distance(corners[0], corners[1]);
            Vector3 selectorScale = this.selectorMark.lossyScale;
            float targetWidth  = worldWidth  / Mathf.Max(selectorScale.x, 0.0001f) + this.selectorPadding.x;
            float targetHeight = worldHeight / Mathf.Max(selectorScale.y, 0.0001f) + this.selectorPadding.y;
            this.selectorMark.DOSizeDelta(new Vector2(targetWidth, targetHeight), this.resizeDuration);

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
