#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public interface IRoomOrchestrator
    {
        RoomController? CurrentRoom { get; }
        /// <summary><paramref name="departurePosition"/> is the door used, to pick between several doors back.</summary>
        UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition);
        /// <summary>Jumps straight into a room (save load). <paramref name="cameraPath"/> names the
        /// shot to show, as a transform path under the room; without it, or if it no longer
        /// resolves, the room picks its shot from the player's current position.</summary>
        void ActivateRoomImmediate(string roomId, string? cameraPath = null);
    }
}
