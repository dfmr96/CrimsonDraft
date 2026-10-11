#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseSpawner : IOperatorCorpseSpawner
    {
        private readonly OperatorCorpseSettings settings;
        private readonly CorpseProximityTracker tracker;

        [Preserve]
        public OperatorCorpseSpawner(OperatorCorpseSettings settings, CorpseProximityTracker tracker)
        {
            this.settings = settings;
            this.tracker  = tracker;
        }

        public void Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
            var corpse = instance.GetComponentInChildren<OperatorCorpse>(true);
            if (corpse != null) corpse.Initialize(slot, this.tracker);
        }
    }
}
