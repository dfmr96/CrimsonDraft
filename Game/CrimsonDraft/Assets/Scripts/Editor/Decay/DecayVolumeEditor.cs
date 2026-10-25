#nullable enable

using System.Collections.Generic;
using CrimsonDraft.Rendering.Decay;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace CrimsonDraft.Editor.Decay
{
    // El DecayVolume vive en el prefab de la sala. Lo que se cambia en la escena (sliders, caja)
    // se lleva solo al prefab al soltar el mouse, así nunca queda como override de la escena.
    [CustomEditor(typeof(DecayVolume))]
    public sealed class DecayVolumeEditor : UnityEditor.Editor
    {
        private static readonly HashSet<DecayVolume> pendingApply = new();

        private readonly BoxBoundsHandle handle = new();

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck())
                ScheduleApply((DecayVolume)this.target);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Abrir Decay Tool"))
                DecayToolWindow.ShowWindow();
        }

        private void OnSceneGUI()
        {
            var volume = (DecayVolume)this.target;
            var t = volume.transform;

            using (new Handles.DrawingScope(new Color(0.9f, 0.5f, 0.2f), Matrix4x4.TRS(t.position, t.rotation, t.lossyScale)))
            {
                this.handle.center = volume.Center;
                this.handle.size   = volume.Size;

                EditorGUI.BeginChangeCheck();
                this.handle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(volume, "Resize DecayVolume");
                    volume.Center = this.handle.center;
                    volume.Size   = this.handle.size;
                    EditorUtility.SetDirty(volume);
                    ScheduleApply(volume);
                }
            }
        }

        // Guardar el prefab en cada frame de un arrastre sería lento: se espera a soltar el mouse.
        internal static void ScheduleApply(DecayVolume volume)
        {
            if (pendingApply.Count == 0) EditorApplication.update += FlushPending;
            pendingApply.Add(volume);
        }

        private static void FlushPending()
        {
            if (GUIUtility.hotControl != 0) return;
            EditorApplication.update -= FlushPending;

            foreach (var volume in pendingApply)
            {
                if (volume == null) continue;
                var room = DecayRoomTool.RoomOf(volume.gameObject);
                if (room == null || DecayRoomTool.Problem(room) != null) continue; // Prefab Mode: se guarda solo
                DecayRoomTool.ApplyOverrides(room, volume);
            }
            pendingApply.Clear();
            DecayShaderGlobals.MarkDirty();
        }
    }
}
