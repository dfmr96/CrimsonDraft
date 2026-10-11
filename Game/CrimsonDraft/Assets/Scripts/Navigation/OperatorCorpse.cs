#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation
{
    [RequireComponent(typeof(Collider))]
    public sealed class OperatorCorpse : MonoBehaviour
    {
        private const string PlayerTag = "Player";

        private CorpseProximityTracker? tracker;
        private int playerCollidersInside;

        public int Slot { get; private set; } = -1;

        public void Initialize(int slot, CorpseProximityTracker proximityTracker)
        {
            this.Slot    = slot;
            this.tracker = proximityTracker;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(PlayerTag)) return;
            if (this.playerCollidersInside++ == 0) this.tracker?.Enter(this.Slot);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(PlayerTag) || this.playerCollidersInside == 0) return;
            if (--this.playerCollidersInside == 0) this.tracker?.Exit(this.Slot);
        }

        // Unity sends no OnTriggerExit when the room holding this corpse is deactivated.
        private void OnDisable()
        {
            if (this.playerCollidersInside == 0) return;
            this.playerCollidersInside = 0;
            this.tracker?.Exit(this.Slot);
        }
    }
}
