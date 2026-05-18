#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public interface IRoomOrchestrator
    {
        UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab);
    }
}
