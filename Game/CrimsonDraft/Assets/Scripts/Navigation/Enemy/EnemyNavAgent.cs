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
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Rendering.Blood;

namespace CrimsonDraft.Navigation.Enemy
{
    // Sub-fases del estado Attack, adaptadas de EnemyStateGrab del Survival Horror Template.
    public enum EnemyGrabPhase { None, Reaching, Holding, Biting }

    public sealed class EnemyNavAgent : MonoBehaviour
    {
        // Red de seguridad: si el impacto del mordisco no logra iniciar el combate (ya hay uno en
        // curso, falta el servicio), se suelta al jugador en vez de sostenerlo para siempre.
        private const float BiteGiveUpSeconds = 3f;

        [SerializeField] private NavigationEnemyData  data          = null!;
        [SerializeField] private EncounterData        encounterData = null!;
        [SerializeField] private string               encounterId   = string.Empty;

        public string        EncounterId  => this.encounterId;
        public EncounterData? EncounterData => this.encounterData;
        [SerializeField] private EnemyDetectionSensor sensor        = null!;
        [SerializeField] private EnemyAttackHitbox    hitbox        = null!;
        [Tooltip("Punto (colgado de un hueso del torso) desde donde sale la sangre al recibir un disparo.")]
        [SerializeField] private Transform?           hitFxPoint;
        [SerializeField] private Transform?           eyePoint;
        [Tooltip("Charco de sangre que aparece bajo el cadáver (inactivo mientras el enemigo vive).")]
        [SerializeField] private CorpseBloodBlob?     bloodBlob;

        private ISceneTransitionService?               sceneTransitionService;
        private ISubscriber<CombatEndedEvent>?         combatEndedSubscriber;
        private ISubscriber<DialogueActiveChangedEvent>? dialogueSubscriber;
        private IEncounterContext?                     encounterContext;
        private IPublisher<EnemyAlertChangedEvent>?    enemyAlertPublisher;
        private PlayerController?                      playerController;
        private EnemyStateRegistry?                    enemyStateRegistry;
        private NavigationTimeScale?                   timeScale;
        private string                                 enemyKey = string.Empty;

        private NavMeshAgent     navAgent        = null!;
        private Rigidbody        playerRb        = null!;
        private EnemyAlertState  state           = EnemyAlertState.Idle;
        private IDisposable?     combatEndedSub;
        private IDisposable?     dialogueSub;
        private bool             combatTriggered;
        private bool             dialoguePaused;
        private bool             inspectionSuspended;
        private EnemyGrabPhase   grabPhase;
        private float            grabPhaseTimer;
        private float            grabHoldDuration;
        private float            nextGrabAllowedTime;
        private bool             holdingPlayer;

