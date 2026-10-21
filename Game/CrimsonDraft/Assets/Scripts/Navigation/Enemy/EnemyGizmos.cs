#nullable enable

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    /// <summary>
    /// Editor-only helpers so every enemy range reads the same way in the Scene view: a flat
    /// disc on the floor plus a label sitting on its edge saying what the range is for. Each
    /// caller picks a different label direction so the texts don't stack on top of each other.
    /// </summary>
    internal static class EnemyGizmos
    {
        private static readonly Dictionary<Color, GUIStyle> Styles = new();

        public static void LabeledDisc(Vector3 center, float radius, Color color, Vector3 labelDirection, string label)
        {
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            Label(center + Flat(labelDirection) * radius, color, label);
        }

        public static void LabeledArc(Vector3 center, Vector3 forward, float radius, float angle, Color color, string label)
        {
            var flatForward = Flat(forward);
            var from        = Quaternion.Euler(0f, -angle * 0.5f, 0f) * flatForward;
            var to          = Quaternion.Euler(0f,  angle * 0.5f, 0f) * flatForward;

            Handles.color = color;
            Handles.DrawWireArc(center, Vector3.up, from, angle, radius);
            Handles.DrawLine(center, center + from * radius);
            Handles.DrawLine(center, center + to   * radius);
            Label(center + flatForward * radius, color, label);
        }

        private static void Label(Vector3 position, Color color, string text)
        {
            var opaque = new Color(color.r, color.g, color.b, 1f);
            if (!Styles.TryGetValue(opaque, out var style))
            {
                style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = opaque } };
                Styles[opaque] = style;
            }
            Handles.Label(position, text, style);
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }
}

#endif
