#nullable enable

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Game-wide room ids: <c>&lt;Deck&gt;/&lt;ROOM&gt;</c> (e.g. <c>Deck_B/KITCHEN</c>), so the
    /// same room name can exist on several decks and anything can point at a room by text.</summary>
    public static class RoomIds
    {
        private const string DevelopmentSuffix = "_Development";

        private static readonly Regex WellFormed = new Regex(@"^[A-Za-z0-9_]+/[A-Z0-9_]+$");

        // Legacy room parts that are renamed, not just cleaned (spec normalization table).
        private static readonly Dictionary<string, string> Renames = new Dictionary<string, string>
        {
            ["SAVEROOM_C"] = "SAVE_ROOM",
        };

        /// <summary>The new id of a room carrying <paramref name="legacyId"/> on <paramref name="deck"/>;
        /// an id that already has a deck keeps it.</summary>
        public static string Normalize(string deck, string legacyId)
        {
            string id   = legacyId.Trim();
            int    cut  = id.IndexOf('/');
            if (cut >= 0)
            {
                deck = id.Substring(0, cut);
                id   = id.Substring(cut + 1);
            }

            string room = id.ToUpperInvariant().Replace(' ', '_');
            if (Renames.TryGetValue(room, out var renamed)) room = renamed;
            return deck + "/" + room;
        }

        /// <summary>New ids that more than one room would get, so a migration can refuse to merge rooms.</summary>
        public static IReadOnlyList<string> FindCollisions(IEnumerable<(string deck, string legacyId)> rooms)
        {
            var seen       = new HashSet<string>();
            var collisions = new List<string>();
            foreach (var (deck, legacyId) in rooms)
            {
                var id = Normalize(deck, legacyId);
                if (!seen.Add(id) && !collisions.Contains(id)) collisions.Add(id);
            }
            return collisions;
        }

        public static bool IsWellFormed(string id) => !string.IsNullOrEmpty(id) && WellFormed.IsMatch(id);

        public static string? DeckOf(string id)
        {
            int cut = id.IndexOf('/');
            return cut > 0 ? id.Substring(0, cut) : null;
        }

        /// <summary><c>Deck_C_Development</c> → <c>Deck_C</c>.</summary>
        public static string DeckOfScene(string sceneName)
            => sceneName.EndsWith(DevelopmentSuffix)
                ? sceneName.Substring(0, sceneName.Length - DevelopmentSuffix.Length)
                : sceneName;
    }
}
