#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>The cameras of a room that should each have one activation zone: every camera a zone
    /// already targets, plus every camera under the room's <c>Cameras</c> object — except the ones another
    /// component of the room points at (puzzle / save-station inspection shots; arrival cameras of doors and
    /// spawns are zone cameras and do not count). Follow cameras at the
    /// room root are not zone cameras.</summary>
    public static class ZoneCameras
    {
        public static CinemachineCamera[] Of(RoomController room)
        {
            var triggers = room.GetComponentsInChildren<FixedCameraZoneTrigger>(true);
            var targeted = new HashSet<CinemachineCamera>(triggers.SelectMany(t => t.ReferencedCameras));

            var used = new HashSet<CinemachineCamera>();
            foreach (var component in room.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Zones and arrival points point at zone cameras; anything else (puzzles, save stations)
                // points at its own inspection shot.
                if (component == null || component is FixedCameraZoneTrigger || component is CinemachineCamera
                    || component is DoorArrival) continue;
                var property = new SerializedObject(component).GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is CinemachineCamera camera) used.Add(camera);
            }

            var result = new List<CinemachineCamera>(targeted);
            var camerasRoot = room.transform.Find("Cameras");
            if (camerasRoot != null)
                foreach (var camera in camerasRoot.GetComponentsInChildren<CinemachineCamera>(true))
                    if (!targeted.Contains(camera) && !used.Contains(camera)) result.Add(camera);
            return result.Where(c => c != null).Distinct().ToArray();
        }
    }
}
