#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(PickupPoints))]
    public sealed class PickupPointAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event pickupEvent = new();

        public void PlayPickup() => pickupEvent?.Post(gameObject);
    }
}
