#nullable enable

using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CrimsonDraft.Navigation.Rooms
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Navigation/RoomTransitionContext")]
    public sealed class RoomTransitionContext : ScriptableObject
    {
        public RoomController? StartingRoom { get; private set; }
        public GameObject?     DoorPrefab  { get; private set; }
        public InputAction?    SkipAction  { get; private set; }

        public void SetStartingRoom(RoomController room) => this.StartingRoom = room;

        private Action? onComplete;

        public void Set(GameObject doorPrefab, InputAction skipAction, Action onComplete)
        {
            this.DoorPrefab  = doorPrefab;
            this.SkipAction  = skipAction;
            this.onComplete  = onComplete;
        }

        public void NotifyComplete()
        {
            var callback    = this.onComplete;
            this.onComplete = null;
            this.DoorPrefab = null;
            this.SkipAction = null;
            callback?.Invoke();
        }
    }
}
