#nullable enable

using UnityEngine;
using UnityEngine.AI;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Traduce el estado de EnemyNavAgent en los parámetros del Animator de navegación
    /// (ZombieMale_Nav): Speed (blend Idle/Walk), Grabbing (agarre en curso), Bite (mordisco) y
    /// Hit (reacción al disparo que inicia el combate). Un enemigo muerto pasa al estado LyingUp.
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

        private Animator     animator = null!;
        private NavMeshAgent navAgent = null!;
        private EnemyGrabPhase previousGrabPhase;
        private bool hitTriggered;
        private bool corpsePoseApplied;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();
            if (owner == null)
                owner = GetComponentInParent<EnemyNavAgent>();
        }

        private void OnEnable()
        {
            previousGrabPhase = EnemyGrabPhase.None;
            hitTriggered      = false;
            corpsePoseApplied = false; // reactivar el GameObject (cambio de sala) reinicia el Animator
        }

        private void Update()
        {
            // Cadáver: salta directo a la pose en el piso, sin transición desde Locomotion.
            if (owner.IsDead)
            {
                if (corpsePoseApplied) return;
                animator.speed = 1f;
                animator.Play(LyingUpHash, 0, 0f);
                corpsePoseApplied = true;
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
    }
}
