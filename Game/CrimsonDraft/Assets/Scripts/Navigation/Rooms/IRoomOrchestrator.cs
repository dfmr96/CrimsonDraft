#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public interface IRoomOrchestrator
    {
        RoomController? CurrentRoom { get; }
        UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab);
        void ActivateRoomImmediate(string roomId);
    }
}
