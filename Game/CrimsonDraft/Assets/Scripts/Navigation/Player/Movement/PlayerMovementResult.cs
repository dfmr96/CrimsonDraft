#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Player.Movement
{
    // Returned by IPlayerMovementStrategy.Tick() every FixedUpdate. Direction is a unit
    // horizontal vector (or Vector3.zero when there's nothing to move toward this frame);
    // AllowSprint lets a strategy force walk speed regardless of the Sprint button (used by
    // ClassicPlayerMovementStrategy's backpedal, which is never a run in the source material).
    public readonly struct PlayerMovementResult
    {
        public Vector3 Direction   { get; }
        public bool    AllowSprint { get; }

        public PlayerMovementResult(Vector3 direction, bool allowSprint)
        {
            this.Direction   = direction;
            this.AllowSprint = allowSprint;
        }

        public static PlayerMovementResult Idle => new PlayerMovementResult(Vector3.zero, true);
    }
}
