#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Marks a string field as a room id: the inspector shows a picker of every room of
    /// the game (from the RoomCatalog) instead of a text box.</summary>
    public sealed class RoomIdAttribute : PropertyAttribute { }
}
