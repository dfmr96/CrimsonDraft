#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.Events;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>Placed as a trigger volume ("the area") somewhere in a room. Reacts to a BEEPER
    /// SEND (BeeperSignalSentEvent) while the player is standing inside it and the transmitted
    /// code matches expectedCode exactly. onCodeAccepted is a plain UnityEvent so each placement
    /// can do whatever it needs to -- grant an item, play an animation, open a door -- same idea
    /// as ItemSocketInteractable.onActivated.
    ///
    /// Construct() is called by BeeperReceiverBootstrap from NavigationScope's
    /// cachedBeeperReceivers, not [Inject] directly -- there can be many scattered instances per
    /// scene, and that's how every other many-instances interactable in this project
    /// (DocumentInteractable, PickupInteractable, CombatTrigger...) gets its DI wiring. Remember
    /// to press "Cache Scene Beeper Receivers" on NavigationScope after placing a new one, same
    /// as note pickups -- otherwise Construct() never runs and nothing happens on SEND.</summary>
    public sealed class BeeperReceiverInteractable : MonoBehaviour
    {
        [SerializeField] private string     expectedCode   = "SOS";
        [SerializeField] private UnityEvent onCodeAccepted = new();

        // False lets the receiver fire again on every matching SEND while the player is still in
        // range (e.g. something that just toggles). True (the default) latches after the first
        // success, matching ItemSocketInteractable's one-shot IsActivated.
        [SerializeField] private bool oneShot = true;

        private BeeperSignalRegistry? registry;
        private IDisposable?          signalSubscription;

        private bool playerInRange;
        private bool consumed;

        public void Construct(BeeperSignalRegistry registry, ISubscriber<BeeperSignalSentEvent> signalSubscriber)
        {
            this.registry           = registry;
            this.signalSubscription = signalSubscriber.Subscribe(OnSignalSent);
        }

        void OnDestroy() => this.signalSubscription?.Dispose();

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) this.playerInRange = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player")) this.playerInRange = false;
        }

        void OnSignalSent(BeeperSignalSentEvent e)
        {
            if (this.oneShot && this.consumed) return;
            if (!this.playerInRange) return;
            if (!string.Equals(e.Code, this.expectedCode, StringComparison.OrdinalIgnoreCase)) return;

            this.consumed = true;

            // Same "someone caught it" signal BeeperTabController's result LED reads --
            // clearing CurrentSignal here (synchronously, during Publish) is how it tells
            // received (green) from nobody-caught-it (red).
            this.registry?.ClearSignal();

            this.onCodeAccepted.Invoke();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            if (col is BoxCollider box)
                Gizmos.DrawCube(box.center, box.size);
            else if (col is SphereCollider sphere)
                Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
#endif
    }
}
