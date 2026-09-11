#nullable enable

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NaughtyAttributes;
using CrimsonDraft.Combat;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.UI
{
    public sealed class OperatorWidgetView : MonoBehaviour
    {
        [SerializeField] private Image       portrait    = null!;
        [SerializeField] private TMP_Text    nameLabel   = null!;
        [SerializeField] private GameObject  deadOverlay = null!;

        [Header("HP ECG")]
        [SerializeField] private ECGSweepAnimator?  ecgLine;
        [SerializeField] private EcgHeartbeatPulse? ecgPulse;

        [Header("Weapon Slot 0 — Primary (4×1)")]
        [SerializeField] private GameObject weaponSlot0Root      = null!;
        [SerializeField] private Image       weaponSlot0Icon      = null!;
        [SerializeField] private TMP_Text?   weaponSlot0AmmoLabel;

        [Header("Melee Slot — permanently equipped (2×1)")]
        [SerializeField] private GameObject weaponSlot1Root = null!;
        [SerializeField] private Image      weaponSlot1Icon = null!;

        private MeleeWeaponData? currentMeleeData;

        public bool            HasMeleeWeapon => this.currentMeleeData != null;
        public MeleeWeaponData? MeleeData     => this.currentMeleeData;
        public RectTransform    MeleeSlotRoot => (RectTransform)this.weaponSlot1Root.transform;

        public void SetEquippedWeapon(WeaponItem? weapon, int slotIndex)
        {
            if (slotIndex == 0 && this.weaponSlot0Root != null)
                RefreshWeaponSlot(weapon, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
        }

        public void Bind(OperatorRuntime op)
        {
            if (!op.IsPresent)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            if (this.portrait    != null) this.portrait.sprite = op.Data?.Portrait;
            if (this.nameLabel   != null) this.nameLabel.text  = op.Data?.DisplayName ?? string.Empty;
            if (this.deadOverlay != null) this.deadOverlay.SetActive(!op.IsAlive);

            this.ApplyHealthState(op.HpRatio, op.IsAlive);

            if (this.weaponSlot0Root != null)
                RefreshWeaponSlot(op.PrimaryWeapon as WeaponItem, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
            this.currentMeleeData = op.MeleeWeapon as MeleeWeaponData;
            if (this.weaponSlot1Root != null)
                RefreshMeleeSlot(op.MeleeWeapon, this.weaponSlot1Root, this.weaponSlot1Icon);
        }

        private void ApplyHealthState(float hpRatio, bool isAlive = true)
        {
            if (this.ecgLine  != null) this.ecgLine.SetHealthState(hpRatio, isAlive);
            if (this.ecgPulse != null) this.ecgPulse.SetHealthState(hpRatio);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test-only — previews the ECG states from the Inspector without touching real HP.
        [Button("100% — Stable")]  private void TestEcgStable()   => this.ApplyHealthState(1f);
        [Button("60% — Caution")]  private void TestEcgCaution()  => this.ApplyHealthState(0.6f);
        [Button("10% — Warning")]  private void TestEcgWarning()  => this.ApplyHealthState(0.1f);
        [Button("0% — Mercy")]     private void TestEcgMercy()     => this.ApplyHealthState(0f, isAlive: true);
        [Button("KIA")]            private void TestEcgKia()      => this.ApplyHealthState(0f, isAlive: false);
#endif

        private const int PrimarySlotCells = 4;
        private const int MeleeSlotCells   = 2;

        private static void RefreshWeaponSlot(WeaponItem? w, GameObject root, Image icon, TMP_Text? ammoLabel)
        {
            root.SetActive(true);
            if (icon == null) return;
            icon.enabled = w != null;
            icon.preserveAspect = true;
            if (w != null)
            {
                icon.sprite = w.Data.Icon;
                AnchorIconToGridSize(icon, w.Data.GridSize.x, PrimarySlotCells);
            }

            if (ammoLabel != null)
            {
                ammoLabel.gameObject.SetActive(w != null);
                if (w != null) ammoLabel.text = w.CurrentAmmo.ToString();
            }
        }

        private static void RefreshMeleeSlot(IMeleeWeapon? melee, GameObject root, Image icon)
        {
            root.SetActive(true);
            if (icon == null) return;
            icon.enabled = melee != null;
            icon.preserveAspect = true;
            if (melee != null)
            {
                icon.sprite = melee.Icon;
                AnchorIconToGridSize(icon, melee.GridSize.x, MeleeSlotCells);
            }
        }

        // Smaller weapons (e.g. a 2x1 pistol in the 4x1 primary slot) must render at their
        // real proportional size flush against the slot's right edge -- never stretched to
        // fill the whole slot, and never centered/floating inside it. Right-flush keeps the
        // occupied cells adjacent to the ammo counter regardless of weapon size (0001, 0011,
        // ... never 1000, 1100), which is also what the ammo label sits next to.
        private static void AnchorIconToGridSize(Image icon, int itemCells, int slotCells)
        {
            var iconRect = icon.rectTransform;
            if (iconRect.parent is not RectTransform slotRect) return;

            float fraction = slotCells > 0 ? Mathf.Clamp01((float)itemCells / slotCells) : 1f;
            iconRect.anchorMin        = new Vector2(1f, 0.5f);
            iconRect.anchorMax        = new Vector2(1f, 0.5f);
            iconRect.pivot            = new Vector2(1f, 0.5f);
            iconRect.sizeDelta        = new Vector2(slotRect.rect.width * fraction, slotRect.rect.height);
            iconRect.anchoredPosition = Vector2.zero;
        }
    }
}
