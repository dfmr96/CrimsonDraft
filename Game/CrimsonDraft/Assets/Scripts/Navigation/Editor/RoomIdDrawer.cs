#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Room id picker: "None" plus every catalog room; "Deck_B/KITCHEN" shows as a Deck_B submenu.</summary>
    [CustomPropertyDrawer(typeof(RoomIdAttribute))]
    public sealed class RoomIdDrawer : PropertyDrawer
    {
        private const string None = "None";

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight + (RoomPickers.IsUnknown(property.stringValue) ? RoomPickers.WarningHeight : 0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[RoomId] needs a string field.");
                return;
            }

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Only write on a real pick: drawing alone must never copy one object's value onto a
            // multi-selection, and BeginProperty keeps the prefab override bold + Revert menu.
            label = EditorGUI.BeginProperty(line, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var picked = RoomPickers.RoomPopup(line, label, property.stringValue);
            if (EditorGUI.EndChangeCheck()) property.stringValue = picked;
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();

            if (!property.hasMultipleDifferentValues && RoomPickers.IsUnknown(property.stringValue))
                EditorGUI.HelpBox(new Rect(position.x, line.yMax + 2f, position.width, RoomPickers.WarningHeight - 4f),
                    $"'{property.stringValue}' is not in the RoomCatalog.", MessageType.Warning);
        }
    }

    /// <summary>A room's own id: deck popup + name box, composed as Deck/ROOM, with warnings for an id
    /// another loaded room already uses or one the RoomCatalog doesn't list yet.</summary>
    [CustomPropertyDrawer(typeof(RoomIdDefinitionAttribute))]
    public sealed class RoomIdDefinitionDrawer : PropertyDrawer
    {
        private static float Line => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => Line * 4f + (Problem(property) != null ? RoomPickers.WarningHeight : 0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "[RoomIdDefinition] needs a string field.");
                return;
            }

            float h       = EditorGUIUtility.singleLineHeight;
            var labelLine = new Rect(position.x, position.y, position.width, h);
            var deckLine  = new Rect(position.x, position.y + Line, position.width, h);
            var nameLine  = new Rect(position.x, position.y + Line * 2f, position.width, h);
            var idLine    = new Rect(position.x, position.y + Line * 3f, position.width, h);

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.LabelField(labelLine, label);

            using (new EditorGUI.IndentLevelScope())
            using (new EditorGUI.DisabledScope(property.hasMultipleDifferentValues))
            {
                var current = property.stringValue;
                var decks   = RoomPickers.Decks(RoomIds.DeckOf(current));
                var deck    = RoomIds.DeckOf(current) ?? decks[0];
                var name    = RoomIds.RoomPartOf(current);

                EditorGUI.BeginChangeCheck();
                int deckIndex = EditorGUI.Popup(deckLine, "Deck", decks.IndexOf(deck), decks.ToArray());
                var newName   = EditorGUI.DelayedTextField(nameLine, "Name", name);
                if (EditorGUI.EndChangeCheck())
                    property.stringValue = RoomIds.Compose(decks[Math.Max(0, deckIndex)], newName);

                EditorGUI.LabelField(idLine, "Id", property.hasMultipleDifferentValues ? "—" : property.stringValue, EditorStyles.boldLabel);
            }
            EditorGUI.EndProperty();

            var problem = Problem(property);
            if (problem != null)
                EditorGUI.HelpBox(new Rect(position.x, idLine.yMax + 2f, position.width, RoomPickers.WarningHeight - 4f), problem, MessageType.Warning);
        }

        private static string? Problem(SerializedProperty property)
        {
            if (property.hasMultipleDifferentValues) return null;
            var id = property.stringValue;
            if (string.IsNullOrEmpty(id)) return "No room id: pick a deck and type a name.";
            if (!RoomIds.IsWellFormed(id)) return $"'{id}' is not in Deck/ROOM form.";

            // Same scene only: in Prefab Mode the deck scene's instance of this very room shares the id.
            var self = property.serializedObject.targetObject as RoomController;
            if (self != null)
                foreach (var other in RoomLookup.LoadedRooms())
                    if (other != self && other.gameObject.scene == self.gameObject.scene && other.RoomId == id)
                        return $"'{id}' is also used by '{other.name}'.";

            return RoomPickers.IsUnknown(id) ? $"'{id}' is not in the RoomCatalog yet: press Rebuild From Scenes on it." : null;
        }
    }

    /// <summary>Unlock target picker: the room, then one of that room's doors.</summary>
    [CustomPropertyDrawer(typeof(DoorRef))]
    public sealed class DoorRefDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var roomId = property.FindPropertyRelative("roomId");
            var doorId = property.FindPropertyRelative("doorId");
            float h    = EditorGUIUtility.singleLineHeight;

            var roomLine = new Rect(position.x, position.y, position.width, h);
            var doorLine = new Rect(position.x, roomLine.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, h);

            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.showMixedValue = roomId.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            var room = RoomPickers.RoomPopup(roomLine, label, roomId.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                roomId.stringValue = room;
                doorId.stringValue = "";
            }
            room = roomId.stringValue;

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(room) || roomId.hasMultipleDifferentValues))
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.showMixedValue = doorId.hasMultipleDifferentValues;
                var doors   = RoomPickers.DoorIdsOf(room);
                var options = new List<string> { "None" };
                options.AddRange(doors);
                if (!string.IsNullOrEmpty(doorId.stringValue) && !doors.Contains(doorId.stringValue))
                    options.Add(doorId.stringValue + " (missing)");

                int index = string.IsNullOrEmpty(doorId.stringValue) ? 0
                    : Math.Max(0, options.FindIndex(o => o == doorId.stringValue || o == doorId.stringValue + " (missing)"));
                int picked = EditorGUI.Popup(doorLine, "Door", index, options.ToArray());
                if (picked != index)
                    doorId.stringValue = picked == 0 ? "" : options[picked].Replace(" (missing)", "");
            }
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }
    }

    internal static class RoomPickers
    {
        public const float WarningHeight = 24f;

        private static RoomCatalog? catalog;
        private static Dictionary<string, List<string>>? doorsByRoom;

        static RoomPickers() => EditorApplication.projectChanged += () =>
        {
            catalog     = null;
            doorsByRoom = null;
        };

        private static RoomCatalog? Catalog => catalog != null ? catalog : catalog = RoomCatalogEditor.Find();

        /// <summary>Decks to pick from: those of the production scenes and the catalog, plus <paramref name="current"/>.</summary>
        public static List<string> Decks(string? current)
        {
            var decks = RoomCatalogEditor.DeckScenes
                .Select(path => RoomIds.DeckOfScene(System.IO.Path.GetFileNameWithoutExtension(path)))
                .Concat(Catalog != null ? Catalog.Rooms.Select(r => RoomIds.DeckOf(r.RoomId) ?? "") : Enumerable.Empty<string>())
                .Where(d => !string.IsNullOrEmpty(d))
                .ToList();
            if (!string.IsNullOrEmpty(current)) decks.Add(current!);
            return decks.Distinct().ToList();
        }

        public static bool IsUnknown(string id)
            => !string.IsNullOrEmpty(id) && Catalog != null && !Catalog.Contains(id);

        public static string RoomPopup(Rect line, GUIContent label, string current)
        {
            var ids     = Catalog != null ? Catalog.Rooms.Select(r => r.RoomId).ToList() : new List<string>();
            var options = new List<string> { "None" };
            options.AddRange(ids);
            if (!string.IsNullOrEmpty(current) && !ids.Contains(current)) options.Add(current);

            int index  = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            int picked = EditorGUI.Popup(line, label, index, options.Select(o => new GUIContent(o)).ToArray());
            return picked <= 0 ? "" : options[picked];
        }

        // Door ids of a room, read from the room prefabs (the room may live in an unloaded scene).
        public static List<string> DoorIdsOf(string roomId)
        {
            if (string.IsNullOrEmpty(roomId)) return new List<string>();
            doorsByRoom ??= BuildDoorIndex();
            return doorsByRoom.TryGetValue(roomId, out var doors) ? doors : new List<string>();
        }

        private static Dictionary<string, List<string>> BuildDoorIndex()
        {
            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/World" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var room   = prefab != null ? prefab.GetComponent<RoomController>() : null;
                if (room == null || string.IsNullOrEmpty(room.RoomId)) continue;
                index[room.RoomId] = room.GetComponentsInChildren<RoomDoorInteractable>(true)
                    .Select(d => d.DoorId).Where(id => !string.IsNullOrEmpty(id)).Distinct().OrderBy(id => id).ToList();
            }
            return index;
        }
    }
}
