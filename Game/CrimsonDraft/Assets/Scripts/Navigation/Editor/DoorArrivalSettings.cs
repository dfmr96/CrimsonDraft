#nullable enable

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Editor-side arrival distance and the "put every arrival of a room back in front of its
    /// door" action used by the door inspector and the Camera Zone Viewer.</summary>
    public static class DoorArrivalSettings
    {
        // Key kept from the removed spawn point aligner, so a tuned value carries over.
        private const string DistancePref = "CrimsonDraft.SpawnDoorDistance";

        public static float Distance
        {
            get => EditorPrefs.GetFloat(DistancePref, DoorArrivalPlacement.DefaultDistance);
            set => EditorPrefs.SetFloat(DistancePref, Mathf.Max(0.1f, value));
        }

        /// <summary>Re-places one arrival with undo; the interior side comes from its room.</summary>
        public static void Reset(DoorArrival arrival)
        {
            var room = arrival.GetComponentInParent<RoomController>(true);
            var hint = room != null ? DoorArrivalPlacement.InteriorHint(room)
                : arrival.transform.position + arrival.transform.forward;

            if (arrival.ArrivalPoint != null) Undo.RecordObject(arrival.ArrivalPoint, "Reset Door Arrival");
            else Undo.RecordObject(arrival, "Reset Door Arrival");
            arrival.PlaceAtDefault(Distance, hint);
            if (arrival.ArrivalPoint != null) EditorUtility.SetDirty(arrival.ArrivalPoint);
            EditorUtility.SetDirty(arrival);
            if (arrival.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(arrival.gameObject.scene);
        }

        /// <summary>Resets every door arrival in the room; returns how many.</summary>
        public static int ResetRoom(RoomController room)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Reset Door Arrivals");
            var arrivals = room.GetComponentsInChildren<DoorArrival>(true);
            foreach (var arrival in arrivals) Reset(arrival);
            return arrivals.Length;
        }
    }
}
