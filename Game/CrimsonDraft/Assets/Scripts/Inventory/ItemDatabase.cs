#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

namespace CrimsonDraft.Inventory
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "CrimsonDraft/Inventory/Item Database")]
    public sealed class ItemDatabase : ScriptableObject
    {
        [SerializeField] private ItemData[] allItems = Array.Empty<ItemData>();

        private Dictionary<string, ItemData>? lookup;

        public bool TryGetById(string itemId, out ItemData item)
        {
            this.lookup ??= BuildLookup();
            return this.lookup.TryGetValue(itemId, out item!);
        }

        private Dictionary<string, ItemData> BuildLookup()
        {
            var dict = new Dictionary<string, ItemData>();
            foreach (var data in this.allItems)
            {
                if (data == null || string.IsNullOrEmpty(data.ItemId)) continue;
                dict[data.ItemId] = data;
            }
            return dict;
        }

#if UNITY_EDITOR
        [Button("Populate From Project")]
        private void PopulateFromProject()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData");
            var items = new List<ItemData>();
            foreach (var guid in guids)
            {
                var path  = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (asset != null)
                    items.Add(asset);
            }
            this.allItems = items.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
