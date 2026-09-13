#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure.Save;

namespace CrimsonDraft.Editor
{
    public sealed class SaveGameManagerWindow : EditorWindow
    {
        private ISaveGameService              saveGameService = null!;
        private IReadOnlyList<SaveSlotSummary> slots           = Array.Empty<SaveSlotSummary>();
        private Vector2                        scrollPos;

        [MenuItem("Tools/CrimsonDraft/Save Manager")]
        private static void Open()
        {
            GetWindow<SaveGameManagerWindow>("Save Manager").Refresh();
        }

        private void OnEnable()
        {
            this.saveGameService = new SaveGameService();
            Refresh();
        }

        private void Refresh()
        {
            this.slots = this.saveGameService.ListSlotSummaries();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh", GUILayout.Height(24)))
                Refresh();

            EditorGUILayout.Space(4);
            this.scrollPos = EditorGUILayout.BeginScrollView(this.scrollPos);

            foreach (var slot in this.slots)
                DrawSlotRow(slot);

            EditorGUILayout.EndScrollView();
        }

        private void DrawSlotRow(SaveSlotSummary slot)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField($"Slot {slot.slot:D2}", GUILayout.Width(60));

            if (slot.isEmpty)
            {
                EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
            }
            else
            {
                var playtime = TimeSpan.FromSeconds(slot.playtimeSeconds);

                EditorGUILayout.LabelField($"{slot.roomId}   •   {slot.timestampIso}   •   {playtime:hh\\:mm\\:ss}   •   Saves: {slot.saveCount}");
            }

            GUI.enabled = !slot.isEmpty;
            if (GUILayout.Button("Delete", GUILayout.Width(70)))
                DeleteSlot(slot.slot);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DeleteSlot(int slot)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Save",
                $"Delete save in slot {slot:D2}? This cannot be undone.",
                "Delete",
                "Cancel");

            if (!confirmed) return;

            this.saveGameService.DeleteSlot(slot);
            Refresh();
        }
    }
}
