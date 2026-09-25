#nullable enable

using UnityEngine;
using UnityEngine.AI;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Traduce el estado de EnemyNavAgent y el movimiento del NavMeshAgent en los parámetros
    /// del Animator Controller de navegación del enemigo (Enemy_Nav_Controller):
    /// Idle / Walk01 / Walk02 (variedad al caminar), ZombieTwitch01 / 02 (interrupciones
    /// aleatorias del Idle), WalkEnemyClose (reacción cuando el jugador pasa cerca) y
    /// Attack (cuando EnemyNavAgent entra en el estado Attack).
    /// </summary>
    public sealed class EnemyAnimationReactor : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform del jugador. Si se deja vacío, se busca automáticamente por el tag \"Player\".")]
        [SerializeField] private Transform? player;
        [Tooltip("EnemyNavAgent dueño de este reactor. Si se deja vacío, se busca en un padre.")]
        [SerializeField] private EnemyNavAgent owner = null!;
        [Tooltip("Hitbox de la mano que ataca. Si se deja vacío, se busca en los hijos.")]
        [SerializeField] private EnemyAttackHitbox hitbox = null!;

        [Header("Proximidad — WalkEnemyClose")]
        [Tooltip("Distancia a la que el jugador es considerado \"cerca\" (dispara WalkEnemyClose).")]
        [SerializeField] private float closeRadius       = 4.5f;
        [Tooltip("Margen extra sobre closeRadius antes de desactivar IsClose, para evitar flickering en el borde.")]
        [SerializeField] private float closeRadiusBuffer = 0.5f;

        [Header("Locomoción")]
        [Tooltip("Velocidad mínima del NavMeshAgent para considerar que el enemigo está caminando.")]
        [SerializeField] private float movingSpeedThreshold = 0.05f;

        [Header("Twitch (Idle)")]
        [Tooltip("Rango de segundos entre interrupciones aleatorias del Idle con ZombieTwitch01/02.")]
        [SerializeField] private Vector2 twitchIntervalRange = new(4f, 9f);

        private static readonly int IsMovingHash    = Animator.StringToHash("IsMoving");
        private static readonly int WalkVariantHash = Animator.StringToHash("WalkVariant");
        private static readonly int Twitch1Hash     = Animator.StringToHash("Twitch1");
        private static readonly int Twitch2Hash     = Animator.StringToHash("Twitch2");
        private static readonly int IsCloseHash     = Animator.StringToHash("IsClose");
        private static readonly int AttackHash      = Animator.StringToHash("Attack");

        private Animator     animator = null!;
        private NavMeshAgent navAgent = null!;

        private bool  isCloseActive;
        private bool  wasMoving;
        private bool  wasPlayingAttack;
        private EnemyAlertState previousState;
        private float twitchTimer;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();

            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (owner == null)
                owner = GetComponentInParent<EnemyNavAgent>();

            if (hitbox == null)
                hitbox = GetComponentInChildren<EnemyAttackHitbox>(true);
        }

        // Unity solo entrega Animation Events al GameObject que tiene el Animator (este),
        // nunca a hijos — por eso EnemyAttackHitbox (colgado de Hand.R) no puede recibirlos
        // directamente; este reactor los recibe y reenvía.
        public void OnAttackHitboxOpen()  => hitbox.Open();
        public void OnAttackHitboxClose() => hitbox.Close();

        private void Start()
        {
            twitchTimer = RandomTwitchInterval();
            previousState = owner.State;
        }

        private void Update()
        {
            if (player == null) return;

            UpdateProximity();
            UpdateLocomotion();
            UpdateTwitch();
            UpdateAttackTrigger();
            UpdateAttackFinishedEdge();
        }

        private void UpdateLocomotion()
        {
            var moving = navAgent.velocity.sqrMagnitude > movingSpeedThreshold * movingSpeedThreshold;

            if (moving && !wasMoving)
                animator.SetInteger(WalkVariantHash, Random.Range(0, 2)); // 0 = Walk01, 1 = Walk02

            wasMoving = moving;
            animator.SetBool(IsMovingHash, moving);
        }

        private void UpdateProximity()
        {
            var toPlayer = player!.position - transform.position;
            toPlayer.y = 0f;
            var distance = toPlayer.magnitude;

            // Hysteresis para no parpadear en el borde.
            if (!isCloseActive && distance < closeRadius)
                isCloseActive = true;
            else if (isCloseActive && distance > closeRadius + closeRadiusBuffer)
                isCloseActive = false;

            // Mientras ataca, no compite con Attack por la transición.
            var reportedClose = isCloseActive && owner.State != EnemyAlertState.Attack;

            animator.SetBool(IsCloseHash, reportedClose);
        }

        // Dispara el trigger Attack del Animator en el flanco de entrada al estado Attack
        // de EnemyNavAgent (única fuente de verdad sobre cuándo atacar).
        private void UpdateAttackTrigger()
        {
            if (owner.State == EnemyAlertState.Attack && previousState != EnemyAlertState.Attack)
                animator.SetTrigger(AttackHash);
            previousState = owner.State;
        }

        // "Current" se mantiene en Attack durante todo el crossfade de salida hacia Idle,
        // así que alcanza para cubrir la animación completa; el chequeo de transición cubre
        // además el instante justo en el que arranca el crossfade de entrada.
        private bool IsPlayingAttack()
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack")) return true;
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Attack")) return true;
            return false;
        }

        // Notifica a EnemyNavAgent apenas la animación de ataque termina sin haber conectado
        // (si hubiera conectado, EnemyAttackHitbox ya sacó al enemigo del estado Attack).
        private void UpdateAttackFinishedEdge()
        {
            var playingAttack = IsPlayingAttack();
            if (wasPlayingAttack && !playingAttack && owner.State == EnemyAlertState.Attack)
                owner.NotifyAttackAnimationFinished();
            wasPlayingAttack = playingAttack;
        }

        private void UpdateTwitch()
        {
            // Solo dispara el twitch mientras el Animator está efectivamente en Idle:
            // evita que un trigger quede "en cola" y salte de golpe al volver a Idle
            // luego de caminar o de una reacción de proximidad.
            if (!animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
            {
                twitchTimer = RandomTwitchInterval();
                return;
            }

            twitchTimer -= Time.deltaTime;
            if (twitchTimer > 0f) return;

            animator.SetTrigger(Random.value < 0.5f ? Twitch1Hash : Twitch2Hash);
            twitchTimer = RandomTwitchInterval();
        }

        private float RandomTwitchInterval()
            => Random.Range(twitchIntervalRange.x, twitchIntervalRange.y);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, closeRadius);
        }
#endif
    }
}