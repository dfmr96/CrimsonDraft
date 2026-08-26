#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Navigation.Player
{
    public sealed class PlayerAimController : MonoBehaviour
    {
        [SerializeField] private float    aimTurnSpeed  = 180f;
        [SerializeField] private float    aimRange      = 20f;
        [SerializeField] private LayerMask obstaclesMask;
        [SerializeField] private LayerMask enemyMask;
        [SerializeField] private Animator animator = null;
        

        private IInputService            inputService           = null!;
        private ISceneTransitionService  sceneTransitionService = null!;
        private EnemyNavAgent[]          cachedEnemies          = null!;
        private PlayerController         playerController       = null!;

        private readonly List<EnemyNavAgent> targets = new();
        private int   currentTargetIndex;
        private float cycleCooldown;
        private bool  previousAxisActive;

        [Inject]
        public void Construct(
            IInputService           inputService,
            ISceneTransitionService sceneTransitionService,
            EnemyNavAgent[]         cachedEnemies)
        {
            this.inputService           = inputService;
            this.sceneTransitionService = sceneTransitionService;
            this.cachedEnemies          = cachedEnemies;
        }

        private void Start()
        {
            this.playerController = GetComponent<PlayerController>();
        }

        private void Update()
        {
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
            this.animator.SetTrigger("AimEnter");
        }

        private void ExitAim()
        {
            this.targets.Clear();
            this.playerController.SetAiming(false);
            this.animator.SetTrigger("AimExit");

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
