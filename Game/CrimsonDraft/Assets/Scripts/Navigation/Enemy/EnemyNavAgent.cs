#nullable enable

using System;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyNavAgent : MonoBehaviour
    {
        [SerializeField] private NavigationEnemyData  data          = null!;
        [SerializeField] private EnemyPatrolPath      path          = null!;
        [SerializeField] private EnemyDetectionSensor sensor        = null!;
        [SerializeField] private Transform?           eyePoint;
        [Tooltip("Si está desactivado, el enemigo permanece estático en su posición inicial en lugar de patrullar.")]
        [SerializeField] private bool                 patrolEnabled        = true;
        [Tooltip("Velocidad de giro en grados/segundo durante el estado Suspicious.")]
        [SerializeField] private float                suspiciousTurnSpeed  = 120f;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<GuardAlertChangedEvent>?    guardAlertPublisher;
        private PlayerController?                      playerController;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private GuardAlertState  state           = GuardAlertState.Patrol;
        private float            suspiciousTimer;
        private IDisposable?     combatEndedSub;
        private bool             combatTriggered;
        private NavMeshPath navPathCache = null!;

        [Inject]
        public void Construct(
            ISceneTransitionService            sceneTransitionService,
            ISubscriber<CombatEndedEvent>      combatEndedSubscriber,
            IEncounterContext                  encounterContext,
            IPublisher<GuardAlertChangedEvent> guardAlertPublisher,
            PlayerController                  playerController)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.encounterContext       = encounterContext;
            this.guardAlertPublisher    = guardAlertPublisher;
            this.playerController       = playerController;
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

            if (patrolEnabled && path.HasWaypoints)
                navAgent.SetDestination(path.Current.position);

            combatEndedSub = combatEndedSubscriber?.Subscribe(OnCombatEnded);
        }

        private void OnDestroy()
        {
            combatEndedSub?.Dispose();
        }

        private void Update()
        {
            if (playerController == null) return;

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

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            combatTriggered = true;
            sceneTransitionService.StartCombatAsync(data.encounterId).Forget();
            gameObject.SetActive(false);
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!combatTriggered) return;
            if (!ev.Victory) return;
            gameObject.SetActive(false);
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
