#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Every room of the game with the scene it lives in. Rebuilt from the production
    /// deck scenes by its editor button; feeds the room pickers and the debug teleport, which
    /// can't scan unloaded scenes.</summary>
    [CreateAssetMenu(fileName = "RoomCatalog", menuName = "CrimsonDraft/Navigation/Room Catalog")]
    public sealed class RoomCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [SerializeField] private string roomId;
            [SerializeField] private string sceneName;

            public Entry(string roomId, string sceneName)
            {
                this.roomId    = roomId;
                this.sceneName = sceneName;
            }

            public string RoomId    => this.roomId;
            public string SceneName => this.sceneName;
        }

        [SerializeField] private Entry[] rooms = Array.Empty<Entry>();

        /// <summary>Grouped by deck (decks in order of first appearance), ids ordinal within a deck.</summary>
        public IReadOnlyList<Entry> Rooms => this.rooms;

        public bool Contains(string roomId) => TryGetScene(roomId, out _);

        public bool TryGetScene(string roomId, out string sceneName)
        {
            foreach (var entry in this.rooms)
            {
                if (!string.Equals(entry.RoomId, roomId, StringComparison.Ordinal)) continue;
                sceneName = entry.SceneName;
                return true;
            }
            sceneName = "";
            return false;
        }

        public void EditorSetRooms(IEnumerable<Entry> entries)
        {
            var unique = entries
                .GroupBy(e => e.RoomId, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();
            var deckOrder = unique
                .Select(e => RoomIds.DeckOf(e.RoomId) ?? "")
                .Distinct()
                .ToList();

            this.rooms = unique
                .OrderBy(e => deckOrder.IndexOf(RoomIds.DeckOf(e.RoomId) ?? ""))
                .ThenBy(e => e.RoomId, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
