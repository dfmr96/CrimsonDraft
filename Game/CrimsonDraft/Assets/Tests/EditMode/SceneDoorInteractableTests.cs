#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class SceneDoorInteractableTests
    {
        private static readonly KeyItem TestKey = (KeyItem)InventoryItemFactory.Create(InventoryTestData.Key());

        // ── helpers ──────────────────────────────────────────────────────────

        private static SceneDoorInteractable MakeDoor(
            DoorData                data,
            IFloorTransitionService floorService,
            DoorStateRegistry       registry,
            string                  doorId = "test-door")
        {
            var go   = new GameObject();
            var door = go.AddComponent<SceneDoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("doorId").stringValue             = doorId;
            so.FindProperty("data").objectReferenceValue      = data;
            so.FindProperty("targetSceneName").stringValue    = "Deck_C";
            so.FindProperty("targetEntryPointId").stringValue = "test-entry";
            so.ApplyModifiedPropertiesWithoutUndo();
            door.Construct(floorService, registry);
            return door;
        }

        private static DoorData MakeUnlockedDoor()
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static DoorData MakeLockedDoor(string yarnNode, KeyItemData? keyItem = null)
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue = true;
            if (keyItem != null)
                so.FindProperty("keyItem").objectReferenceValue = keyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.DialogueReference.nodeName = yarnNode;
            return data;
        }

        private static KeyItemData MakeKeyItem(string id, string displayName)
        {
            var data = ScriptableObject.CreateInstance<KeyItemData>();
            var so   = new SerializedObject(data);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static InteractionContext MakeContext(FakeDialogue dialogue, FakeInventoryService inventory)
            => new(inventory, null!, dialogue, null!, null!, null!, null!, null!, null!, null!, null!);

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_callsFloorTransitionImmediately()
        {
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeUnlockedDoor(), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.IsTrue(fakeService.TransitionCalled, "must call floor transition immediately");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void RestoreFromRegistry_whenRegistryHasDoorUnlocked_transitionsImmediatelyDespiteLockedData()
        {
            var registry = new DoorStateRegistry();
            registry.SetUnlocked("door-1");
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeLockedDoor("door_locked"), fakeService, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.IsTrue(fakeService.TransitionCalled, "registry unlock must override locked data flag");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenLockedNoKey_startsDialogue_doesNotTransition()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var door        = MakeDoor(MakeLockedDoor("door_locked"), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, new FakeInventoryService()));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsFalse(fakeService.TransitionCalled, "must not transition when locked");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenNotLocked_marksDoorUnlockedInRegistry()
        {
            var registry    = new DoorStateRegistry();
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeUnlockedDoor(), fakeService, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-1"),
                "crossing an open door must mark it Unlocked on the map");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenLockedNoKey_marksDoorLockedInRegistry()
        {
            var registry    = new DoorStateRegistry();
            var fakeService = new FakeFloorService();
            var door        = MakeDoor(MakeLockedDoor("door_locked"), fakeService, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-1"));

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeySuccess_startsDialogue_thenTransitionsOnComplete()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.Success, TestKey) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsFalse(fakeService.TransitionCalled, "must not transition before dialogue completes");

            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(fakeService.TransitionCalled, "must transition after dialogue completes");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeySuccess_updatesRegistry()
        {
            var registry    = new DoorStateRegistry();
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.Success, TestKey) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, registry, "door-1");

            door.Interact(MakeContext(dialogue, inventory));
            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(registry.IsUnlocked("door-1"), "registry must be updated on unlock");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var fakeService = new FakeFloorService();
            var dialogue    = new FakeDialogue();
            var keyData     = MakeKeyItem("key-1", "Key 1");
            var inventory   = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, TestKey) };
            var door        = MakeDoor(MakeLockedDoor("door_test", keyData), fakeService, new DoorStateRegistry());

            door.Interact(MakeContext(dialogue, inventory));

            CollectionAssert.Contains(inventory.Removed, TestKey, "must remove item from inventory when key is depleted");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeFloorService : IFloorTransitionService
        {
            public bool TransitionCalled { get; private set; }

            public UniTask TransitionToFloorAsync(
                string from, string to, string entryId, GameObject doorPrefab)
            {
                this.TransitionCalled = true;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeDialogue : IDialogueService
        {
            public bool    IsRunning      => false;
            public string? LastNodeName   { get; private set; }
            public Action? LastOnComplete { get; private set; }

            public void StartDialogue(
                string                               nodeName,
                IReadOnlyDictionary<string, object>? variables  = null,
                Action?                              onComplete  = null,
                IReadOnlyDictionary<string, Action>? commands   = null)
            {
                this.LastNodeName   = nodeName;
                this.LastOnComplete = onComplete;
            }

            public void SetVariable(string name, object value) { }
        }

    }
}
