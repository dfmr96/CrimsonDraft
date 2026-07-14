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
        [SerializeField] private RectTransform    selectorMark   = null!;
        [SerializeField] private Image       dimmingOverlay = null!;
        [SerializeField] private CanvasGroup operatorsGroup = null!;
        [SerializeField] private float bobAmplitude = 4f;
        [SerializeField] private float bobDuration  = 0.4f;

        [Header("Weapon Icon")]
        [SerializeField] private float weaponIconCellSize = 32f; // px per GridSize unit (1×1 = 32px)

        private Action[] submitHandlers   = Array.Empty<Action>();
        private Action[] selectedHandlers = Array.Empty<Action>();
        private bool     isMasterDimmed;
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

            for (int i = 0; i < this.operators.Length; i++)
            {
                this.operators[i].OnSubmit   -= this.submitHandlers[i];
                this.operators[i].OnSelected -= this.selectedHandlers[i];
            }

            this.submitHandlers   = Array.Empty<Action>();
            this.selectedHandlers = Array.Empty<Action>();
        }

        #endregion

        #region Private

        private void MoveSelector(int index) =>
            MoveSelectorTo(this.operators[index].SelectorAnchor);

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
            this.selectorMark.gameObject.SetActive(false);
        }

        public RectTransform GetOperatorAnchor(int index) =>
            this.operators[index].SelectorAnchor;

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

        public void MoveSelectorTo(RectTransform anchor)
        {
            this.selectorMark.gameObject.SetActive(true);
            this.selectorMark.DOKill();

            var parentRect   = (RectTransform)this.selectorMark.parent;
            Vector3 localPos = parentRect.InverseTransformPoint(anchor.position);

            float centerX = localPos.x;
            float centerY = localPos.y;

            this.selectorMark.localPosition = new Vector3(centerX, centerY - this.bobAmplitude, 0f);
            this.selectorMark.DOLocalMoveY(centerY + this.bobAmplitude, this.bobDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        #endregion
    }
}
