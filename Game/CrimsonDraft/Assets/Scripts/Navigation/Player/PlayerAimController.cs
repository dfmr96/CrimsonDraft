#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Enemy;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private float    aimTurnSpeed  = 180f;
        [SerializeField] private float    aimRange      = 20f;
        [SerializeField] private LayerMask obstaclesMask;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private Animator animator = null;

        [Header("Weapon Visuals")]
        [SerializeField] private GameObject pistolWeapon  = null!; // "Glock 1"
        [SerializeField] private GameObject shotgunWeapon = null!; // "Shotgun 1"

        [Header("Shotgun Aim Pose (relative to its parent hand bone)")]
        [SerializeField] private Vector3 shotgunAimLocalPosition;
        [SerializeField] private Vector3 shotgunAimLocalEulerAngles;
        [SerializeField, Range(0.5f, 20f)] private float aimPoseBlendSpeed = 6f; // ~1/6 s to fully blend

        private IInputService            inputService           = null!;
        private ISceneTransitionService  sceneTransitionService = null!;
        private EnemyNavAgent[]          cachedEnemies          = null!;
        private PlayerController         playerController       = null!;

        private readonly List<EnemyNavAgent> targets = new();
        private int   currentTargetIndex;
        private float cycleCooldown;
        private bool  previousAxisActive;

        private const int PlayerOperatorSlot = 0; // mirrors PlayerController.PlayerOperatorSlot
        private IOperatorRoster? roster;
        private Vector3          shotgunRestLocalPosition;
        private Quaternion       shotgunRestLocalRotation;
        private float            aimPoseWeight; // 0 = resting pose, 1 = aim pose
        private int              healthOverlayLayerIndex = -1;

            [Inject]
            public void Construct(
                IInputService           inputService,
                ISceneTransitionService sceneTransitionService,
                EnemyNavAgent[]         cachedEnemies,
                IOperatorRoster         roster)
            {
                this.inputService           = inputService;
                this.sceneTransitionService = sceneTransitionService;
                this.cachedEnemies          = cachedEnemies;
                this.roster                 = roster;
            }

            private void Start()
            {
                this.playerController = GetComponent<PlayerController>();

                this.shotgunRestLocalPosition = this.shotgunWeapon.transform.localPosition;
                this.shotgunRestLocalRotation = this.shotgunWeapon.transform.localRotation;

                // The "Health Overlay" layer is a full-body Override layer (no avatar mask), so at
                // weight 1 it completely replaces whatever the Base Layer is doing -- including the
                // aim pose -- whenever HealthState != 0 (yellow/orange/danger). We silence it while
                // aiming (see EnterAim/ExitAim) so the player can still aim while wounded, and restore
                // it as soon as aiming stops.
                this.healthOverlayLayerIndex = this.animator.GetLayerIndex("Health Overlay");
            }

            private void Update()
            {
                UpdateWeaponVisual();

                if (this.cycleCooldown > 0f)
                    this.cycleCooldown -= Time.deltaTime;

                if (this.inputService.Aim.WasPressedThisFrame())
                    EnterAim();
                else if (this.inputService.Aim.WasReleasedThisFrame())
                    ExitAim();
                else if (this.playerController.IsAiming && !this.inputService.Aim.IsPressed())
                    ExitAim();

                if (!this.playerController.IsAiming) return;

                RotateTowardTarget();
                HandleCycle();
                HandleFire();
            }

private void EnterAim()
        {
            BuildTargetList();
            this.currentTargetIndex = 0;
            this.playerController.SetAiming(true);
            // Cancel a still-pending AimExit before it can fire late (e.g. queued while we were
            // mid-clip on a state with no AimExit transition) and cancel this new aim right after
            // it starts, even though the button is held down again by now.
            this.animator.ResetTrigger("AimExit");
            this.animator.SetTrigger("AimEnter");

            // Let the aim pose show fully even while wounded -- otherwise the Health Overlay layer
            // (full body, weight 1) would keep overriding the arms and the aim animation would never
            // be visible in yellow/orange/danger.
            if (this.healthOverlayLayerIndex >= 0)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 0f);
        }

