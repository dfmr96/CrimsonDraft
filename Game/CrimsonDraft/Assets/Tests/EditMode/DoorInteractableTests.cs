#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class DoorInteractableTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        private static DoorData MakeDoorData(bool locked, string yarnNodeName, KeyItemData? keyItem = null)
        {
            var data = ScriptableObject.CreateInstance<DoorData>();
            var so   = new SerializedObject(data);
            so.FindProperty("locked").boolValue = locked;
            if (keyItem != null)
                so.FindProperty("keyItem").objectReferenceValue = keyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.DialogueReference.nodeName = yarnNodeName;
            return data;
        }

        private static KeyItemData MakeKeyItemData(string id, string displayName)
        {
            var data = ScriptableObject.CreateInstance<KeyItemData>();
            var so   = new SerializedObject(data);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.KeyItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static DoorInteractable MakeDoor(DoorData data)
        {
            var go   = new GameObject();
            var door = go.AddComponent<DoorInteractable>();
            var so   = new SerializedObject(door);
            so.FindProperty("data").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private static InteractionContext MakeContext(
            FakeDoorDialogueService  dialogue,
            FakeDoorInventoryService inventory)
        {
            return new InteractionContext(inventory, null!, dialogue, null!, null!, null!, null!, null!);
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void Interact_whenNotLocked_doesNotStartDialogue()
        {
            var data      = MakeDoorData(locked: false, yarnNodeName: "door_test");
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService();

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsNull(dialogue.LastNodeName, "unlocked door should not start dialogue");
        }

        [Test]
        public void Interact_whenLockedNoKeyItem_startsDialogueWithDoorNodeName()
        {
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: null);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService();

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_test", dialogue.LastNodeName);
        }

        [Test]
        public void Interact_whenLockedKeyNotFound_startsDialogueWithDoorNodeName()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.NotFound, -1)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_test", dialogue.LastNodeName);
        }

        [Test]
        public void Interact_whenKeySuccess_startsOpenedFeedbackDialogue_andOnCompleteOpens()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.Success, 2)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.AreEqual("door_opened_feedback", dialogue.LastNodeName);
            Assert.AreEqual("opened", dialogue.LastVariables!["$outcome"]);
            Assert.AreEqual("Keycard A", dialogue.LastVariables["$key_name"]);
            Assert.IsNotNull(dialogue.LastOnComplete, "onComplete callback should be set");

            dialogue.LastOnComplete!.Invoke();

            var dialogue2 = new FakeDoorDialogueService();
            door.Interact(MakeContext(dialogue2, inventory));
            Assert.IsNull(dialogue2.LastNodeName, "door is now unlocked, no dialogue");
        }

        [Test]
        public void Interact_whenKeyDepletedAfterUse_removesItemFromInventory()
        {
            var keyData   = MakeKeyItemData("keycard-a", "Keycard A");
            var data      = MakeDoorData(locked: true, yarnNodeName: "door_test", keyItem: keyData);
            var door      = MakeDoor(data);
            var dialogue  = new FakeDoorDialogueService();
            var inventory = new FakeDoorInventoryService
            {
                TryUseKeyResult = new KeyUseOutcome(KeyUseResult.DepletedAfterUse, 3)
            };

            door.Interact(MakeContext(dialogue, inventory));

            Assert.IsTrue(inventory.RemoveItemCalled);
            Assert.AreEqual(3, inventory.RemovedSlotIndex);
        }

        // ── Fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeDoorDialogueService : IDialogueService
        {
            public bool IsRunning => false;
            public string?                              LastNodeName   { get; private set; }
            public IReadOnlyDictionary<string, object>? LastVariables  { get; private set; }
            public Action?                              LastOnComplete { get; private set; }

            public void StartDialogue(
                string                                 nodeName,
                IReadOnlyDictionary<string, object>?  variables  = null,
                Action?                                onComplete = null,
                IReadOnlyDictionary<string, Action>?  commands   = null)
            {
                LastNodeName   = nodeName;
                LastVariables  = variables;
                LastOnComplete = onComplete;
            }

            public void SetVariable(string name, object value) { }
        }

        private sealed class FakeDoorInventoryService : IInventoryService
        {
            public KeyUseOutcome TryUseKeyResult  = new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public bool          RemoveItemCalled  { get; private set; }
            public int           RemovedSlotIndex  { get; private set; } = -1;

            public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
            public int  SlotCount                                           => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)               => false;
            public void RemoveItem(int slotIndex) { RemoveItemCalled = true; RemovedSlotIndex = slotIndex; }
            public void MoveItem(int fromSlot, int toSlot)         { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex)               { }
            public int  GetEquippedWeaponIndex(int operatorSlot)   => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB)                       => false;
            public KeyUseOutcome TryUseKey(string keyItemId)                   => TryUseKeyResult;
            public void          SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public void          LoadState(InventorySlot[] slots)               { }
            public InventorySlot[] GetRawSlots()                               => Array.Empty<InventorySlot>();
        }
    }
}
