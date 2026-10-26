#nullable enable

using System.Collections.Generic;
using System.Linq;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Milestones;
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
        private readonly IGateStates              gateStates;
        private readonly IMilestoneService        milestones;

        private readonly List<FixedCameraZoneTrigger> entered = new();
        // The priority zone that currently holds the camera, until the player leaves it.
        private FixedCameraZoneTrigger? holder;

        [Preserve]
        public FixedCameraZoneBootstrap(
            FixedCameraZoneTrigger[] triggers, IFixedCameraZoneService zoneService, PlayerController player,
            IGateStates gateStates, IMilestoneService milestones)
        {
            this.triggers    = triggers;
            this.zoneService = zoneService;
            this.player      = player;
            this.gateStates  = gateStates;
            this.milestones  = milestones;
        }

        // Zones are polygons tested here, not physics triggers: a hidden room's zones are skipped
        // so only the room the player is in can cut the camera.
        //
        // Ordinary zones cut when the player enters them. A priority zone (completion gate still open)
        // overrides that: while the player stands in one it holds the camera, even inside ordinary
        // zones it overlaps; on leaving it, the camera goes back to the ordinary zone the player is in
        // (that zone was never "entered" again, so nothing else would cut back).
        void ITickable.Tick()
        {
            // Inspection shots temporarily own the camera; navigation zones must
            // not replace them while the player remains inside a two-sided zone.
            if (this.zoneService.CurrentZoneCamera != this.zoneService.GameplayZoneCamera) return;
            var position = this.player.transform.position;

            this.entered.Clear();
            FixedCameraZoneTrigger? priority = null;
            foreach (var trigger in this.triggers)
            {
                if (!IsLive(trigger)) continue;
                if (trigger.Track(position)) this.entered.Add(trigger);
                if (priority == null && trigger.IsPlayerInside && trigger.IsPriority) priority = trigger;
            }

            if (priority != null)
            {
                this.holder = priority;
                priority.Activate();
                return;
            }

            if (this.holder != null)
            {
                // Completed while the player is still inside: its camera stays until they step out.
                if (IsLive(this.holder) && this.holder.IsPlayerInside) return;
                this.holder = null;
                HandBack();
                return;
            }

            foreach (var trigger in this.entered)
                if (!trigger.IsCompleted) trigger.Activate();
        }

        private void HandBack()
        {
            foreach (var trigger in this.triggers)
            {
                if (!IsLive(trigger) || !trigger.IsPlayerInside || trigger.IsCompleted) continue;
                trigger.Activate();
                return;
            }
        }

        private static bool IsLive(FixedCameraZoneTrigger? trigger) => trigger != null && trigger.isActiveAndEnabled;

        void IInitializable.Initialize()
        {
            foreach (var trigger in this.triggers)
                trigger.Construct(this.zoneService, this.gateStates, this.milestones);

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