private void ExitAim()
        {
            this.targets.Clear();
            this.playerController.SetAiming(false);
            // Symmetric to EnterAim -- clear a stale pending AimEnter so a quick press/release/
            // press doesn't leave an old "start aiming" queued up to fire after this exit.
            this.animator.ResetTrigger("AimEnter");
            this.animator.SetTrigger("AimExit");

            // Restore the wounded look (a no-op if HealthState is currently 0/Normal).
            if (this.healthOverlayLayerIndex >= 0)
                this.animator.SetLayerWeight(this.healthOverlayLayerIndex, 1f);
        }

                    // Keeps the visible weapon model in sync with whatever weapon is currently equipped in
            // the inventory: activates the mesh matching the equipped GunType and deactivates the
            // other one (or both, if nothing is equipped). Also gives the shotgun model a distinct
            // local position/rotation while aiming that blends back to its original local transform
            // (cached at Start) as soon as the player stops aiming.
            private void UpdateWeaponVisual()
            {
                IWeaponSlot? activeWeapon = this.roster != null && this.roster.IsInitialized
                    ? this.roster[PlayerOperatorSlot].ActiveWeapon
                    : null;

                bool isShotgunFamily = activeWeapon != null
                    && activeWeapon.GunType is GunType.Shotgun or GunType.REShotgun;
                bool isPistolFamily = activeWeapon != null
                    && activeWeapon.GunType is GunType.Pistols or GunType.REPistols;

                if (this.pistolWeapon.activeSelf != isPistolFamily)
                    this.pistolWeapon.SetActive(isPistolFamily);
                if (this.shotgunWeapon.activeSelf != isShotgunFamily)
                    this.shotgunWeapon.SetActive(isShotgunFamily);

                bool wantsAimPose = isShotgunFamily && this.playerController.IsAiming;
                this.aimPoseWeight = Mathf.MoveTowards(
                    this.aimPoseWeight, wantsAimPose ? 1f : 0f, this.aimPoseBlendSpeed * Time.deltaTime);

                var shotgunTransform = this.shotgunWeapon.transform;
                shotgunTransform.localPosition = Vector3.Lerp(
                    this.shotgunRestLocalPosition, this.shotgunAimLocalPosition, this.aimPoseWeight);
                shotgunTransform.localRotation = Quaternion.Slerp(
                    this.shotgunRestLocalRotation, Quaternion.Euler(this.shotgunAimLocalEulerAngles), this.aimPoseWeight);
            }

private void BuildTargetList()
        {
            this.targets.Clear();
            foreach (var enemy in this.cachedEnemies)
            {
                if (enemy == null) continue;
                if (!enemy.gameObject.activeInHierarchy) continue;
                var nav = enemy.GetComponent<NavMeshAgent>();
                if (nav == null || !nav.enabled) continue;
                this.targets.Add(enemy);
            }

            var playerPos = transform.position;
            this.targets.Sort((a, b) =>
                (a.transform.position - playerPos).sqrMagnitude
                    .CompareTo((b.transform.position - playerPos).sqrMagnitude));
        }

        private void RotateTowardTarget()
        {
            if (this.targets.Count == 0) return;

            var target = this.targets[this.currentTargetIndex];
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                BuildTargetList();
                this.currentTargetIndex = 0;
                if (this.targets.Count == 0) return;
                target = this.targets[0];
            }

            var dir = target.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            var targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, this.aimTurnSpeed * Time.deltaTime);
        }

        private void HandleCycle()
        {
            if (this.targets.Count <= 1) return;
            if (this.cycleCooldown > 0f) return;

            var x          = this.inputService.Move.ReadValue<Vector2>().x;
            var axisActive = Mathf.Abs(x) > 0.5f;

            if (axisActive && !this.previousAxisActive)
            {
                if (x > 0f)
                    this.currentTargetIndex = (this.currentTargetIndex + 1) % this.targets.Count;
                else
                    this.currentTargetIndex = (this.currentTargetIndex - 1 + this.targets.Count) % this.targets.Count;

                this.cycleCooldown = 0.3f;
            }

            this.previousAxisActive = axisActive;
        }

        private void HandleFire()
        {
            if (!this.inputService.AimFire.WasPressedThisFrame()) return;
            if (this.targets.Count == 0) return;
            if (this.sceneTransitionService.IsInCombat) return;

            var target = this.targets[this.currentTargetIndex];
            if (target == null || !target.gameObject.activeInHierarchy) return;

            var encounterData = target.EncounterData;
            if (encounterData == null) return;

            var origin  = transform.position + Vector3.up * 0.8f;
            var forward = transform.forward;

            if (!Physics.Raycast(origin, forward, out var hit, this.aimRange, this.obstaclesMask | this.enemyMask))
                return;

            var hitEnemy = hit.collider.GetComponentInParent<EnemyNavAgent>();
            if (hitEnemy != target)
            {
                UnityEngine.Debug.LogWarning($"[PlayerAimController] Raycast hit '{hit.collider.name}' (layer {hit.collider.gameObject.layer}) instead of target '{target.name}'. Check obstaclesMask/enemyMask layer configuration.");
                return;
            }

            this.animator.SetTrigger("Shoot");
            target.NotifyCombatTriggered();
            ExitAim();
            this.sceneTransitionService.StartCombatAsync(
                target.EncounterId,
                encounterData,
                operatorsStartFull: true).Forget();
        }
    }
}
