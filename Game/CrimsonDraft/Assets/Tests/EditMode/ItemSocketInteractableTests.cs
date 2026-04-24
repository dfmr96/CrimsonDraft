#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Navigation.Interactables;

namespace CrimsonDraft.Tests
{
    public sealed class ItemSocketInteractableTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static SocketItemData MakeSocketItemData(string id)
        {
            var d  = ScriptableObject.CreateInstance<SocketItemData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.SocketItem;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ConsumableData MakeConsumableData(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new UnityEditor.SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemSocketInteractable MakeSocket(params SocketItemData[] required)
        {
            var go     = new GameObject();
            var socket = go.AddComponent<ItemSocketInteractable>();
            var so     = new UnityEditor.SerializedObject(socket);
            var arr    = so.FindProperty("requiredItems");
            arr.arraySize = required.Length;
            for (int i = 0; i < required.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = required[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return socket;
        }

        // ── TryInsert ─────────────────────────────────────────────────────────

        [Test]
        public void TryInsert_returnsTrue_whenItemIdMatches()
        {
            var data   = MakeSocketItemData("panel-a");
            var socket = MakeSocket(data);

            bool result = socket.TryInsert(data, poi: null);

            Assert.IsTrue(result);
        }

        [Test]
        public void TryInsert_returnsFalse_whenItemIdDoesNotMatch()
        {
            var required = MakeSocketItemData("panel-a");
            var wrong    = MakeSocketItemData("panel-b");
            var socket   = MakeSocket(required);

            bool result = socket.TryInsert(wrong, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_returnsFalse_forNonSocketItemData()
        {
            var socketData = MakeSocketItemData("panel-a");
            var consumable = MakeConsumableData("consumable-x");
            var socket     = MakeSocket(socketData);

            bool result = socket.TryInsert(consumable, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_returnsFalse_whenAlreadyActivated()
        {
            var data   = MakeSocketItemData("panel-a");
            var socket = MakeSocket(data);
            socket.TryInsert(data, poi: null); // activates (single-slot socket)

            bool result = socket.TryInsert(data, poi: null);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryInsert_activatesSocket_whenAllSlotsSatisfied()
        {
            var dataA  = MakeSocketItemData("panel-a");
            var dataB  = MakeSocketItemData("panel-b");
            var socket = MakeSocket(dataA, dataB);

            socket.TryInsert(dataA, poi: null);
            Assert.IsFalse(socket.IsActivated, "still one slot remaining");

            socket.TryInsert(dataB, poi: null);
            Assert.IsTrue(socket.IsActivated, "all slots satisfied");
        }

        [Test]
        public void TryInsert_canSatisfySameItemTwice_whenRequiredTwice()
        {
            var data   = MakeSocketItemData("battery");
            var socket = MakeSocket(data, data);

            bool first  = socket.TryInsert(data, poi: null);
            bool second = socket.TryInsert(data, poi: null);

            Assert.IsTrue(first,  "first insert accepted");
            Assert.IsTrue(second, "second insert accepted");
            Assert.IsTrue(socket.IsActivated);
        }

        [Test]
        public void TryInsert_doesNotSatisfyAlreadySatisfiedSlot_whenInsertingAgain()
        {
            var dataA  = MakeSocketItemData("panel-a");
            var dataB  = MakeSocketItemData("panel-b");
            var socket = MakeSocket(dataA, dataB);

            socket.TryInsert(dataA, poi: null);            // satisfies slot 0
            bool duplicate = socket.TryInsert(dataA, poi: null); // no unsatisfied slot for panel-a

            Assert.IsFalse(duplicate, "panel-a slot already satisfied, no second slot for it");
            Assert.IsFalse(socket.IsActivated, "panel-b still missing");
        }
    }
}
