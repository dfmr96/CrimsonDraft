#nullable enable

using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Navigation
{
    public interface IOperatorCorpseSpawner
    {
        void Spawn(RoomController room, Vector3 position, Quaternion rotation);
    }
}
