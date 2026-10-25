#nullable enable

using System.Linq;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using Unity.Cinemachine;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public sealed class FixedCameraZoneBootstrap : IInitializable, ITickable
    {
        private readonly FixedCameraZoneTrigger[] triggers;
        private readonly IFixedCameraZoneService  zoneService;
        private readonly PlayerController         player;
        private readonly PuzzleStateRegistry      puzzleStates;

        [Preserve]
        public FixedCameraZoneBootstrap(
            FixedCameraZoneTrigger[] triggers, IFixedCameraZoneService zoneService, PlayerController player,
            PuzzleStateRegistry puzzleStates)
        {
            this.triggers    = triggers;
            this.zoneService = zoneService;
            this.player      = player;
            this.puzzleStates = puzzleStates;
        }

        // Zones are polygons tested here, not physics triggers: a hidden room's zones are skipped
        // so only the room the player is in can cut the camera.
        void ITickable.Tick()
        {
            // Inspection shots temporarily own the camera; navigation zones must
            // not replace them while the player remains inside a two-sided zone.
            if (this.zoneService.CurrentZoneCamera != this.zoneService.GameplayZoneCamera) return;
            var position = this.player.transform.position;
            foreach (var trigger in this.triggers)
            {
                if (trigger == null || !trigger.isActiveAndEnabled) continue;
                trigger.Evaluate(position);
            }
        }

        void IInitializable.Initialize()
        {
            foreach (var trigger in this.triggers)
                trigger.Construct(this.zoneService, this.puzzleStates);

            // A room can hold several zone cameras, but only one may be enabled at a time
            // or Cinemachine can't tell which one to show, and this service can only ever
            // turn off a camera it knows is "current". Designers editing a room by hand can
            // easily leave more than one ticked on — normalize each room's zone cameras
            // down to exactly one here so that never sticks.
            //
            // Scanning every CinemachineCamera under the room (not just ones a trigger
            // targets) matters: a room's starting/default shot is often never the target of
            // any trigger — nothing "enters" it, it's just what's live before the player
            // crosses anything — so trigger-target membership alone would miss it entirely.
            var rooms = this.triggers
                .Select(t => t.GetComponentInParent<RoomController>())
                .Where(r => r != null)
                .Distinct();

            foreach (var room in rooms)
            {
                var camerasInRoom = room.GetComponentsInChildren<CinemachineCamera>(true);
                if (camerasInRoom.Length == 0) continue;

                var seed = camerasInRoom.FirstOrDefault(c => c.enabled) ?? camerasInRoom[0];
                foreach (var camera in camerasInRoom)
                    camera.enabled = camera == seed;
            }
        }
    }
}
