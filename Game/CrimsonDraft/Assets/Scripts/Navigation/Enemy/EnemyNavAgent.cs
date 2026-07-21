#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Combat;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyNavAgent : MonoBehaviour
    {
        [SerializeField] private NavigationEnemyData  data          = null!;
        [SerializeField] private EncounterData        encounterData = null!;
        [SerializeField] private string               encounterId   = string.Empty;

        public string        EncounterId  => this.encounterId;
        public EncounterData? EncounterData => this.encounterData;
        [SerializeField] private EnemyPatrolPath      path          = null!;
        [SerializeField] private EnemyDetectionSensor sensor        = null!;
        [SerializeField] private Transform?           eyePoint;
        [Tooltip("Si está desactivado, el enemigo permanece estático en su posición inicial en lugar de patrullar.")]
        [SerializeField] private bool                 patrolEnabled        = true;
        [Tooltip("Velocidad de giro en grados/segundo durante el estado Suspicious.")]
        [SerializeField] private float                suspiciousTurnSpeed  = 120f;

        [Header("Attack")]
        [SerializeField] private Animator?  animator;
        [Tooltip("Nombres de estado del Animator (Base Layer) reproducidos al alcanzar al jugador; se elige uno al azar.")]
        [SerializeField] private string[]   attackStateNames     = { "Armature|Attack_1 0", "Armature|Attack_2", "Armature|Attack_3" };
        [Tooltip("Tope de seguridad por si la animación de ataque no reporta normalizedTime >= 1 (p. ej. clip en loop).")]
        [SerializeField] private float      attackAnimationTimeout = 2.5f;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private ISubscriber<DialogueActiveChangedEvent>? dialogueSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<GuardAlertChangedEvent>?    guardAlertPublisher;
        private PlayerController?                      playerController;
        private EnemyStateRegistry?                    enemyStateRegistry;
        private string                                 enemyKey = string.Empty;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private GuardAlertState  state           = GuardAlertState.Patrol;
        private float            suspiciousTimer;
        private IDisposable?     combatEndedSub;
        private IDisposable?     dialogueSub;
        private bool             combatTriggered;
        private bool             isAttacking;
        private bool             dialoguePaused;
        private NavMeshPath navPathCache = null!;

        public void Construct(
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<GuardAlertChangedEvent>      guardAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      enemyStateRegistry,
            string                                  enemyKey)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.guardAlertPublisher    = guardAlertPublisher;
            this.playerController       = playerController;
            this.enemyStateRegistry     = enemyStateRegistry;
            this.enemyKey               = enemyKey;
        }

        private void Start()
        {
            navPathCache = new NavMeshPath();
            navAgent = GetComponent<NavMeshAgent>();
            playerRb = playerController!.GetComponent<Rigidbody>();
            if (playerRb == null)
            {
                Debug.LogError($"[EnemyNavAgent] PlayerController on '{playerController.name}' has no Rigidbody. Sound detection will NullRef.", this);
                enabled = false;
                return;
            }
            navAgent.speed = data.patrolSpeed;
            if (animator == null)
                animator = GetComponent<Animator>();

            if (patrolEnabled && path.HasWaypoints)
                navAgent.SetDestination(path.Current.position);

            combatEndedSub = combatEndedSubscriber?.Subscribe(OnCombatEnded);
            dialogueSub    = dialogueSubscriber?.Subscribe(OnDialogueActiveChanged);
        }

        private void OnDestroy()
        {
            combatEndedSub?.Dispose();
            dialogueSub?.Dispose();
        }

        private void Update()
        {
            if (playerController == null) return;
            if (dialoguePaused) return;

            switch (state)
            {
                case GuardAlertState.Patrol:     UpdatePatrol();     break;
                case GuardAlertState.Suspicious: UpdateSuspicious(); break;
                case GuardAlertState.Alert:      UpdateAlert();      break;
            }
        }

        private void UpdatePatrol()
        {
            if (patrolEnabled
                && path.HasWaypoints
                && !navAgent.pathPending
                && navAgent.hasPath
                && navAgent.remainingDistance < data.waypointStopDistance)
            {
                path.Advance();
                navAgent.SetDestination(path.Current.position);
            }

            if (!Detect()) return;

            if (data.suspiciousEnabled)
                TransitionTo(GuardAlertState.Suspicious);
            else if (CanReachPlayer())
                TransitionTo(GuardAlertState.Alert);
        }

        private void UpdateSuspicious()
        {
            var dir = (playerController!.transform.position - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, suspiciousTurnSpeed * Time.deltaTime);
            }

            suspiciousTimer -= Time.deltaTime;

            if (Detect() && CanReachPlayer())
            {
                TransitionTo(GuardAlertState.Alert);
                return;
            }

            if (suspiciousTimer <= 0f)
                TransitionTo(GuardAlertState.Patrol);
        }

        private void UpdateAlert()
        {
            if (isAttacking) return;

            navAgent.SetDestination(playerController!.transform.position);

            var toPlayer = playerController.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude < data.catchRadius)
                TriggerCombat();
        }

        private bool Detect()
            => sensor.Evaluate(data, playerController!.transform, playerRb, eyePoint);

        private bool CanReachPlayer()
        {
            NavMesh.CalculatePath(
                transform.position,
                playerController!.transform.position,
                NavMesh.AllAreas,
                navPathCache);
            return navPathCache.status == NavMeshPathStatus.PathComplete;
        }

        private void TransitionTo(GuardAlertState next)
        {
            var prev = state;
            state = next;

            guardAlertPublisher?.Publish(new GuardAlertChangedEvent
            {
                GuardId       = gameObject.name,
                PreviousState = prev,
                NewState      = next,
            });

            switch (next)
            {
                case GuardAlertState.Patrol:
                    navAgent.isStopped = false;
                    navAgent.speed = data.patrolSpeed;
                    navAgent.updateRotation = true;
                    sensor.ResetState();
                    if (patrolEnabled && path.HasWaypoints)
                        navAgent.SetDestination(path.Current.position);
                    break;

                case GuardAlertState.Suspicious:
                    navAgent.ResetPath();
                    navAgent.isStopped = true;
                    navAgent.updateRotation = false;
                    suspiciousTimer = data.suspiciousDuration;
                    break;

                case GuardAlertState.Alert:
                    navAgent.isStopped = false;
                    navAgent.updateRotation = true;
                    navAgent.speed = data.chaseSpeed;
                    break;
            }
        }

        public void NotifyCombatTriggered()
        {
            this.combatTriggered = true;
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            if (isAttacking) return;

            isAttacking = true;
            this.combatTriggered = true;
            PlayAttackThenStartCombatAsync().Forget();
        }

        private async UniTaskVoid PlayAttackThenStartCombatAsync()
        {
            navAgent.isStopped = true;
            FacePlayer();

            if (animator != null && attackStateNames.Length > 0)
            {
                var stateName = attackStateNames[UnityEngine.Random.Range(0, attackStateNames.Length)];
                animator.Play(stateName, 0, 0f);

                await UniTask.Yield(); // let the Animator enter the new state before reading its info
                var elapsed = 0f;
                while (elapsed < attackAnimationTimeout)
                {
                    var info = animator.GetCurrentAnimatorStateInfo(0);
                    if (info.IsName(stateName) && info.normalizedTime >= 1f)
                        break;
                    elapsed += Time.deltaTime;
                    await UniTask.Yield();
                }
            }

            if (sceneTransitionService == null) return;
            sceneTransitionService.StartCombatAsync(this.encounterId, this.encounterData).Forget();
            gameObject.SetActive(false);
        }

        private void FacePlayer()
        {
            if (playerController == null) return;
            var dir = playerController.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);
        }

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent ev)
        {
            dialoguePaused = ev.IsActive;
            navAgent.isStopped = ev.IsActive || state is GuardAlertState.Suspicious;
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!combatTriggered) return;
            if (!ev.Victory) return;
            gameObject.SetActive(false);
            this.enemyStateRegistry?.SetDefeated(this.enemyKey);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (data == null) return;

            var pos       = transform.position;
            var eyeOrigin = eyePoint != null ? eyePoint.position : pos;

            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(pos, data.catchRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(pos, data.detectRadius);

            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(pos, data.undetectRadius);

            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(pos, data.walkSoundRadius);

            Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
            Gizmos.DrawWireSphere(pos, data.runSoundRadius);

            DrawVisualFovGizmo(eyeOrigin, data.visualRange, data.visualFov);
        }

        private void DrawVisualFovGizmo(Vector3 origin, float range, float fov)
        {
            const int arcSegments = 24;

            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
            var halfFov   = fov * 0.5f;
            var leftEdge  = Quaternion.Euler(0f, -halfFov, 0f) * transform.forward;
            var rightEdge = Quaternion.Euler(0f,  halfFov, 0f) * transform.forward;

            Gizmos.DrawLine(origin, origin + leftEdge  * range);
            Gizmos.DrawLine(origin, origin + rightEdge * range);

            var previous = origin + leftEdge * range;
            for (var i = 1; i <= arcSegments; i++)
            {
                var angle   = -halfFov + fov * (i / (float)arcSegments);
                var dir     = Quaternion.Euler(0f, angle, 0f) * transform.forward;
                var next    = origin + dir * range;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
#endif
    }
}
