#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Dialogue;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Map;

namespace CrimsonDraft.Tests
{
    public sealed class MapPickupInteractableTests
    {
        private static MapPickupInteractable MakePickup(
            PickupRegistry    pickupRegistry,
            KnownMapsRegistry knownMaps,
            string            pickupId = "map-a",
            string            sceneName = "Deck_B")
        {
            var map = ScriptableObject.CreateInstance<MapData>();
            var mapSo = new SerializedObject(map);
            mapSo.FindProperty("sceneName").stringValue = sceneName;
            mapSo.ApplyModifiedPropertiesWithoutUndo();

            var item = ScriptableObject.CreateInstance<ItemData>();

            var go      = new GameObject();
            var pickup  = go.AddComponent<MapPickupInteractable>();
            var so      = new SerializedObject(pickup);
            so.FindProperty("pickupId").stringValue     = pickupId;
            so.FindProperty("item").objectReferenceValue = item;
            so.FindProperty("map").objectReferenceValue  = map;
            so.ApplyModifiedPropertiesWithoutUndo();

            pickup.Construct(pickupRegistry, knownMaps);
            return pickup;
        }

        private static InteractionContext MakeContext(FakeDialogue dialogue, FakeInventory inventory)
            => new(inventory, null!, null!, null!, null!, dialogue, null!, null!);

        [Test]
        public void Construct_whenAlreadyCollected_deactivatesGameObject()
        {
            var registry = new PickupRegistry();
            registry.SetCollected("map-a");

            var pickup = MakePickup(registry, new KnownMapsRegistry());

            Assert.IsFalse(pickup.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(pickup.gameObject);
        }

        [Test]
        public void Interact_onSuccess_marksDeckKnown_andCollected()
        {
            var pickupRegistry = new PickupRegistry();
            var knownMaps      = new KnownMapsRegistry();
            var pickup         = MakePickup(pickupRegistry, knownMaps);
            var dialogue       = new FakeDialogue();
            var inventory      = new FakeInventory { AddItemAutoResult = true };

            pickup.Interact(MakeContext(dialogue, inventory));
            dialogue.LastCommands!["try_pickup"].Invoke();
            dialogue.LastOnComplete!.Invoke();

            Assert.IsTrue(pickupRegistry.IsCollected("map-a"));
            Assert.IsTrue(knownMaps.IsKnown("Deck_B"));
            Assert.IsFalse(pickup.gameObject.activeSelf);

            UnityEngine.Object.DestroyImmediate(pickup.gameObject);
        }

        [Test]
        public void Interact_whenInventoryFull_doesNotMarkDeckKnown()
        {
            var pickupRegistry = new PickupRegistry();
            var knownMaps      = new KnownMapsRegistry();
            var pickup         = MakePickup(pickupRegistry, knownMaps);
            var dialogue       = new FakeDialogue();
            var inventory      = new FakeInventory { AddItemAutoResult = false };

            pickup.Interact(MakeContext(dialogue, inventory));
            dialogue.LastCommands!["try_pickup"].Invoke();
            dialogue.LastOnComplete!.Invoke();

            Assert.IsFalse(pickupRegistry.IsCollected("map-a"));
            Assert.IsFalse(knownMaps.IsKnown("Deck_B"));

            UnityEngine.Object.DestroyImmediate(pickup.gameObject);
        }

        // ── fakes ─────────────────────────────────────────────────────────────

        private sealed class FakeDialogue : IPickupDialogueService
        {
            public bool                                  IsRunning      => false;
            public string?                                LastNodeName   { get; private set; }
            public Action?                                LastOnComplete { get; private set; }
            public IReadOnlyDictionary<string, Action>?   LastCommands   { get; private set; }

            public void StartDialogue(
                string                               nodeName,
                IReadOnlyDictionary<string, object>? variables  = null,
                Action?                              onComplete = null,
                IReadOnlyDictionary<string, Action>? commands   = null)
            {
                LastNodeName   = nodeName;
                LastOnComplete = onComplete;
                LastCommands   = commands;
            }

            public void SetVariable(string name, object value) { }
        }

        private sealed class FakeInventory : IInventoryService
        {
            public bool AddItemAutoResult;

            public IReadOnlyList<InventorySlot> Slots                              => Array.Empty<InventorySlot>();
            public int  SlotCount                                                   => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)               => AddItemAutoResult;
            public void RemoveItem(int slotIndex)                                  { }
            public void MoveItem(int fromSlot, int toSlot)                         { }
            public void EquipWeapon(int slotIndex, int operatorSlot)               { }
            public void UnequipWeapon(int slotIndex)                               { }
            public int  GetEquippedWeaponIndex(int operatorSlot)                   => -1;
            public bool CanReload(int slotIndex, int operatorSlot)                 => false;
            public void ReloadOperator(int slotIndex, int operatorSlot)            { }
            public bool TryCombine(int slotA, int slotB)                               => false;
            public KeyUseOutcome   TryUseKey(string keyItemId)                         => new(KeyUseResult.NotFound, -1);
            public void            SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public void            LoadState(InventorySlot[] slots)                    { }
            public InventorySlot[] GetRawSlots()                                       => Array.Empty<InventorySlot>();
        }
    }
}
