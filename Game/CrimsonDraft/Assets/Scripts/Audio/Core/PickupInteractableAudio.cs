#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class PickupInteractableAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger pickupTrigger = new();

        public void Play() => pickupTrigger.Fire(gameObject);
    }
}
