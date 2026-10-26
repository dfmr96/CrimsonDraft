#nullable enable

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CrimsonDraft.Navigation.Gates;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;
using CrimsonDraft.Navigation.Milestones;

namespace CrimsonDraft.Navigation.Editor.Milestones
{
    // Editor-only scan of the open scenes, used by the trigger picker and the validation badge.
    public static class MilestoneSceneIds
    {
        public static IReadOnlyList<string> For(MilestoneTriggerKind kind)
        {
            IEnumerable<string> ids = kind switch
            {
                MilestoneTriggerKind.PickupCollected =>
                    Find<PickupInteractable>().Select(p => p.PickupId).Concat(Find<MapPickupInteractable>().Select(p => p.PickupId)),
                MilestoneTriggerKind.DoorUnlocked    =>
                    Find<RoomDoorInteractable>().Select(d => d.DoorId).Concat(Find<SceneDoorInteractable>().Select(d => d.DoorId)),
                // Every gate (socket, puzzle, radio zone...), already kind-prefixed by GateIds.
                MilestoneTriggerKind.GateCompleted   =>
                    Find<MonoBehaviour>().OfType<IGate>().Select(g => g.GateId),
                _ => Enumerable.Empty<string>(),
            };
            return ids.Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id).ToList();
        }

        private static T[] Find<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
