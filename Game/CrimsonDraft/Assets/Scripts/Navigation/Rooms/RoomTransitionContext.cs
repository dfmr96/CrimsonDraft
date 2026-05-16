#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Navigation/RoomTransitionContext")]
    public sealed class RoomTransitionContext : ScriptableObject
    {
        public GameObject? DoorPrefab { get; private set; }

        private Action? onComplete;

        public void Set(GameObject doorPrefab, Action onComplete)
        {
            this.DoorPrefab  = doorPrefab;
            this.onComplete  = onComplete;
        }

        public void NotifyComplete()
        {
            var callback    = this.onComplete;
            this.onComplete = null;
            this.DoorPrefab = null;
            callback?.Invoke();
        }
    }
}
