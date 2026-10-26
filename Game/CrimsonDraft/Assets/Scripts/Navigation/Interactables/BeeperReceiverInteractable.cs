#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Gates;

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
    /// as note pickups -- otherwise Construct() never runs and nothing happens on SEND.
    ///
    /// It is a gate (gate:&lt;gateId&gt;): catching the code completes it in GateRegistry, which is saved.
    /// A one-shot receiver bound to a registry where it is already complete (a loaded save, a revisited
    /// scene) latches and replays onCodeAccepted inside WorldRestore, so whatever it opened is simply open
    /// again (SlideOpenOnCall snaps instead of sliding) -- wire item reveals through
    /// PickupInteractable.Reveal, not GameObject.SetActive, so a taken item stays taken.</summary>
    public sealed class BeeperReceiverInteractable : MonoBehaviour, IGate
    {
        [SerializeField] private string     expectedCode   = "SOS";
        [SerializeField] private UnityEvent onCodeAccepted = new();

        [Tooltip("Unique id, saved once the code is caught. Camera zones and milestones wait on it as gate:<id>.")]
        [FormerlySerializedAs("puzzleId")]
        [SerializeField] private string gateId = "";

        // False lets the receiver fire again on every matching SEND while the player is still in
        // range (e.g. something that just toggles). True (the default) latches after the first
        // success, matching ItemSocketInteractable's one-shot IsActivated.
        [SerializeField] private bool oneShot = true;

        private BeeperSignalRegistry? registry;
        private GateRegistry?         gates;
        private IDisposable?          signalSubscription;

        private bool playerInRange;
        private bool consumed;

        // Lets a sibling coordinator (e.g. BeeperDoorMechanism) react to an accepted code from
        // code, without needing to be wired as a persistent listener in the Inspector.
        public UnityEvent OnCodeAccepted => this.onCodeAccepted;

        public string GateId => GateIds.Generic(this.gateId);

        /// <summary>Called by BeeperReceiverBootstrap after the save has loaded. Restores a completed
        /// one-shot receiver (see the class summary); a repeatable one only records, as replaying a
        /// toggle would flip it rather than restore it.</summary>
        public void BindGates(GateRegistry registry)
        {
            this.gates = registry;
            if (!this.oneShot || this.consumed || !registry.IsCompleted(this.gateId)) return;

            this.consumed = true;
            using (WorldRestore.Begin()) this.onCodeAccepted.Invoke();
        }

        public void Construct(
            BeeperSignalRegistry registry, ISubscriber<BeeperSignalSentEvent> signalSubscriber,
            IInventoryService inventory)
        {
            this.registry           = registry;
            this.signalSubscription = signalSubscriber.Subscribe(OnSignalSent);
            GetComponent<BeeperRadioIndicator>()?.Bind(inventory);
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

            this.gates?.SetCompleted(this.gateId);
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
