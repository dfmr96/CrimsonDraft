#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(PickupPoints))]
    public sealed class PickupPointAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger pickupTrigger = new();

        public void PlayPickup() => pickupTrigger.Fire(gameObject);
    }
}
