#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class ItemDatabaseTests
    {
        private static ConsumableData MakeConsumableData(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = "Test Consumable";
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static ItemDatabase MakeDatabase(params ItemData[] items)
        {
            var db = ScriptableObject.CreateInstance<ItemDatabase>();
            var so = new SerializedObject(db);
            var arr = so.FindProperty("allItems");
            arr.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        [Test]
        public void TryGetById_returnsTrueAndItem_whenIdExists()
        {
            var item = MakeConsumableData("herb-green");
            var db   = MakeDatabase(item);

            bool found = db.TryGetById("herb-green", out var result);

            Assert.IsTrue(found);
            Assert.AreEqual(item, result);
        }

        [Test]
        public void TryGetById_returnsFalse_whenIdMissing()
        {
            var db = MakeDatabase(MakeConsumableData("herb-green"));

            bool found = db.TryGetById("missing", out _);

            Assert.IsFalse(found);
        }
    }
}
