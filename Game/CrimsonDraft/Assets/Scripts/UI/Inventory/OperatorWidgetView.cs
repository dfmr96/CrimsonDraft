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

        [Header("Weapon Slot 1 — Secondary (2×1)")]
        [SerializeField] private GameObject weaponSlot1Root      = null!;
        [SerializeField] private Image       weaponSlot1Icon      = null!;
        [SerializeField] private TMP_Text?   weaponSlot1AmmoLabel;

        public void SetEquippedWeapon(WeaponItem? weapon, int slotIndex)
        {
            if (slotIndex == 0 && this.weaponSlot0Root != null)
                RefreshWeaponSlot(weapon, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
            else if (slotIndex == 1 && this.weaponSlot1Root != null)
                RefreshWeaponSlot(weapon, this.weaponSlot1Root, this.weaponSlot1Icon, this.weaponSlot1AmmoLabel);
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

            this.ApplyHealthState(op.HpRatio);

            if (this.weaponSlot0Root != null)
                RefreshWeaponSlot(op.PrimaryWeapon   as WeaponItem, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
            if (this.weaponSlot1Root != null)
                RefreshWeaponSlot(op.SecondaryWeapon as WeaponItem, this.weaponSlot1Root, this.weaponSlot1Icon, this.weaponSlot1AmmoLabel);
        }

        private void ApplyHealthState(float hpRatio)
        {
            if (this.ecgLine  != null) this.ecgLine.SetHealthState(hpRatio);
            if (this.ecgPulse != null) this.ecgPulse.SetHealthState(hpRatio);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test-only — previews the 4 ECG health bands from the Inspector without touching real HP.
        [Button("100% — Stable")]   private void TestEcgStable()   => this.ApplyHealthState(1f);
        [Button("60% — Caution")]   private void TestEcgCaution()  => this.ApplyHealthState(0.6f);
        [Button("35% — Warning")]   private void TestEcgWarning()  => this.ApplyHealthState(0.35f);
        [Button("10% — Critical")]  private void TestEcgCritical() => this.ApplyHealthState(0.1f);
#endif

        private static void RefreshWeaponSlot(WeaponItem? w, GameObject root, Image icon, TMP_Text? ammoLabel)
        {
            root.SetActive(true);
            if (icon == null) return;
            icon.enabled = w != null;
            if (w != null) icon.sprite = w.Data.Icon;

            if (ammoLabel != null)
            {
                ammoLabel.gameObject.SetActive(w != null);
                if (w != null) ammoLabel.text = w.CurrentAmmo.ToString();
            }
        }
    }
}
