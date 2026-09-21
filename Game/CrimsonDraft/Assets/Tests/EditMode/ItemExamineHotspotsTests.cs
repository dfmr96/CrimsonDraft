#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Yarn.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ItemExamineHotspotsTests
    {
        private static ItemExamineHotspots MakeHotspots(
            ItemExamineHotspots.Hotspot[] hotspots,
            DialogueReference defaultDialogue)
        {
            var go   = new GameObject();
            var comp = go.AddComponent<ItemExamineHotspots>();

            var hotspotsField = typeof(ItemExamineHotspots).GetField("hotspots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var defaultField = typeof(ItemExamineHotspots).GetField("defaultDialogue",
                BindingFlags.Instance | BindingFlags.NonPublic);

            hotspotsField!.SetValue(comp, hotspots);
            defaultField!.SetValue(comp, defaultDialogue);

            return comp;
        }

        [Test]
        public void GetDialogue_hitMatchingCollider_returnsHotspotDialogue()
        {
            var collider = new GameObject().AddComponent<BoxCollider>();
            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(collider);

            Assert.AreSame(hotspotDialogue, result);
        }

        [Test]
        public void GetDialogue_nullCollider_returnsDefaultDialogue()
        {
            var collider = new GameObject().AddComponent<BoxCollider>();
            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(null);

            Assert.AreSame(defaultDialogue, result);
        }

        [Test]
        public void GetDialogue_hitUnregisteredCollider_returnsDefaultDialogue()
        {
            var registered = new GameObject().AddComponent<BoxCollider>();
            var other       = new GameObject().AddComponent<BoxCollider>();

            var hotspotDialogue = new DialogueReference { nodeName = "hit_node" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = registered, dialogue = hotspotDialogue } },
                defaultDialogue);

            var result = comp.GetDialogue(other);

            Assert.AreSame(defaultDialogue, result);
        }

        [Test]
        public void GetDialogue_multipleHotspots_returnsTheOneThatMatches()
        {
            var colliderA = new GameObject().AddComponent<BoxCollider>();
            var colliderB = new GameObject().AddComponent<BoxCollider>();
            var dialogueA = new DialogueReference { nodeName = "node_a" };
            var dialogueB = new DialogueReference { nodeName = "node_b" };
            var defaultDialogue = new DialogueReference { nodeName = "default_node" };

            var comp = MakeHotspots(
                new[]
                {
                    new ItemExamineHotspots.Hotspot { collider = colliderA, dialogue = dialogueA },
                    new ItemExamineHotspots.Hotspot { collider = colliderB, dialogue = dialogueB },
                },
                defaultDialogue);

            Assert.AreSame(dialogueB, comp.GetDialogue(colliderB));
            Assert.AreSame(dialogueA, comp.GetDialogue(colliderA));
        }

        private sealed class FakeInventoryService : IInventoryService
        {
            private readonly HashSet<string> itemIds;
            public FakeInventoryService(params string[] presentItemIds) => this.itemIds = new HashSet<string>(presentItemIds);

            public bool HasItem(string itemId) => this.itemIds.Contains(itemId);
            public bool TryRemoveItem(string itemId) => this.itemIds.Remove(itemId);

            public IReadOnlyList<InventorySlot> Slots => Array.Empty<InventorySlot>();
            public int  SlotCount                                           => 0;
            public bool AddItem(ItemData data, int operatorSlot, int quantity = 0) => false;
            public bool AddExistingItem(InventoryItem item, int operatorSlot)      => false;
            public bool AddItemAuto(ItemData data, int quantity = 0)               => false;
            public void RemoveItem(int slotIndex) { }
            public void PruneEmptyStacks() { }
            public void MoveItem(int fromSlot, int toSlot)         { }
            public void EquipWeapon(int slotIndex, int operatorSlot) { }
            public void UnequipWeapon(int slotIndex)               { }
            public int  GetEquippedWeaponIndex(int operatorSlot)   => -1;
            public bool CanReload(int slotIndex, int operatorSlot) => false;
            public void ReloadOperator(int slotIndex, int operatorSlot) { }
            public bool TryCombine(int slotA, int slotB)                       => false;
            public bool TryCombine(int slotA, int slotB, int resultSlot, out InventoryItem? combinedItem) { combinedItem = null; return false; }
            public KeyUseOutcome TryUseKey(string keyItemId)                   => new KeyUseOutcome(KeyUseResult.NotFound, -1);
            public void          SetSlotPosition(int slotIndex, int col, int row, int rotation) { }
            public void          LoadState(InventorySlot[] slots)               { }
            public InventorySlot[] GetRawSlots()                               => Array.Empty<InventorySlot>();
        }

        private static ItemData MakeItemData(string itemId)
        {
            var d  = ScriptableObject.CreateInstance<ItemData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue = itemId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        // DialogueReference.IsValid checks project.Program.Nodes.ContainsKey(nodeName)
        // against a real compiled Program -- a bare ScriptableObject.CreateInstance()
        // has no compiled data, so tests that need IsValid == true load the project's
        // actual, already-imported YarnProject asset and reference one of its real
        // node names (e.g. "examine_placeholder") instead of a synthetic one.
        private static YarnProject MakeYarnProjectStandIn()
        {
            var project = UnityEditor.AssetDatabase.LoadAssetAtPath<YarnProject>(
                "Assets/Dialogues/CrimsonDraft.yarnproject");
            Assert.IsNotNull(project, "Test requires Assets/Dialogues/CrimsonDraft.yarnproject to exist.");
            return project!;
        }

        [Test]
        public void Resolve_noRequiredItem_returnsText()
        {
            var collider = new GameObject().AddComponent<BoxCollider>();
            // Needs a real project so DialogueReference.IsValid is true -- Resolve()
            // checks validity (unlike GetDialogue(), which just does identity matching).
            var dialogue = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var comp = MakeHotspots(
                new[] { new ItemExamineHotspots.Hotspot { collider = collider, dialogue = dialogue } },
                new DialogueReference { nodeName = "default_node" });

            var result = comp.Resolve(collider, new FakeInventoryService());

            Assert.IsNotNull(result);
            Assert.IsNull(result!.Value.Prompt);
            Assert.AreSame(dialogue, result.Value.Text);
        }

        [Test]
        public void Resolve_requiredItemPresent_validPrompt_returnsPrompt()
        {
            var collider     = new GameObject().AddComponent<BoxCollider>();
            var requiredItem = MakeItemData("small_key");
            var activationTransform = new GameObject("ActivationTarget").transform;
            // "examine_placeholder" is a real node already in the project's YarnProject,
            // already tagged `examine` -- used here only so DialogueReference.IsValid is
            // true; its actual text is irrelevant to this test.
            var prompt       = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var flavorDialogue = new DialogueReference { nodeName = "locked_node" };
            var hotspot = new ItemExamineHotspots.Hotspot
            {
                collider             = collider,
                dialogue             = flavorDialogue,
                requiredItem         = requiredItem,
                promptDialogue       = prompt,
                activationTransform  = activationTransform,
            };
            var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

            var result = comp.Resolve(collider, new FakeInventoryService("small_key"));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result!.Value.Prompt);
            Assert.AreSame(flavorDialogue, result.Value.Prompt!.Value.FlavorDialogue);
            Assert.AreSame(prompt, result.Value.Prompt!.Value.Dialogue);
            Assert.AreSame(requiredItem, result.Value.Prompt.Value.RequiredItem);
            Assert.AreSame(activationTransform, result.Value.Prompt.Value.ActivationTransform);
        }

        [Test]
        public void Resolve_requiredItemPresent_validPrompt_returnsRewardFields()
        {
            var collider     = new GameObject().AddComponent<BoxCollider>();
            var requiredItem = MakeItemData("small_key");
            var rewardItem   = MakeItemData("reward_item");
            var prompt       = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var rewardDialogue = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var hotspot = new ItemExamineHotspots.Hotspot
            {
                collider       = collider,
                dialogue       = new DialogueReference { nodeName = "locked_node" },
                requiredItem   = requiredItem,
                promptDialogue = prompt,
                rewardItem     = rewardItem,
                rewardDialogue = rewardDialogue,
            };
            var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

            var result = comp.Resolve(collider, new FakeInventoryService("small_key"));

            Assert.IsNotNull(result);
            Assert.IsNotNull(result!.Value.Prompt);
            Assert.AreSame(rewardItem, result.Value.Prompt!.Value.RewardItem);
            Assert.AreSame(rewardDialogue, result.Value.Prompt.Value.RewardDialogue);
        }

        [Test]
        public void Resolve_requiredItemAbsent_fallsBackToText()
        {
            var collider     = new GameObject().AddComponent<BoxCollider>();
            var requiredItem = MakeItemData("small_key");
            // Needs a real project so DialogueReference.IsValid is true for the fallback path.
            var lockedText   = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var hotspot = new ItemExamineHotspots.Hotspot
            {
                collider       = collider,
                dialogue       = lockedText,
                requiredItem   = requiredItem,
                // promptDialogue.IsValid is never checked here -- HasItem returns false first --
                // so its content doesn't matter for this test.
                promptDialogue = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" },
            };
            var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

            var result = comp.Resolve(collider, new FakeInventoryService()); // key not present

            Assert.IsNotNull(result);
            Assert.IsNull(result!.Value.Prompt);
            Assert.AreSame(lockedText, result.Value.Text);
        }

        [Test]
        public void Resolve_requiredItemPresent_invalidPromptDialogue_fallsBackToText()
        {
            var collider     = new GameObject().AddComponent<BoxCollider>();
            var requiredItem = MakeItemData("small_key");
            // Needs a real project so DialogueReference.IsValid is true for the fallback path.
            var lockedText   = new DialogueReference { project = MakeYarnProjectStandIn(), nodeName = "examine_placeholder" };
            var hotspot = new ItemExamineHotspots.Hotspot
            {
                collider       = collider,
                dialogue       = lockedText,
                requiredItem   = requiredItem,
                promptDialogue = new DialogueReference(), // unconfigured -- IsValid == false
            };
            var comp = MakeHotspots(new[] { hotspot }, new DialogueReference { nodeName = "default_node" });

            var result = comp.Resolve(collider, new FakeInventoryService("small_key"));

            Assert.IsNotNull(result);
            Assert.IsNull(result!.Value.Prompt);
            Assert.AreSame(lockedText, result.Value.Text);
        }
    }
}
