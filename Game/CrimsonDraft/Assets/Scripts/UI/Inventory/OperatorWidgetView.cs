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
        [SerializeField] private TMP_Text?   deadLabel;

        [Header("Missing (no operator in this slot) / Dead")]
        [Tooltip("Whole card (portrait + grid). Faded for a slot with no operator (MIA).")]
        [SerializeField] private CanvasGroup? cardFade;
        [Tooltip("Portrait header only. Faded for a dead operator (KIA) -- the grid stays fully readable so the corpse can still be looted.")]
        [SerializeField] private CanvasGroup? headerFade;
        [SerializeField, Range(0f, 1f)] private float missingAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float deadAlpha    = 0.5f;

        [Header("HP ECG")]
        [SerializeField] private ECGSweepAnimator?  ecgLine;
        [SerializeField] private EcgHeartbeatPulse? ecgPulse;
        [Tooltip("CDG_Statis -- text readout of the same HP bands the ECG pulse uses.")]
        [SerializeField] private TMP_Text?          statusLabel;

        [Header("Weapon Slot 0 — Primary (4×1)")]
        [SerializeField] private GameObject weaponSlot0Root      = null!;
        [SerializeField] private Image       weaponSlot0Icon      = null!;
        [SerializeField] private TMP_Text?   weaponSlot0AmmoLabel;

        [Header("Melee Slot — permanently equipped (2×1)")]
        [SerializeField] private GameObject weaponSlot1Root = null!;
        [SerializeField] private Image      weaponSlot1Icon = null!;

        private MeleeWeaponData? currentMeleeData;
        private OperatorRuntime? boundOperator;

        public bool            HasMeleeWeapon => this.currentMeleeData != null;
        public MeleeWeaponData? MeleeData     => this.currentMeleeData;
        public RectTransform    MeleeSlotRoot => (RectTransform)this.weaponSlot1Root.transform;
        public RectTransform?   MeleeIconRect => this.weaponSlot1Icon != null ? this.weaponSlot1Icon.rectTransform : null;

        public void SetEquippedWeapon(WeaponItem? weapon, int slotIndex)
        {
            if (slotIndex == 0 && this.weaponSlot0Root != null)
                RefreshWeaponSlot(weapon, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
        }

        public void Bind(OperatorRuntime op, bool canLoot, string? corpseRoomId)
        {
            this.boundOperator = op;
            if (!op.IsPresent)
            {
                ShowMissing();
                return;
            }

            gameObject.SetActive(true);
            ClearMissing();
            if (this.headerFade != null) this.headerFade.alpha = op.IsAlive ? 1f : this.deadAlpha;

            if (this.portrait    != null) this.portrait.sprite = op.Data?.Portrait;
            if (this.nameLabel   != null) this.nameLabel.text  = op.Data?.CombatName ?? string.Empty;
            bool showDead = !op.IsAlive && !canLoot;
            if (this.deadOverlay != null) this.deadOverlay.SetActive(showDead);
            if (this.deadLabel   != null)
            {
                this.deadLabel.gameObject.SetActive(showDead);
                this.deadLabel.text = corpseRoomId != null ? $"RETRIEVE ITEMS IN\n{corpseRoomId}" : "KIA";
            }

            this.ApplyHealthState(op.HpRatio, op.IsAlive);

            if (this.weaponSlot0Root != null)
                RefreshWeaponSlot(op.PrimaryWeapon as WeaponItem, this.weaponSlot0Root, this.weaponSlot0Icon, this.weaponSlot0AmmoLabel);
            this.currentMeleeData = op.MeleeWeapon as MeleeWeaponData;
            if (this.weaponSlot1Root != null)
                RefreshMeleeSlot(op.MeleeWeapon, this.weaponSlot1Root, this.weaponSlot1Icon);
        }

        /// <summary>
        /// Slot with no operator (never recruited / not in the party): the card stays on screen
        /// faded, with "MIA" in the ECG status readout and no portrait/ECG line. It has no
        /// inventory container, so the cursor can't enter its grid (see InventoryPresenters).
        /// </summary>
        public void ShowMissing()
        {
            gameObject.SetActive(true);

            if (this.cardFade   != null) this.cardFade.alpha   = this.missingAlpha;
            if (this.headerFade != null) this.headerFade.alpha = 1f;

            if (this.portrait     != null) this.portrait.enabled = false;
            if (this.nameLabel    != null) this.nameLabel.text = string.Empty;
            if (this.deadOverlay  != null) this.deadOverlay.SetActive(false);
            if (this.deadLabel    != null) this.deadLabel.gameObject.SetActive(false);

            // The status readout (CDG_Statis) lives under the ECG background: keep that, drop the
            // trace line and stop the pulse (it has no health band to pulse for).
            if (this.statusLabel  != null) this.statusLabel.text = "MIA";
            if (this.ecgLine      != null) this.ecgLine.gameObject.SetActive(false);
            if (this.ecgPulse     != null) this.ecgPulse.enabled = false;
            if (this.weaponSlot0Root != null) this.weaponSlot0Root.SetActive(false);
            if (this.weaponSlot1Root != null) this.weaponSlot1Root.SetActive(false);
            this.currentMeleeData = null;
        }

        private void ClearMissing()
        {
            if (this.cardFade     != null) this.cardFade.alpha = 1f;
            if (this.portrait     != null) this.portrait.enabled = true;
            if (this.ecgLine      != null) this.ecgLine.gameObject.SetActive(true);
            if (this.ecgPulse     != null) this.ecgPulse.enabled = true;
        }

        private void ApplyHealthState(float hpRatio, bool isAlive = true)
        {
            if (this.ecgLine  != null) this.ecgLine.SetHealthState(hpRatio, isAlive, mercyApplies: false);
            if (this.ecgPulse != null) this.ecgPulse.SetHealthState(hpRatio);
            if (this.statusLabel != null)
            {
                this.statusLabel.text = GetStatusText(hpRatio, isAlive);
                // Same band color the ECG background uses (alpha stays as authored on the label).
                if (this.ecgPulse != null)
                {
                    var color = this.ecgPulse.BaseColor;
                    color.a = this.statusLabel.color.a;
                    this.statusLabel.color = color;
                }
            }
        }

        // Same bands as EcgHeartbeatPulse.SetHealthState (>75 / 50-75 / 25-50 / <=25).
        private static string GetStatusText(float hpRatio, bool isAlive)
        {
            if (!isAlive)                     return "KIA";
            if (hpRatio <= 0.25f)             return "Critical";
            if (hpRatio <= 0.50f)             return "Danger";
            if (hpRatio <= 0.75f)             return "Caution";
            return "Fine";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Test-only — previews the ECG states from the Inspector without touching real HP.
        [Button("100% — Fine")]    private void TestEcgFine()     => this.ApplyHealthState(1f);
        [Button("60% — Caution")]  private void TestEcgCaution()  => this.ApplyHealthState(0.6f);
        [Button("40% — Danger")]   private void TestEcgDanger()   => this.ApplyHealthState(0.4f);
        [Button("10% — Critical")] private void TestEcgCritical() => this.ApplyHealthState(0.1f);
        [Button("KIA")]            private void TestEcgKia()      => this.ApplyHealthState(0f, isAlive: false);

        // Test-only -- really kills / revives the operator bound to this card (Play Mode, inventory
        // open) so the full KIA look (faded header, flat ECG, KIA status, loot label) can be checked.
        [Button("Kill operator")]
        private void TestKillOperator()
        {
            var panel = GetComponentInParent<PartyPanelView>();
            if (this.boundOperator == null || panel == null) return;

            // First hit takes them to Mercy (0 HP, alive); the second one is the kill.
            for (int i = 0; i < 2 && this.boundOperator.IsAlive; i++)
                this.boundOperator.ApplyDamage(this.boundOperator.MaxHp * 2);
            panel.Refresh();
        }

        [Button("Revive operator")]
        private void TestReviveOperator()
        {
            var panel = GetComponentInParent<PartyPanelView>();
            if (this.boundOperator == null || panel == null || panel.Roster == null) return;

            var snapshot = panel.Roster.GetHpSnapshot();
            snapshot[this.boundOperator.SlotIndex] = this.boundOperator.MaxHp;
            panel.Roster.RestoreHp(snapshot);
            panel.Refresh();
        }
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
                if (w != null)
                {
                    ammoLabel.text  = w.CurrentAmmo.ToString();
                    ammoLabel.color = ItemCountColors.Loaded;
                }
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
