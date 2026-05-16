#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomDoorInteractableTests
    {
        // ── helpers ──────────────────────────────────────────────────────────

        private static RoomDoorInteractable MakeDoor(
            DoorData data,
            RoomController destination,
            GameObject doorPrefab,
            IRoomOrchestrator orchestrator)
        {
            var go   = new GameObject();
            var door = go.AddComponent<RoomDoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("data").objectReferenceValue                 = data;
            so.FindProperty("destination").objectReferenceValue          = destination;
            so.FindProperty("doorTransitionPrefab").objectReferenceValue = doorPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            door.Construct(orchestrator);
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

        private static InteractionContext MakeContext(FakeDialogue dialogue, FakeInventory inventory)
            => new(inventory, null!, dialogue, null!, null!);

        // ── tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_callsTransitionImmediately()
        {
            var data         = MakeUnlockedDoor();
            var destination  = MakeRoom();
            var prefab       = new GameObject("DoorPrefab");
            var orchestrator = new FakeOrchestrator();
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(new FakeDialogue(), new FakeInventory()));

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "should transition to the configured destination");
            Assert.AreEqual(prefab, orchestrator.LastDoorPrefab,
                "should pass the configured door prefab");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
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

            door.Interact(MakeContext(dialogue, new FakeInventory()));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination, "must not transition when locked with no key");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
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
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_locked", dialogue.LastNodeName);
            Assert.IsNull(orchestrator.LastDestination);

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
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
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 0) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsNull(orchestrator.LastDestination, "must not transition before dialogue completes");

            dialogue.LastOnComplete!.Invoke();

            Assert.AreEqual(destination, orchestrator.LastDestination,
                "must transition after dialogue completes");

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
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
            var inventory    = new FakeInventory { UseKeyResult = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, 3) };
            var door         = MakeDoor(data, destination, prefab, orchestrator);

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsTrue(inventory.RemoveItemCalled, "must remove item from inventory when key is depleted");
            Assert.AreEqual(3, inventory.RemovedSlotIndex);

            Object.DestroyImmediate(door.gameObject);
            Object.DestroyImmediate(destination.gameObject);
            Object.DestroyImmediate(prefab);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeOrchestrator : IRoomOrchestrator
        {
            public RoomController? LastDestination { get; private set; }
            public GameObject?     LastDoorPrefab  { get; private set; }

            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
            {
                LastDestination = destination;
                LastDoorPrefab  = doorPrefab;
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
                LastNodeName   = nodeName;
                LastOnComplete = onComplete;
            }
        }

        private sealed class FakeInventory : IInventoryService
        {
            public KeyUseOutcome UseKeyResult    = new(KeyUseResult.NotFound, -1);
            public bool          RemoveItemCalled { get; private set; }
            public int           RemovedSlotIndex { get; private set; } = -1;

            public IReadOnlyList<InventorySlot> Slots                                  => Array.Empty<InventorySlot>();
            public int  SlotCount                                                       => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0)     => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)                   => false;
            public void RemoveItem(int slotIndex) { RemoveItemCalled = true; RemovedSlotIndex = slotIndex; }
            public void MoveItem(int fromSlot, int toSlot)                             { }
            public void EquipWeapon(int slotIndex, int operatorSlot)                   { }
            public void UnequipWeapon(int slotIndex)                                   { }
            public int  GetEquippedWeaponIndex(int operatorSlot)                       => -1;
            public bool CanReload(int slotIndex, int operatorSlot)                     => false;
            public void ReloadOperator(int slotIndex, int operatorSlot)                { }
            public bool TryCombine(int slotA, int slotB)                               => false;
            public KeyUseOutcome TryUseKey(string keyItemId)                           => UseKeyResult;
        }
    }
}