        public EnemyAlertState State => state;
        public EnemyGrabPhase GrabPhase => grabPhase;
        public bool IsReactingToShot { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsRising { get; private set; }

        // Colliders that EnterCorpseState switched off, so a revive turns back on exactly those.
        private readonly System.Collections.Generic.List<Collider> corpseDisabledColliders = new();
        public Vector3 HitFxPosition => hitFxPoint != null ? hitFxPoint.position : transform.position + Vector3.up;
        public float TimeScale => timeScale?.Scale ?? 1f;

        public void Construct(
            ISceneTransitionService                 sceneTransitionService,
            ISubscriber<CombatEndedEvent>           combatEndedSubscriber,
            ISubscriber<DialogueActiveChangedEvent> dialogueSubscriber,
            IEncounterContext                       encounterContext,
            IPublisher<EnemyAlertChangedEvent>      enemyAlertPublisher,
            PlayerController                        playerController,
            EnemyStateRegistry                      enemyStateRegistry,
            NavigationTimeScale                     timeScale,
            string                                  enemyKey)
        {
            this.sceneTransitionService = sceneTransitionService;
            this.combatEndedSubscriber  = combatEndedSubscriber;
            this.dialogueSubscriber     = dialogueSubscriber;
            this.encounterContext       = encounterContext;
            this.enemyAlertPublisher    = enemyAlertPublisher;
            this.playerController       = playerController;
            this.enemyStateRegistry     = enemyStateRegistry;
            this.timeScale              = timeScale;
            this.enemyKey               = enemyKey;
        }

        private void Start()
        {
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
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

        private void OnDisable()
        {
            ReleasePlayer();
        }

        private void OnDestroy()
        {
            combatEndedSub?.Dispose();
            dialogueSub?.Dispose();
        }

        private void Update()
        {
            if (playerController == null || IsDead || IsRising) return;
            if (dialoguePaused || inspectionSuspended) return;

            // Congela por completo a cualquier enemigo que no haya sido el que disparó el
            // combate: nada de decisiones de IA ni de movimiento mientras dure la pelea
            // (NavigationTimeScale, no Time.timeScale global -- Combat necesita ese en 1).
            // Un combate iniciado (por este mordisco o por cualquier otra vía) también suelta
            // al jugador: el input ya pasó a Combat y no debe volver trabado a navegación.
            if (TimeScale <= 0f)
            {
                navAgent.isStopped = true;
                ReleasePlayer();
                return;
            }

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
                if (Time.time >= nextGrabAllowedTime)
                {
                    TransitionTo(EnemyAlertState.Attack);
                    return;
                }

                // En cooldown tras un agarre fallido: se queda encarando al jugador en vez de
                // empujarlo con el NavMeshAgent.
                navAgent.isStopped = true;
                navAgent.updateRotation = false;
                FacePlayer(toPlayer);
                return;
            }

            var angle = Vector3.Angle(transform.forward, toPlayer.normalized);
            if (angle > data.turnInPlaceThreshold)
            {
                navAgent.isStopped = true;
                navAgent.updateRotation = false;
                var targetRotation = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.turnSpeed * TimeScale * Time.deltaTime);
            }
            else
            {
                navAgent.isStopped = false;
                navAgent.updateRotation = true;
                navAgent.SetDestination(playerController.transform.position);
            }
        }

        // Agarre + mordisco (EnemyStateGrab del template): se acerca con los brazos extendidos,
        // y si el jugador queda dentro de la hitbox lo sostiene un rato y lo muerde. El combate
        // arranca en el impacto del mordisco (NotifyBiteImpact, vía Animation Event).
        private void UpdateAttack()
        {
            var dt = Time.deltaTime * TimeScale;
            grabPhaseTimer += dt;

            var toPlayer = playerController!.transform.position - transform.position;
            toPlayer.y = 0f;
            FacePlayer(toPlayer);

            switch (grabPhase)
            {
                case EnemyGrabPhase.Reaching:
                    navAgent.SetDestination(playerController.transform.position);
                    if (grabPhaseTimer < data.grabDelay) return;

                    if (hitbox.OverlapsPlayer() && playerController.TryBeginGrab(transform))
                    {
                        holdingPlayer      = true;
                        navAgent.isStopped = true;
                        navAgent.ResetPath();
                        grabHoldDuration   = UnityEngine.Random.Range(
                            data.grabHoldDuration - data.grabHoldRandomOffset,
                            data.grabHoldDuration + data.grabHoldRandomOffset);
                        SetGrabPhase(EnemyGrabPhase.Holding);
                        return;
                    }

                    if (grabPhaseTimer >= data.grabDelay + data.grabWindow)
                    {
                        nextGrabAllowedTime = Time.time + data.grabCooldown;
                        TransitionTo(EnemyAlertState.Alerted);
                    }
                    return;

                case EnemyGrabPhase.Holding:
                    KeepGrabDistance(toPlayer, dt);
                    if (grabPhaseTimer >= grabHoldDuration)
                        SetGrabPhase(EnemyGrabPhase.Biting);
                    return;

                case EnemyGrabPhase.Biting:
                    KeepGrabDistance(toPlayer, dt);
                    if (grabPhaseTimer >= BiteGiveUpSeconds)
                    {
                        nextGrabAllowedTime = Time.time + data.grabCooldown;
                        TransitionTo(EnemyAlertState.Alerted);
                    }
                    return;
            }
        }

