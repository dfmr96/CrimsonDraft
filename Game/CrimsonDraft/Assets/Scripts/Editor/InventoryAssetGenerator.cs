#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    public static class InventoryAssetGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/Inventory";

        [MenuItem("CrimsonDraft/Generate Inventory Test Assets")]
        public static void GenerateTestAssets()
        {
            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Inventory");

            CreateItem("Mk18",       "mk18",      ItemType.Weapon,    "Mk18 (5.56)",      "5.56");
            CreateItem("Benelli_M4", "benelli_m4",ItemType.Weapon,    "Benelli M4 (12ga)","12ga");
            CreateItem("P229",       "p229",      ItemType.Weapon,    "P229 (9mm)",        "9mm");
            CreateItem("MP5",        "mp5",       ItemType.Weapon,    "MP5 (9mm)",         "9mm");
            CreateItem("9mm_Box",    "9mm_box",   ItemType.AmmoBox,   "9mm Box",           "9mm");
            CreateItem("556_Box",    "556_box",   ItemType.AmmoBox,   "5.56 Box",          "5.56");
            CreateItem("12ga_Box",   "12ga_box",  ItemType.AmmoBox,   "12ga Box",          "12ga");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InventoryAssetGenerator] Generated assets at {OutputPath}");
        }

        private static void CreateItem(
            string fileName,
            string itemId,
            ItemType type,
            string displayName,
            string caliber)
        {
            string path = $"{OutputPath}/{fileName}.asset";

            // Skip if already exists
            if (AssetDatabase.LoadAssetAtPath<ItemData>(path) != null)
            {
                Debug.Log($"[InventoryAssetGenerator] Skipped (already exists): {path}");
                return;
            }

            var data = ScriptableObject.CreateInstance<ItemData>();
            var so   = new SerializedObject(data);

            so.FindProperty("itemId").stringValue       = itemId;
            so.FindProperty("itemType").enumValueIndex  = (int)type;
            so.FindProperty("displayName").stringValue  = displayName;
            so.FindProperty("caliber").stringValue      = caliber;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[InventoryAssetGenerator] Created: {path}");
        }
    }
}
