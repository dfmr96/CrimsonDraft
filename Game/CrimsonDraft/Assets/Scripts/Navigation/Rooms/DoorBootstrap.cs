#nullable enable

using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class DoorBootstrap : IInitializable
    {
        private readonly IRoomOrchestrator       roomOrchestrator;
        private readonly IFloorTransitionService floorService;
        private readonly DoorStateRegistry       registry;
        private readonly DoorCache               cache;

        [Preserve]
        public DoorBootstrap(
            IRoomOrchestrator       roomOrchestrator,
            IFloorTransitionService floorService,
            DoorStateRegistry       registry,
            DoorCache               cache)
        {
            this.roomOrchestrator = roomOrchestrator;
            this.floorService     = floorService;
            this.registry         = registry;
            this.cache            = cache;
        }

        void IInitializable.Initialize()
        {
            // A stale cache entry (door deleted/script missing since "Cache Scene Doors" was last
            // pressed) must not abort Initialize -- it would skip every initializer after this one.
            foreach (var door in this.cache.RoomDoors)
            {
                if (door == null) continue;
                door.Construct(this.roomOrchestrator, this.registry);
            }

            foreach (var door in this.cache.SceneDoors)
            {
                if (door == null) continue;
                door.Construct(this.floorService, this.registry);
            }
        }
    }
}
