#nullable enable

using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class StorageBoxInteractableTests
    {
        private sealed class FakePublisher : IPublisher<StorageOpenRequestedEvent>
        {
            public int Count;
            public void Publish(StorageOpenRequestedEvent message) => this.Count++;
        }

        [Test]
        public void Interact_publishesStorageOpenRequested()
        {
            var go        = new GameObject("StorageBox");
            var box       = go.AddComponent<StorageBoxInteractable>();
            var publisher = new FakePublisher();
            var context   = new InteractionContext(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, publisher);

            box.Interact(context);

            Assert.AreEqual(1, publisher.Count);
            Object.DestroyImmediate(go);
        }
    }
}
