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
            foreach (var door in this.cache.RoomDoors)
                door.Construct(this.roomOrchestrator, this.registry);

            foreach (var door in this.cache.SceneDoors)
                door.Construct(this.floorService, this.registry);
        }
    }
}
