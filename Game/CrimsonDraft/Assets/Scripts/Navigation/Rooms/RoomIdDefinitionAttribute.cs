#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>Marks the string field that defines a room's own id: the inspector shows a deck
    /// popup plus a name box and composes <c>Deck/ROOM</c> (see <see cref="RoomIds.Compose"/>).</summary>
    public sealed class RoomIdDefinitionAttribute : PropertyAttribute { }
}
