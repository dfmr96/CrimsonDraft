#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    public sealed class GameStateResetter : IGameStateResetter
    {
        private readonly WorldStateRegistries  world;
        private readonly InventoryStateRegistry inventoryState;
        private readonly RosterHealthRegistry   rosterHealth;
        private readonly PlaytimeTracker        playtimeTracker;

        [Preserve]
        public GameStateResetter(
            WorldStateRegistries   world,
            InventoryStateRegistry inventoryState,
            RosterHealthRegistry   rosterHealth,
            PlaytimeTracker        playtimeTracker)
        {
            this.world           = world;
            this.inventoryState  = inventoryState;
            this.rosterHealth    = rosterHealth;
            this.playtimeTracker = playtimeTracker;
        }

        public void ResetAll()
        {
            this.world.Doors.ClearAll();
            this.world.Rooms.ClearAll();
            this.world.Pickups.ClearAll();
            this.world.Notes.ClearAll();
            this.world.KnownMaps.ClearAll();
            this.world.Enemies.ClearAll();
            this.world.OperatorCorpses.ClearAll();
            this.inventoryState.ClearAll();
            this.rosterHealth.ClearAll();
            this.playtimeTracker.Reset();
        }
    }
}
