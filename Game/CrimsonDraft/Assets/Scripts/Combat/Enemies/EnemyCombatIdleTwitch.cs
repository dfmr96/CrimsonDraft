#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Attached to an enemy's battlefield prefab (EnemyCombatModel). Purely cosmetic idle
    /// flavor for Enemy_Combat_Controller v2: while the Animator is sitting in its Idle
    /// state, randomly fires Twitch1/Twitch2 to interrupt it, exactly like
    /// EnemyAnimationReactor does for the Navigation-phase Enemy_Nav_Controller. Doesn't
    /// touch combat state (poise, stagger, HP) at all -- BattlefieldView drives everything
    /// else on this Animator (Flinch/Stagger/IsStaggered/StaggerRecover/Attack/Death).
    /// </summary>
    public sealed class EnemyCombatIdleTwitch : MonoBehaviour
    {
        [Tooltip("Rango de segundos entre interrupciones aleatorias del Idle con Twitch1/Twitch2.")]
        [SerializeField] private Vector2 twitchIntervalRange = new(4f, 9f);
        [SerializeField] private Animator? animator;

        private static readonly int Twitch1Hash = Animator.StringToHash("Twitch1");
        private static readonly int Twitch2Hash = Animator.StringToHash("Twitch2");

        private float twitchTimer;

        private void Awake()
        {
            if (this.animator == null)
                this.animator = GetComponent<Animator>();
        }

        private void Start()
        {
            this.twitchTimer = RandomTwitchInterval();
        }

        private void Update()
        {
            if (this.animator == null) return;

            // Solo dispara el twitch mientras el Animator está efectivamente en Idle: evita
            // que un trigger quede "en cola" y salte de golpe apenas termine Attack/Flinch/
            // Stagger/Death y vuelva a Idle.
            if (!this.animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
            {
                this.twitchTimer = RandomTwitchInterval();
                return;
            }

            this.twitchTimer -= Time.deltaTime;
            if (this.twitchTimer > 0f) return;

            this.animator.SetTrigger(Random.value < 0.5f ? Twitch1Hash : Twitch2Hash);
            this.twitchTimer = RandomTwitchInterval();
        }

        private float RandomTwitchInterval()
            => Random.Range(this.twitchIntervalRange.x, this.twitchIntervalRange.y);
    }
}
