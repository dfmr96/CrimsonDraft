#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Inspector for a door's arrival: a reset button, and a scene arrow showing where the player
    /// appears and which way they face, with a dotted line back to the door.</summary>
    [CustomEditor(typeof(DoorArrival))]
    public sealed class DoorArrivalEditor : UnityEditor.Editor
    {
        private static readonly Color ArrowColor = new Color(1f, 0.75f, 0.2f);

        public override void OnInspectorGUI()
        {
            this.DrawDefaultInspector();
            if (GUILayout.Button($"Reset Arrival Point ({DoorArrivalSettings.Distance:0.##} m)"))
            {
                foreach (var t in this.targets) DoorArrivalSettings.Reset((DoorArrival)t);
                SceneView.RepaintAll();
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        private static void DrawArrival(DoorArrival arrival, GizmoType type)
        {
            var point = arrival.ArrivalPoint;
            if (point == null) return;

            bool selected = (type & GizmoType.Selected) != 0;
            Handles.color = selected ? ArrowColor : new Color(ArrowColor.r, ArrowColor.g, ArrowColor.b, 0.6f);
            var feet = point.position - Vector3.up * DoorArrivalPlacement.ArrivalHeight;
            Handles.DrawWireDisc(feet, Vector3.up, 0.25f);
            Handles.ArrowHandleCap(0, feet, Quaternion.LookRotation(point.forward, Vector3.up), 0.6f, EventType.Repaint);
            Handles.DrawDottedLine(feet, DoorArrivalPlacement.DoorBounds(arrival).center - Vector3.up * (DoorArrivalPlacement.DoorBounds(arrival).extents.y), 3f);
        }
    }
}
