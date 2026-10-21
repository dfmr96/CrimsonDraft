#nullable enable

using UnityEngine;
using UnityEngine.AI;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Traduce el estado de EnemyNavAgent en los parámetros del Animator de navegación
    /// (ZombieMale_Nav): Speed (blend Idle/Walk), Grabbing (agarre en curso), Bite (mordisco) y
    /// Hit (reacción al disparo que inicia el combate). El crawler suma Alerted (si no, queda Dormant). Un enemigo muerto pasa al estado LyingUp, y uno revivido a StandUp.
    /// Las transiciones viven en el Animator Controller; acá solo se escriben parámetros.
    /// </summary>
    public sealed class EnemyAnimationReactor : MonoBehaviour
    {
        [Tooltip("EnemyNavAgent dueño de este reactor. Si se deja vacío, se busca en un padre.")]
        [SerializeField] private EnemyNavAgent owner = null!;
        [Tooltip("Suavizado (segundos) del parámetro Speed.")]
        [SerializeField] private float speedDampTime = 0.1f;

        private static readonly int SpeedHash    = Animator.StringToHash("Speed");
        private static readonly int GrabbingHash = Animator.StringToHash("Grabbing");
        private static readonly int BiteHash     = Animator.StringToHash("Bite");
        private static readonly int HitHash      = Animator.StringToHash("Hit");
        private static readonly int LyingUpHash  = Animator.StringToHash("LyingUp");
        private static readonly int StandUpHash  = Animator.StringToHash("StandUp");
        private static readonly int AlertedHash  = Animator.StringToHash("Alerted");

        private Animator     animator = null!;
        private NavMeshAgent navAgent = null!;
        private EnemyGrabPhase previousGrabPhase;
        private bool hitTriggered;
        private bool corpsePoseApplied;
        private bool riseStarted;
        private bool riseObserved;
        // Solo el controller del crawler tiene "Alerted" (para su estado Dormant, tirado hasta
        // detectar al jugador); escribirlo en uno que no lo tiene loguea un warning por frame.
        private bool hasAlertedParam;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();
            if (owner == null)
                owner = GetComponentInParent<EnemyNavAgent>();

            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == AlertedHash) hasAlertedParam = true;
        }

        private void OnEnable()
        {
            previousGrabPhase = EnemyGrabPhase.None;
            hitTriggered      = false;
            // Reactivar el GameObject (cambio de sala) reinicia el Animator: la pose de cadáver o el
            // levantarse se vuelven a aplicar desde cero.
            corpsePoseApplied = false;
            riseStarted       = false;
            riseObserved      = false;
        }

        private void Update()
        {
            // Cadáver: salta directo a la pose en el piso, sin transición desde Locomotion.
            if (owner.IsDead)
            {
                if (corpsePoseApplied) return;
                animator.speed = 1f;
                // Al final del clip: el walker usa una pose de un frame (ZombieLyingUp), pero el
                // crawler usa ZombieCrawlHeadStomp y su cadáver es el último frame de esa muerte.
                animator.Play(LyingUpHash, 0, 1f);
                corpsePoseApplied = true;
                return;
            }

            // Levantándose: ZombieStandUp arranca desde la pose LyingUp y sale solo a Locomotion
            // (exit time en el controller). Recién cuando el Animator dejó StandUp se le devuelve
            // la IA al agente.
            if (owner.IsRising)
            {
                corpsePoseApplied = false; // una próxima muerte vuelve a tirarlo al piso
                animator.speed = owner.TimeScale;
                if (!riseStarted)
                {
                    animator.SetBool(GrabbingHash, false);
                    animator.SetFloat(SpeedHash, 0f);
                    animator.Play(StandUpHash, 0, 0f);
                    riseStarted = true;
                    return;
                }

                if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == StandUpHash)
                {
                    riseObserved = true;
                    return;
                }

                if (riseObserved)
                {
                    riseStarted  = false;
                    riseObserved = false;
                    owner.CompleteRise();
                }
                return;
            }

            // El disparo que inicia el combate congela la navegación al instante; la reacción de
            // daño es la excepción y sigue reproduciéndose durante el fade hacia Combat.
            if (owner.IsReactingToShot)
            {
                animator.speed = 1f;
                animator.SetFloat(SpeedHash, 0f);
                if (!hitTriggered)
                {
                    animator.SetTrigger(HitHash);
                    hitTriggered = true;
                }
                return;
            }
            hitTriggered = false;

            // NavigationTimeScale (no Time.timeScale global) también congela la reproducción
            // del Animator mientras dure el combate.
            animator.speed = owner.TimeScale;
            if (owner.TimeScale <= 0f) return;

            var grabbing = owner.State == EnemyAlertState.Attack;
            animator.SetBool(GrabbingHash, grabbing);
            if (hasAlertedParam)
                animator.SetBool(AlertedHash, owner.State != EnemyAlertState.Idle);

            var velocity = navAgent.velocity;
            velocity.y = 0f;
            animator.SetFloat(SpeedHash, grabbing ? 0f : velocity.magnitude, speedDampTime, Time.deltaTime);

            var phase = owner.GrabPhase;
            if (phase == EnemyGrabPhase.Biting && previousGrabPhase != EnemyGrabPhase.Biting)
                animator.SetTrigger(BiteHash);
            else if (phase != EnemyGrabPhase.Biting)
                animator.ResetTrigger(BiteHash);
            previousGrabPhase = phase;
        }

        // Animation Event del clip ZombieGrabAttack (el mordisco), en el frame en que la boca
        // llega al jugador. El mismo clip lo usa el modelo de combate, donde lo recibe
        // EnemyAttackEventRelay.
        public void OnAttackImpact() => owner.NotifyBiteImpact();

        // Animation Event que trae ZombieStandUp desde el Horror Engine (TriggerEvent("StandUp")).
        // Acá no se usa -- el fin del levantarse se detecta por el estado del Animator -- pero
        // sin receptor Unity loguea un error en cada levantada.
        public void TriggerEvent(string eventId) { }
    }
}
