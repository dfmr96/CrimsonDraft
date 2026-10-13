#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Tests
{
    public static class InventoryTestData
    {
        public static WeaponData Weapon(Caliber caliber = Caliber._9mm, int magazine = 6, string? id = null)
        {
            var data = Create<WeaponData>(id, ItemType.Weapon);
            var so   = new SerializedObject(data);
            so.FindProperty("caliber").enumValueIndex    = (int)caliber;
            so.FindProperty("magazineCapacity").intValue = magazine;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        public static AmmoBoxData Ammo(Caliber caliber = Caliber._9mm, int defaultQuantity = 30, int maxStack = 999, string? id = null)
        {
            var data = Create<AmmoBoxData>(id, ItemType.AmmoBox);
            var so   = new SerializedObject(data);
            so.FindProperty("caliber").enumValueIndex   = (int)caliber;
            so.FindProperty("defaultQuantity").intValue = defaultQuantity;
            so.FindProperty("maxStack").intValue        = maxStack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        public static ConsumableData Consumable(int heal = 20, bool stackable = false, int maxStack = 999, string? id = null)
        {
            var data = Create<ConsumableData>(id, ItemType.Consumable);
            var so   = new SerializedObject(data);
            so.FindProperty("healAmount").intValue = heal;
            so.FindProperty("stackable").boolValue = stackable;
            so.FindProperty("maxStack").intValue   = maxStack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        public static KeyItemData Key(int maxUses = 1, string? id = null)
        {
            var data = Create<KeyItemData>(id, ItemType.KeyItem);
            var so   = new SerializedObject(data);
            so.FindProperty("maxUses").intValue = maxUses;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        public static T Sized<T>(T data, int width, int height) where T : ItemData
        {
            var so = new SerializedObject(data);
            so.FindProperty("gridSize").vector2IntValue = new Vector2Int(width, height);
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        public static ItemDatabase Database(params ItemData[] items)
        {
            var db       = ScriptableObject.CreateInstance<ItemDatabase>();
            var so       = new SerializedObject(db);
            var allItems = so.FindProperty("allItems");
            allItems.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++)
                allItems.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return db;
        }

        public static OperatorRuntime Alive(int slot) =>
            new OperatorRuntime(slot, null, isPresent: true, maxHp: 100);

        public static OperatorRuntime Dead(int slot)
        {
            var op = Alive(slot);
            op.ApplyDamage(op.MaxHp);
            op.ApplyDamage(1);
            return op;
        }

        private static T Create<T>(string? id, ItemType type) where T : ItemData
        {
            var data = ScriptableObject.CreateInstance<T>();
            var so   = new SerializedObject(data);
            so.FindProperty("itemId").stringValue      = id ?? System.Guid.NewGuid().ToString();
            so.FindProperty("itemType").enumValueIndex = (int)type;
            so.FindProperty("displayName").stringValue = typeof(T).Name;
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }
}
