#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomDoorInteractableTests
    {
        private static readonly KeyItem TestKey = (KeyItem)InventoryItemFactory.Create(InventoryTestData.Key());

        // ── helpers ──────────────────────────────────────────────────────────

        private static RoomDoorInteractable MakeDoor(
            DoorData          data,
            RoomController    destination,
            GameObject        doorPrefab,
            IRoomOrchestrator orchestrator,
            DoorStateRegistry? registry = null,
            string            doorId   = "test-door",
            RoomDoorInteractable? unlocksOnCross = null)
        {
            var go   = new GameObject();
            var door = go.AddComponent<RoomDoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("data").objectReferenceValue                 = data;
            so.FindProperty("destination").objectReferenceValue          = destination;
            so.FindProperty("doorTransitionPrefab").objectReferenceValue = doorPrefab;
            so.FindProperty("doorId").stringValue                        = doorId;
            so.FindProperty("unlocksOnCross").objectReferenceValue       = unlocksOnCross;
            so.ApplyModifiedPropertiesWithoutUndo();
            door.Construct(orchestrator, registry ?? new DoorStateRegistry());
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

        private static RoomController MakeRoom()
            => new GameObject("Room").AddComponent<RoomController>();

        private static InteractionContext MakeContext(FakeDialogue dialogue, FakeInventoryService inventory)
            => new(inventory, null!, dialogue, null!, null!, null!, null!, null!, null!, null!, null!);

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_callsTransitionImmediately()
        {
            var data         = MakeUnlockedDoor();
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "should transition to the configured destination");
            Assert.AreEqual(prefab, orchestrator.LastDoorPrefab,
                "should pass the configured door prefab");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenLockedNoKey_startsDialogue_doesNotTransition()
        {
            var data         = MakeLockedDoor("door_locked");
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, new FakeInventoryService()));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination, "must not transition when locked with no key");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenNotLocked_marksDoorUnlockedInRegistry()
        {
            var registry     = new DoorStateRegistry();
            var data         = MakeUnlockedDoor();
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(DoorMapState.Unlocked, registry.GetMapState("door-1"),
                "crossing an open door must mark it Unlocked on the map");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenLockedNoKey_marksDoorLockedInRegistry()
        {
            var registry    = new DoorStateRegistry();
            var data        = MakeLockedDoor("door_locked");
            var destination = MakeRoom();
            var prefab      = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door        = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(DoorMapState.Locked, registry.GetMapState("door-1"));

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenLockedKeyNotFound_startsDialogue_doesNotTransition()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_locked", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.NotFound, TestKey) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination);

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenKeySuccess_startsDialogue_thenTransitionsOnComplete()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_test", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.Success, TestKey) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsNull(orchestrator.LastDestination, "must not transition before dialogue completes");

            dialogue.LastOnComplete!.Invoke();

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "must transition after dialogue completes");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_test", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, TestKey) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            CollectionAssert.Contains(inventory.Removed, TestKey, "must remove item from inventory when key is depleted");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void RestoreFromRegistry_whenRegistryHasDoorUnlocked_transitionsImmediatelyDespiteLockedData()
        {
            var registry     = new DoorStateRegistry();
            registry.SetUnlocked("door-1");
            var data         = MakeLockedDoor("door_locked");
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "registry unlock must override locked data flag");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenCrossingPairedDoor_unlocksTheOtherSideMechanismLock()
        {
            var registry       = new DoorStateRegistry();
            var destinationA   = MakeRoom();
            var destinationB   = MakeRoom();
            var prefab         = new GameObject("DoorPrefab");
            var orchestrator   = new FakeOrchestrator();

            // Door A: locked from the other side until Door B is crossed.
            var doorA = MakeDoor(MakeUnlockedDoor(), destinationA, prefab, orchestrator, registry, "door-a");
            var soA   = new SerializedObject(doorA);
            soA.FindProperty("useMechanismLock").boolValue = true;
            soA.FindProperty("mechanismLocked").boolValue  = true;
            soA.ApplyModifiedPropertiesWithoutUndo();

            // Door B: the free side, paired to unlock Door A once crossed.
            var doorB = MakeDoor(MakeUnlockedDoor(), destinationB, prefab, orchestrator, registry, "door-b", doorA);

            Assert.IsTrue(doorA.MechanismLocked, "door A should start locked from the other side");

            doorB.Interact(MakeContext(new FakeDialogue(), new FakeInventoryService()));

            Assert.IsFalse(doorA.MechanismLocked, "crossing door B must unlock door A");

            UnityEngine.Object.DestroyImmediate(doorA.gameObject);
            UnityEngine.Object.DestroyImmediate(doorB.gameObject);
            UnityEngine.Object.DestroyImmediate(destinationA.gameObject);
            UnityEngine.Object.DestroyImmediate(destinationB.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Interact_whenKeySuccess_updatesRegistry()
        {
            var registry     = new DoorStateRegistry();
            var keyData      = MakeKeyItem("key-1", "Key 1");
            var data         = MakeLockedDoor("door_test", keyData);
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var dialogue     = new FakeDialogue();
            var inventory    = new FakeInventoryService { NextKeyOutcome = new KeyUseOutcome(KeyUseResult.Success, TestKey) };
            var door         = MakeDoor(data, destination, prefab, orchestrator, registry, "door-1");

            door.Interact(MakeContext(dialogue, inventory));
            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(registry.IsUnlocked("door-1"), "registry must be updated when door is unlocked");

            UnityEngine.Object.DestroyImmediate(door.gameObject);
            UnityEngine.Object.DestroyImmediate(destination.gameObject);
            UnityEngine.Object.DestroyImmediate(prefab);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeOrchestrator : IRoomOrchestrator
        {
            public RoomController? LastDestination { get; private set; }
            public GameObject?     LastDoorPrefab  { get; private set; }
            public RoomController? CurrentRoom     => null;

            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
            {
                LastDestination = destination;
                LastDoorPrefab  = doorPrefab;
                return UniTask.CompletedTask;
            }

            public void ActivateRoomImmediate(string roomId) { }
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
                LastNodeName   = nodeName;
                LastOnComplete = onComplete;
            }

            public void SetVariable(string name, object value) { }
        }

    }
}
