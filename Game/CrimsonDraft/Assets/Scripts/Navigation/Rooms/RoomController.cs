#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        [SerializeField] private Transform spawnPoint = null!;

        public Transform SpawnPoint => this.spawnPoint;

        public void Activate()   => gameObject.SetActive(true);
        public void Deactivate() => gameObject.SetActive(false);
    }
}
