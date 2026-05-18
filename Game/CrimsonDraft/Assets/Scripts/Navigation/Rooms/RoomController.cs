#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        [SerializeField] private string roomId = "";

        public string RoomId => this.roomId;

        public void Activate()   => gameObject.SetActive(true);
        public void Deactivate() => gameObject.SetActive(false);
    }
}
