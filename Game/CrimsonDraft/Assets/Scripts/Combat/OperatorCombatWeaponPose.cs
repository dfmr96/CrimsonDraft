#nullable enable

using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Drives the "Operator_Combat_Controller v2" Animator parameters for a single operator on the
    /// battlefield, and blends the shotgun model between its rest (idle) and aim local pose -- the same
    /// technique used for the shotgun in navigation (see PlayerAimController: shotgunRestLocalPosition /
    /// shotgunAimLocalPosition blended by aimPoseWeight). The Health Overlay layer is a full-body
    /// Override layer with no avatar mask, so it is silenced (weight 0) while aiming, exactly like
    /// PlayerAimController does, so a wounded idle pose never fights the aim pose.
    /// </summary>
    public sealed class OperatorCombatWeaponPose : MonoBehaviour
    {
        [SerializeField] private Animator animator = null!;

        [Header("Weapon Visuals")]
        [SerializeField] private GameObject pistolWeapon = null!;  // e.g. "Glock 1"
        [SerializeField] private GameObject shotgunWeapon = null!; // e.g. "Shotgun 1"

        [Header("Melee Weapon Visual")]
        [SerializeField] private GameObject knifeWeapon = null!; // e.g. "Knife 1" -- only shown during EnterMelee/ExitMelee

        [Header("Shotgun Aim Pose (relative to its parent hand bone)")]
        [SerializeField] private Vector3 shotgunAimLocalPosition;
        [SerializeField] private Vector3 shotgunAimLocalEulerAngles;
        [SerializeField, Range(0.5f, 20f)] private float aimPoseBlendSpeed = 6f; // ~1/6 s to fully blend

        private Vector3 shotgunRestLocalPosition;
        private Quaternion shotgunRestLocalRotation;
        private float aimPoseWeight; // 0 = resting pose, 1 = aim pose
        private bool isAiming;
        private bool isShotgunEquipped;
        private int healthOverlayLayerIndex = -1;

        private static readonly int GunTypeHash = Animator.StringToHash("GunType");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private static readonly int ShootHash = Animator.StringToHash("Shoot");
        private static readonly int ReloadHash = Animator.StringToHash("Reload");
        private static readonly int KnifeAttackHash = Animator.StringToHash("KnifeAttack");
        private static readonly int FlinchHash = Animator.StringToHash("Flinch");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HealthStateHash = Animator.StringToHash("HealthState");

        private void Awake()
        {
            this.shotgunRestLocalPosition = this.shotgunWeapon.transform.localPosition;
            this.shotgunRestLocalRotation = this.shotgunWeapon.transform.localRotation;

            // "Health Overlay" is a full-body Override layer (no avatar mask), so at weight 1 it
            // completely replaces whatever the Base Layer is doing -- including the aim pose --
            // whenever HealthState != 0 (yellow/orange/danger). We silence it while aiming (see
            // EnterAim/ExitAim) so the operator can still aim cleanly while wounded, and restore it
            // as soon as aiming stops.
            this.healthOverlayLayerIndex = this.animator.GetLayerIndex("Health Overlay");
        }

        private void Update()
        {
            if (!this.isShotgunEquipped) return;

            float target = (this.isShotgunEquipped && this.isAiming) ? 1f : 0f;
            this.aimPoseWeight = Mathf.MoveTowards(this.aimPoseWeight, target, this.aimPoseBlendSpeed * Time.deltaTime);

            var shotgunTransform = this.shotgunWeapon.transform;
            shotgunTransform.localPosition = Vector3.Lerp(
                this.shotgunRestLocalPosition, this.shotgunAimLocalPosition, this.aimPoseWeight);
            shotgunTransform.localRotation = Quaternion.Slerp(
                this.shotgunRestLocalRotation, Quaternion.Euler(this.shotgunAimLocalEulerAngles), this.aimPoseWeight);
        }

        /// <summary>Equips the given weapon family: swaps the visible weapon model and updates the Animator's GunType.</summary>
public void SetGunType(GunType gunType)
        {
            this.isShotgunEquipped = gunType is GunType.Shotgun or GunType.REShotgun;
            this.ApplyEquippedWeaponVisibility();
            this.animator.SetInteger(GunTypeHash, this.isShotgunEquipped ? 1 : 2);
        }

/// <summary>Shows whichever weapon model matches the currently equipped GunType (pistol xor shotgun), hiding the other. Used both by SetGunType and to restore visibility after a melee swing hides both.</summary>
        private void ApplyEquippedWeaponVisibility()
        {
            if (this.pistolWeapon.activeSelf == this.isShotgunEquipped)
                this.pistolWeapon.SetActive(!this.isShotgunEquipped);
            if (this.shotgunWeapon.activeSelf != this.isShotgunEquipped)
                this.shotgunWeapon.SetActive(this.isShotgunEquipped);
        }


        /// <summary>Call when the operator starts aiming (matches PlayerAimController.EnterAim).</summary>
        public void EnterAim()
        {
            this.isAiming = true;
            this.animator.SetBool(AimHash, true);

            if (this.healthOverlayLayerIndex >= 0)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 0f);
        }

        /// <summary>Call when the operator stops aiming (matches PlayerAimController.ExitAim).</summary>
        public void ExitAim()
        {
            this.isAiming = false;
            this.animator.SetBool(AimHash, false);

            // Restore the wounded look (a no-op if HealthState is currently 0/Full).
            if (this.healthOverlayLayerIndex >= 0)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 1f);
        }

        public void TriggerShoot() => this.animator.SetTrigger(ShootHash);

        public void TriggerReload() => this.animator.SetTrigger(ReloadHash);

        public void TriggerKnifeAttack() => this.animator.SetTrigger(KnifeAttackHash);

/// <summary>Call right before a melee/knife swing plays -- hides whichever gun (pistol or shotgun) is currently equipped, since the operator is using the knife, not the gun, for this attack.</summary>
/// <summary>Call right before a melee/knife swing plays -- hides whichever gun (pistol or shotgun) is currently equipped and shows the knife model instead, since the operator is using the knife for this attack.</summary>
        public void EnterMelee()
        {
            if (this.pistolWeapon.activeSelf)
                this.pistolWeapon.SetActive(false);
            if (this.shotgunWeapon.activeSelf)
                this.shotgunWeapon.SetActive(false);
            if (!this.knifeWeapon.activeSelf)
                this.knifeWeapon.SetActive(true);
        }

        /// <summary>Call once the melee/knife swing finishes -- restores whichever gun matches the equipped GunType.</summary>
/// <summary>Call once the melee/knife swing finishes -- hides the knife again and restores whichever gun matches the equipped GunType.</summary>
        public void ExitMelee()
        {
            if (this.knifeWeapon.activeSelf)
                this.knifeWeapon.SetActive(false);
            this.ApplyEquippedWeaponVisibility();
        }


        public void TriggerFlinch() => this.animator.SetTrigger(FlinchHash);

        public void TriggerDeath() => this.animator.SetTrigger(DeathHash);

        /// <summary>0 = Full, 1 = Yellow, 2 = Orange, 3 = Danger -- drives the Health Overlay layer's idle pose.</summary>
        public void SetHealthState(int healthState) =>
            this.animator.SetInteger(HealthStateHash, Mathf.Clamp(healthState, 0, 3));
    }
}
