#nullable enable

using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Events;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    /// <summary>Reveal() is what a safe / beeper / socket calls to show the item hidden behind it. On load
    /// those effects replay, so an item the player already took must not come back.</summary>
    public sealed class PickupInteractableRevealTests
    {
        private GameObject go = null!;
        private PickupInteractable pickup = null!;
        private PickupRegistry registry = null!;

        [SetUp]
        public void SetUp()
        {
            this.go = new GameObject("KI_xx_Pickeable");
            this.go.SetActive(false); // hidden behind the safe until revealed
            this.pickup = this.go.AddComponent<PickupInteractable>();
            var so = new SerializedObject(this.pickup);
            so.FindProperty("pickupId").stringValue = "KI_xx";
            so.ApplyModifiedPropertiesWithoutUndo();
            this.registry = new PickupRegistry();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.go);

        [Test]
        public void NotCollected_revealShowsIt()
        {
            Construct();
            this.pickup.Reveal();
            Assert.IsTrue(this.go.activeSelf);
        }

        [Test]
        public void AlreadyCollected_revealKeepsItHidden()
        {
            this.registry.SetCollected("KI_xx");
            Construct();
            this.pickup.Reveal();
            Assert.IsFalse(this.go.activeSelf);
        }

        [Test]
        public void RevealedBeforeConstruct_stillEndsHiddenWhenCollected()
        {
            // Bootstrap order isn't guaranteed: Construct hides it again once it knows.
            this.registry.SetCollected("KI_xx");
            this.pickup.Reveal();
            Construct();
            Assert.IsFalse(this.go.activeSelf);
        }

        private void Construct() => this.pickup.Construct(this.registry, new NullPublisher(), new NoteRegistry());

        private sealed class NullPublisher : IPublisher<NoteCollectedEvent>
        {
            public void Publish(NoteCollectedEvent message) { }
        }
    }
}
