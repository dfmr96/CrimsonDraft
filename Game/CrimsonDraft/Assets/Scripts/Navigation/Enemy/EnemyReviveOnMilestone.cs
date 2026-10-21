#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Milestones;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Levanta al cadáver de este enemigo una vez completado un milestone, cuando el jugador se
    /// acerca a menos de <see cref="riseRadius"/>. Si el milestone se completa con el jugador en
    /// otra sala, el cuerpo lo espera tirado (Update no corre en salas inactivas). Es de un solo
    /// uso: si el enemigo ya se levantó alguna vez, o estaba vivo cuando se completó el
    /// milestone, no vuelve a disparar (queda persistido como "revivido" en EnemyStateRegistry).
    /// EnemyBootstrap le inyecta el servicio de milestones y el jugador.
    /// </summary>
    [RequireComponent(typeof(EnemyNavAgent))]
    public sealed class EnemyReviveOnMilestone : MonoBehaviour
    {
        public enum Step { Wait, Revive, Spend }

        [Tooltip("Milestone que, al completarse, deja a este cadáver listo para levantarse.")]
        [SerializeField] private MilestoneRef milestone;
        [Tooltip("Distancia horizontal (m) del jugador al cuerpo a la que se levanta.")]
        [SerializeField, Min(0f)] private float riseRadius = 3f;

        private EnemyNavAgent      agent = null!;
        private IMilestoneService? service;
        private PlayerController?  player;
        private bool               pending;

        public void Construct(IMilestoneService milestoneService, PlayerController playerController)
        {
            this.service = milestoneService;
            this.player  = playerController;
            this.service.Completed += OnMilestoneCompleted;
            // Covers loading a save where the milestone is already done but the body is still down.
            this.pending = this.service.IsCompleted(this.milestone);
        }

        /// <summary>What to do this frame while the revive is pending.</summary>
        public static Step Decide(bool isDead, bool isRising, bool hasBeenRevived, float playerDistance, float radius)
        {
            if (hasBeenRevived || isRising) return Step.Wait;
            if (!isDead) return Step.Spend; // alive when it fired: the chance is used up
            return playerDistance <= radius ? Step.Revive : Step.Wait;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void Awake() => this.agent = GetComponent<EnemyNavAgent>();

        private void OnDestroy()
        {
            if (this.service != null) this.service.Completed -= OnMilestoneCompleted;
        }

        private void OnMilestoneCompleted(MilestoneRef completed)
        {
            if (!this.milestone.IsValid || completed.nodeId != this.milestone.nodeId) return;
            this.pending = true;
        }

        private void Update()
        {
            if (!this.pending || this.player == null) return;

            var distance = HorizontalDistance(transform.position, this.player.transform.position);
            switch (Decide(this.agent.IsDead, this.agent.IsRising, this.agent.HasBeenRevived, distance, this.riseRadius))
            {
                case Step.Spend:
                    this.agent.SpendRevive();
                    this.pending = false;
                    break;

                case Step.Revive:
                    // Revive refuses during a combat; stays pending and retries once it's over.
                    if (this.agent.Revive()) this.pending = false;
                    break;

                case Step.Wait:
                    if (this.agent.HasBeenRevived && !this.agent.IsRising) this.pending = false;
                    break;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var diagonal = (transform.forward + transform.right).normalized; // free spot among EnemyNavAgent's labels
            EnemyGizmos.LabeledDisc(transform.position, this.riseRadius, new Color(1f, 0.5f, 0.1f, 0.9f), diagonal,
                $"Rises on approach {this.riseRadius:0.#}m");
        }
#endif
    }
}
