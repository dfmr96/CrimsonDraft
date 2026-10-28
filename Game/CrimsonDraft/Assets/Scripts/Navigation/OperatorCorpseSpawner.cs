#nullable enable

using UnityEngine;
using UnityEngine.Scripting;
using CrimsonDraft.Navigation.Rooms;
using CrimsonDraft.Operators;
using CrimsonDraft.Operators.Appearance;

namespace CrimsonDraft.Navigation
{
    public sealed class OperatorCorpseSpawner : IOperatorCorpseSpawner
    {
        private readonly OperatorCorpseSettings settings;
        private readonly CorpseProximityTracker tracker;
        private readonly IOperatorRoster?       roster;

        [Preserve]
        public OperatorCorpseSpawner(OperatorCorpseSettings settings, CorpseProximityTracker tracker, IOperatorRoster? roster = null)
        {
            this.settings = settings;
            this.tracker  = tracker;
            this.roster   = roster;
        }

        public void Spawn(int slot, RoomController room, Vector3 position, Quaternion rotation)
        {
            var instance = Object.Instantiate(this.settings.CorpsePrefab, position, rotation, room.transform);
            var corpse = instance.GetComponentInChildren<OperatorCorpse>(true);
            if (corpse != null) corpse.Initialize(slot, this.tracker);

            // The body wears the look of the operator who died in that slot.
            var profile = this.roster != null && slot < this.roster.Count ? this.roster[slot].Data?.AppearanceProfile : null;
            var appearance = profile != null ? instance.GetComponentInChildren<SoldierAppearance>(true) : null;
            if (appearance != null) appearance.Apply(profile!);
        }
    }
}
