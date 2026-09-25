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
        [SerializeField] private EnemyDetectionSensor sensor        = null!;
        [SerializeField] private Transform?           eyePoint;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private ISubscriber<DialogueActiveChangedEvent>? dialogueSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<EnemyAlertChangedEvent>?    enemyAlertPublisher;
        private PlayerController?                      playerController;
        private EnemyStateRegistry?                    enemyStateRegistry;
        private string                                 enemyKey = string.Empty;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private EnemyAlertState  state           = EnemyAlertState.Idle;
        private IDisposable?     combatEndedSub;
        private IDisposable?     dialogueSub;
        private bool             combatTriggered;
        private bool             dialoguePaused;

        public EnemyAlertState State => state;

        public void Construct(
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      enemyStateRegistry,
            string                                  enemyKey)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.enemyAlertPublisher    = enemyAlertPublisher;
            this.playerController       = playerController;
            this.enemyStateRegistry     = enemyStateRegistry;
            this.enemyKey               = enemyKey;
        }

        private void Start()
        {
            navAgent = GetComponent<NavMeshAgent>();
            playerRb = playerController!.GetComponent<Rigidbody>();
            if (playerRb == null)
            {
                Debug.LogError($"[EnemyNavAgent] PlayerController on '{playerController.name}' has no Rigidbody. Sound detection will NullRef.", this);
                enabled = false;
                return;
            }

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
                case EnemyAlertState.Idle:    UpdateIdle();    break;
                case EnemyAlertState.Alerted: UpdateAlerted(); break;
                case EnemyAlertState.Attack:  UpdateAttack();  break;
            }
        }

        private void UpdateIdle()
        {
            if (Detect())
                TransitionTo(EnemyAlertState.Alerted);
        }

        private void UpdateAlerted()
        {
            var toPlayer = playerController!.transform.position - transform.position;
            toPlayer.y = 0f;

            if (toPlayer.magnitude < data.attackRange)
            {
                TransitionTo(EnemyAlertState.Attack);
                return;
            }

            var angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > data.turnInPlaceThreshold)
            {
                navAgent.isStopped = true;
                navAgent.updateRotation = false;
                var targetRotation = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.turnSpeed * Time.deltaTime);
            }
            else
            {
                navAgent.isStopped = false;
                navAgent.updateRotation = true;
                navAgent.SetDestination(playerController.transform.position);
            }
        }

        private void UpdateAttack()
        {
            // Intentionally empty: the agent stays stopped (set on TransitionTo),
            // Animator plays the Attack clip, and EnemyAttackHitbox/EnemyAnimationReactor
            // drive the exit via NotifyAttackHit()/NotifyAttackAnimationFinished().
        }

        private bool Detect()
            => sensor.Evaluate(data, playerController!.transform, playerRb, eyePoint);

        private void TransitionTo(EnemyAlertState next)
        {
            var prev = state;
            state = next;

            enemyAlertPublisher?.Publish(new EnemyAlertChangedEvent
            {
                EnemyId       = gameObject.name,
                PreviousState = prev,
                NewState      = next,
            });

            switch (next)
            {
                case EnemyAlertState.Idle:
                    navAgent.isStopped = false;
                    navAgent.updateRotation = true;
                    navAgent.ResetPath();
                    break;

                case EnemyAlertState.Alerted:
                    navAgent.isStopped = false;
                    navAgent.updateRotation = true;
                    navAgent.speed = data.chaseSpeed;
                    break;

                case EnemyAlertState.Attack:
                    navAgent.isStopped = true;
                    navAgent.updateRotation = true;
                    break;
            }
        }

        public void NotifyCombatTriggered()
        {
            this.combatTriggered = true;
        }

        public void NotifyAttackHit()
        {
            if (state != EnemyAlertState.Attack) return;
            TriggerCombat();
        }

        public void NotifyAttackAnimationFinished()
        {
            if (state != EnemyAlertState.Attack) return;
            TransitionTo(EnemyAlertState.Alerted);
        }

        public void ResetToSpawn(Vector3 position, Quaternion rotation)
        {
            if (!isActiveAndEnabled) return;

            // A room activated for the first time this session cascades Awake()/OnEnable() on its
            // enemies synchronously, but Start() (where navAgent is normally assigned) is deferred
            // to the next frame — so navAgent can still be null here. GetComponent doesn't have that
            // restriction: the component already exists on the GameObject regardless of Start() timing.
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

            navAgent.Warp(position);
            transform.rotation = rotation;

            combatTriggered = false;
            dialoguePaused  = false;

            TransitionTo(EnemyAlertState.Idle);
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            this.combatTriggered = true;
            sceneTransitionService.StartCombatAsync(this.encounterId, this.encounterData).Forget();
            gameObject.SetActive(false);
        }

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent ev)
        {
            dialoguePaused = ev.IsActive;
            navAgent.isStopped = ev.IsActive || state is EnemyAlertState.Attack;
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
            Gizmos.DrawWireSphere(pos, data.attackRange);

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