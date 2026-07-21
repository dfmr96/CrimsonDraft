#nullable enable

using UnityEngine;
using VContainer;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Interactables
{
    public sealed class RadioInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private AK.Wwise.Event playRadioEvent = new();
        [SerializeField] private AK.Wwise.Event stopRadioEvent = new();
        [SerializeField] private AK.Wwise.RTPC  signalProximityRtpc = new();
        [SerializeField] private float          maxDistance         = 15f;
        [SerializeField] private float          maxRtpcValue        = 100f;

        [Inject] private PlayerController playerController = null!;

        private bool isPlaying;

        public void Interact(InteractionContext context)
        {
            if (this.isPlaying)
            {
                this.stopRadioEvent.Post(gameObject);
            }
            else
            {
                this.playRadioEvent.Post(gameObject);
            }

            this.isPlaying = !this.isPlaying;
        }

        private void Update()
        {
            var distance           = Vector3.Distance(transform.position, this.playerController.transform.position);
            var normalizedDistance = Mathf.Clamp01(distance / this.maxDistance);

            this.signalProximityRtpc.SetValue(gameObject, normalizedDistance * this.maxRtpcValue);
        }
    }
}
