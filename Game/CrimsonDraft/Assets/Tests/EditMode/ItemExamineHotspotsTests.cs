#nullable enable

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
    }
}