        // MoveGrabberToGrabbed del template: el enemigo se acomoda a grabDistance del jugador.
        private void KeepGrabDistance(Vector3 toPlayer, float dt)
        {
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            var playerPos = playerController!.transform.position;
            var target    = playerPos - toPlayer.normalized * data.grabDistance;
            target.y      = transform.position.y;
            navAgent.Move(Vector3.MoveTowards(transform.position, target, data.grabMoveSpeed * dt) - transform.position);
        }

        private void FacePlayer(Vector3 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 0.0001f) return;
            var targetRotation = Quaternion.LookRotation(toPlayer.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, data.turnSpeed * TimeScale * Time.deltaTime);
        }

        private void SetGrabPhase(EnemyGrabPhase phase)
        {
            grabPhase      = phase;
            grabPhaseTimer = 0f;
        }

        private void ReleasePlayer()
        {
            if (!holdingPlayer) return;
            holdingPlayer = false;
            playerController?.EndGrab(transform);
        }

        private bool Detect()
            => sensor.Evaluate(data, playerController!.transform, playerRb, eyePoint);

        private void TransitionTo(EnemyAlertState next)
        {
            var prev = state;
            state = next;

            if (next != EnemyAlertState.Attack)
            {
                ReleasePlayer();
                SetGrabPhase(EnemyGrabPhase.None);
            }

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
                    // Sigue avanzando hasta agarrar algo (igual que el template); la rotación
                    // la maneja UpdateAttack para no perder de vista al jugador.
                    navAgent.isStopped = false;
                    navAgent.updateRotation = false;
                    SetGrabPhase(EnemyGrabPhase.Reaching);
                    break;
            }
        }

        public void NotifyCombatTriggered()
        {
            this.combatTriggered = true;
        }

        // El disparo del jugador que inicia el combate: EnemyAnimationReactor reproduce la
        // reacción de daño aunque NavigationTimeScale ya haya congelado al resto de la IA.
        public void NotifyShot()
        {
            ReleasePlayer();
            IsReactingToShot = true;
        }

        /// <summary>Freezes this enemy while the player is in an inspection view (puzzle camera):
        /// no AI, no movement and no combat trigger. Lifted again when the view is left.</summary>
        public void SetInspectionSuspended(bool suspended)
        {
            this.inspectionSuspended = suspended;

            // Con el agarre del template el agente solo queda detenido mientras sostiene al jugador
            // (en Reaching sigue avanzando), así que se usa holdingPlayer en vez de "state is Attack".
            if (navAgent != null && isActiveAndEnabled && navAgent.isOnNavMesh)
                navAgent.isStopped = suspended || dialoguePaused || holdingPlayer;
        }

        public void NotifyBiteImpact()
        {
            if (inspectionSuspended) return;
            if (state != EnemyAlertState.Attack || grabPhase != EnemyGrabPhase.Biting) return;
            playerController?.NotifyBitten();
            TriggerCombat();
        }

        public void ResetToSpawn(Vector3 position, Quaternion rotation)
        {
            if (!isActiveAndEnabled || IsDead || IsRising) return;

            // A room activated for the first time this session cascades Awake()/OnEnable() on its
            // enemies synchronously, but Start() (where navAgent is normally assigned) is deferred
            // to the next frame — so navAgent can still be null here. GetComponent doesn't have that
            // restriction: the component already exists on the GameObject regardless of Start() timing.
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();

            navAgent.Warp(position);
            transform.rotation = rotation;

            combatTriggered     = false;
            dialoguePaused      = false;
            nextGrabAllowedTime = 0f;
            IsReactingToShot    = false;

            TransitionTo(EnemyAlertState.Idle);
        }

        private void TriggerCombat()
        {
            if (sceneTransitionService == null) return;
            if (sceneTransitionService.IsInCombat) return;
            if (inspectionSuspended) return;
            this.combatTriggered = true;
            // Se desactiva recién cuando el fade termina de tapar la pantalla (onScreenCovered),
            // no en el instante del golpe -- si no, el zombie desaparece de golpe en medio del
            // freeze-frame en vez de quedar congelado, visible, durante ese beat.
            sceneTransitionService.StartCombatAsync(this.encounterId, this.encounterData, onScreenCovered: () => gameObject.SetActive(false)).Forget();
        }

        private void OnDialogueActiveChanged(DialogueActiveChangedEvent ev)
        {
            dialoguePaused = ev.IsActive;
            if (IsDead || IsRising || !navAgent.isActiveAndEnabled) return;
            navAgent.isStopped = ev.IsActive || state is EnemyAlertState.Attack;
        }

        // El enemigo que disparó el combate queda tirado donde estaba (no se destruye), así
        // puede reactivarse más adelante. El fade todavía tapa la pantalla cuando llega el
        // evento, así que el jugador lo encuentra ya en el piso.
        private void OnCombatEnded(CombatEndedEvent ev)
        {
            if (!combatTriggered) return;
            if (!ev.Victory) return;
            gameObject.SetActive(true);
            EnterCorpseState(freshDeath: true);
            this.enemyStateRegistry?.SetDefeated(this.enemyKey, transform.position, transform.rotation);
        }

        /// <summary>Lays a defeated enemy down at its saved pose (or wherever it stands, for saves
        /// that predate corpse poses).</summary>
        public void RestoreAsCorpse(Vector3? position, Quaternion? rotation)
        {
            EnterCorpseState(freshDeath: false);
            if (position.HasValue) transform.position = position.Value;
            if (rotation.HasValue) transform.rotation = rotation.Value;
        }

        // Cadáver: sin IA, sin agente (lo saca también de la lista de objetivos del apuntado) y
        // sin colliders, así el jugador lo atraviesa. Queda solo la pose ZombieLyingUp, que
        // aplica EnemyAnimationReactor, y el charco de sangre (que solo crece en una muerte nueva).
        private void EnterCorpseState(bool freshDeath)
        {
            ReleasePlayer();
            SetGrabPhase(EnemyGrabPhase.None);
            IsDead           = true;
            IsReactingToShot = false;
            combatTriggered  = false;
            state            = EnemyAlertState.Idle;

            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
            if (navAgent != null) navAgent.enabled = false;
            if (sensor != null) sensor.enabled = false;
            if (hitbox != null) hitbox.enabled = false;
            corpseDisabledColliders.Clear();
            foreach (var col in GetComponentsInChildren<Collider>(includeInactive: true))
            {
                if (!col.enabled) continue;
                col.enabled = false;
                corpseDisabledColliders.Add(col);
            }

            if (bloodBlob != null) bloodBlob.Show(grow: freshDeath);
        }

        /// <summary>Gets a corpse back up (ZombieStandUp, played by EnemyAnimationReactor). It
        /// stops counting as defeated right away; the AI only comes back once the clip ends
        /// (<see cref="CompleteRise"/>). The blood pool stays on the floor where it fell.
        /// Returns false if there's nothing to revive right now.</summary>
        public bool Revive()
        {
            if (!IsDead || IsRising || !gameObject.activeInHierarchy) return false;
            if (sceneTransitionService?.IsInCombat == true) return false;

            IsDead   = false;
            IsRising = true;
            this.enemyStateRegistry?.SetAlive(this.enemyKey);

            if (bloodBlob != null && bloodBlob.gameObject.activeSelf)
                bloodBlob.LeaveBehind(transform.parent);
            return true;
        }

        /// <summary>Called by EnemyAnimationReactor once ZombieStandUp has finished: hands the
        /// enemy back to the AI in Idle, so it has to detect the player again.</summary>
        public void CompleteRise()
        {
            if (!IsRising) return;
            IsRising = false;

            foreach (var col in corpseDisabledColliders)
                if (col != null) col.enabled = true;
            corpseDisabledColliders.Clear();
            if (sensor != null) sensor.enabled = true;
            if (hitbox != null) hitbox.enabled = true;

            navAgent.enabled = true;
            navAgent.Warp(transform.position);
            TransitionTo(EnemyAlertState.Idle);
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