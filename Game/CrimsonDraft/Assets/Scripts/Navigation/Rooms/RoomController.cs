#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        public void Activate()   => gameObject.SetActive(true);
        public void Deactivate() => gameObject.SetActive(false);
    }
}
