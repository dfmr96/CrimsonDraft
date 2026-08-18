#nullable enable

using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Save
{
    public sealed class GameStateResetter : IGameStateResetter
    {
        private readonly WorldStateRegistries  world;
        private readonly InventoryStateRegistry inventoryState;
        private readonly RosterHealthRegistry   rosterHealth;

        [Preserve]
        public GameStateResetter(
            WorldStateRegistries   world,
            InventoryStateRegistry inventoryState,
            RosterHealthRegistry   rosterHealth)
        {
            this.world          = world;
            this.inventoryState = inventoryState;
            this.rosterHealth   = rosterHealth;
        }

        public void ResetAll()
        {
            this.world.Doors.ClearAll();
            this.world.Rooms.ClearAll();
            this.world.Pickups.ClearAll();
            this.world.Notes.ClearAll();
            this.world.KnownMaps.ClearAll();
            this.world.Enemies.ClearAll();
            this.inventoryState.ClearAll();
            this.rosterHealth.ClearAll();
        }
    }
}
