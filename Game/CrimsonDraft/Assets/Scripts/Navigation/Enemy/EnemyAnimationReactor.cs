#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Traduce el movimiento del NavMeshAgent y la posición del jugador en los parámetros
    /// del Animator Controller de navegación del enemigo (Enemy_Nav_Controller):
    /// Idle / Walk01 / Walk02 (variedad al caminar), ZombieTwitch01 / 02 (interrupciones
    /// aleatorias del Idle), WalkEnemyClose (reacción cuando el jugador pasa cerca) y
    /// Attack (cuando el jugador está muy cerca).
    /// </summary>
    public sealed class EnemyAnimationReactor : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Transform del jugador. Si se deja vacío, se busca automáticamente por el tag \"Player\".")]
        [SerializeField] private Transform? player;

        [Header("Proximidad — WalkEnemyClose")]
        [Tooltip("Distancia a la que el jugador es considerado \"cerca\" (dispara WalkEnemyClose).")]
        [SerializeField] private float closeRadius       = 4.5f;
        [Tooltip("Margen extra sobre closeRadius antes de desactivar IsClose, para evitar flickering en el borde.")]
        [SerializeField] private float closeRadiusBuffer = 0.5f;

        [Header("Ataque")]
        [Tooltip("Distancia a la que el jugador está lo bastante cerca como para que el enemigo ataque.")]
        [SerializeField] private float attackRange       = 1.3f;
        [Tooltip("Margen extra sobre attackRange antes de salir del rango de ataque.")]
        [SerializeField] private float attackRangeBuffer = 0.3f;
        [Tooltip("Tiempo mínimo entre ataques mientras el jugador se mantiene en rango de ataque.")]
        [SerializeField] private float attackCooldown    = 1.6f;

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
        private bool  isAttackRangeActive;
        private bool  stoppedForAttack;
        private bool  wasMoving;
        private float twitchTimer;
        private float attackTimer;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            navAgent = GetComponent<NavMeshAgent>();

            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
        }

        private void Start()
        {
            twitchTimer = RandomTwitchInterval();
        }

        private void Update()
        {
            if (player == null) return;

            UpdateProximity();
            UpdateAttackMovementLock();
            UpdateLocomotion();
            UpdateTwitch();
            UpdateAttack();
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

            // Hysteresis igual que EnemyDetectionSensor: activa por debajo del radio,
            // desactiva recién al superar radio + buffer, para no parpadear en el borde.
            if (!isCloseActive && distance < closeRadius)
                isCloseActive = true;
            else if (isCloseActive && distance > closeRadius + closeRadiusBuffer)
                isCloseActive = false;

            if (!isAttackRangeActive && distance < attackRange)
                isAttackRangeActive = true;
            else if (isAttackRangeActive && distance > attackRange + attackRangeBuffer)
                isAttackRangeActive = false;

            // Dentro del rango de ataque el enemigo ataca en vez de reaccionar al paso
            // del jugador: se reporta IsClose en falso para que WalkEnemyClose no compita
            // con Attack por la transición.
            var reportedClose = isCloseActive && !isAttackRangeActive;

            animator.SetBool(IsCloseHash, reportedClose);
        }

        // Frena al NavMeshAgent apenas el jugador entra en rango de ataque (mientras se
        // "prepara") y lo mantiene frenado mientras se reproduce Attack, aunque el jugador
        // ya se haya alejado del rango durante la animación. Recién lo suelta cuando el
        // Animator terminó de salir del estado Attack, para que primero termine de atacar
        // y después retome el camino que ya tenía en curso. Solo toca isStopped en los
        // flancos de entrada/salida (no todos los frames) y solo lo libera si fue este
        // script el que lo frenó, para no pisar el isStopped que maneja EnemyNavAgent en
        // sus propios estados (Suspicious, pausa por diálogo, etc.).
        private void UpdateAttackMovementLock()
        {
            var shouldHalt = isAttackRangeActive || IsPlayingAttack();

            if (shouldHalt && !stoppedForAttack)
            {
                navAgent.isStopped = true;
                stoppedForAttack = true;
            }
            else if (!shouldHalt && stoppedForAttack)
            {
                navAgent.isStopped = false;
                stoppedForAttack = false;
            }
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

        private void UpdateAttack()
        {
            if (!isAttackRangeActive)
            {
                // Que el primer ataque, al entrar en rango, salga sin demora.
                attackTimer = 0f;
                return;
            }

            attackTimer -= Time.deltaTime;
            if (attackTimer > 0f) return;

            animator.SetTrigger(AttackHash);
            attackTimer = attackCooldown;
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

        // The Attack clip (ZombieRigged Zombie_Attack_37) is shared with Enemy_Combat_Controller v2,
        // where its OnAttackImpact Animation Event drives the combat hit (EnemyAttackEventRelay).
        // Navigation has no hit to resolve at that frame -- this no-op receiver just keeps Unity
        // from logging "AnimationEvent 'OnAttackImpact' has no receiver" every nav attack.
        public void OnAttackImpact() { }

        private float RandomTwitchInterval()
            => Random.Range(twitchIntervalRange.x, twitchIntervalRange.y);

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, closeRadius);

            Gizmos.color = new Color(1f, 0f, 1f, 0.55f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
#endif
    }
}
