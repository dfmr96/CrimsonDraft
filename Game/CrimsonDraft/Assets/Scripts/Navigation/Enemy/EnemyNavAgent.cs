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
        [SerializeField] private NavigationEnemyData  data      = null!;
        [SerializeField] private EnemyPatrolPath      path      = null!;
        [SerializeField] private EnemyDetectionSensor sensor    = null!;
        [SerializeField] private Transform?           eyePoint;

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
                Debug.LogError($"[EnemyNavAgent] PlayerController on '{playerController.name}' has no Rigidbody. Sound detection will NullRef.", this);
            navAgent.speed = data.patrolSpeed;

            if (path.HasWaypoints)
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
            if (path.HasWaypoints
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
                transform.rotation = Quaternion.LookRotation(dir.normalized);

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

            var distToPlayer = Vector3.Distance(transform.position, playerController.transform.position);
            if (distToPlayer < data.catchRadius)
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
                    navAgent.speed = data.patrolSpeed;
                    navAgent.updateRotation = true;
                    sensor.ResetState();
                    if (path.HasWaypoints)
                        navAgent.SetDestination(path.Current.position);
                    break;

                case GuardAlertState.Suspicious:
                    navAgent.ResetPath();
                    navAgent.updateRotation = false;
                    suspiciousTimer = data.suspiciousDuration;
                    break;

                case GuardAlertState.Alert:
                    navAgent.updateRotation = true;
                    navAgent.speed = data.chaseSpeed;
                    break;
            }
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            sceneTransitionService.StartCombatAsync(data.encounterId).Forget();
            gameObject.SetActive(false);
        }

        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!ev.Victory) return;
            if (encounterContext?.CurrentEncounterId != data.encounterId) return;
            gameObject.SetActive(false);
        }
    }
}
