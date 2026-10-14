#nullable enable

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    // Inventory save/scene changes store item ids and resolve them back through the ItemDatabase;
    // an item missing from it is dropped on the next room/scene change or loaded save.
    public sealed class ItemDatabaseCoverageTests
    {
        private const string DatabasePath = "Assets/Data/ItemDatabase.asset";

        [Test]
        public void EveryItemDataAsset_isRegisteredInTheItemDatabase()
        {
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(DatabasePath);
            Assert.IsNotNull(database, $"No ItemDatabase at {DatabasePath}");

            var allItems   = new SerializedObject(database).FindProperty("allItems");
            var registered = new HashSet<ItemData>();
            for (int i = 0; i < allItems.arraySize; i++)
                if (allItems.GetArrayElementAtIndex(i).objectReferenceValue is ItemData data)
                    registered.Add(data);

            var missing = AssetDatabase.FindAssets("t:ItemData")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !registered.Contains(AssetDatabase.LoadAssetAtPath<ItemData>(path)))
                .ToList();

            Assert.IsEmpty(missing,
                "Not in the ItemDatabase (use its 'Populate From Project' button):\n" + string.Join("\n", missing));
        }
    }
}
