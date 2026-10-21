#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    /// <summary>
    /// Bundles the nine cross-scene world-state registries that Save/Load and New-Game-reset
    /// all need together, so consumers don't carry eight separate constructor parameters.
    /// </summary>
    public sealed class WorldStateRegistries
    {
        public readonly DoorStateRegistry       Doors;
        public readonly RoomStateRegistry       Rooms;
        public readonly PickupRegistry          Pickups;
        public readonly NoteRegistry            Notes;
        public readonly KnownMapsRegistry       KnownMaps;
        public readonly EnemyStateRegistry      Enemies;
        public readonly OperatorCorpseRegistry  OperatorCorpses;
        public readonly ItemSocketStateRegistry ItemSockets;
        public readonly RadioCallRegistry       RadioCalls;

        [Preserve]
        public WorldStateRegistries(
            DoorStateRegistry       doors,
            RoomStateRegistry       rooms,
            PickupRegistry          pickups,
            NoteRegistry            notes,
            KnownMapsRegistry       knownMaps,
            EnemyStateRegistry      enemies,
            OperatorCorpseRegistry  operatorCorpses,
            ItemSocketStateRegistry itemSockets,
            RadioCallRegistry       radioCalls)
        {
            RadioCalls      = radioCalls;
            Doors           = doors;
            Rooms           = rooms;
            Pickups         = pickups;
            Notes           = notes;
            KnownMaps       = knownMaps;
            Enemies         = enemies;
            OperatorCorpses = operatorCorpses;
            ItemSockets     = itemSockets;
        }
    }
}
