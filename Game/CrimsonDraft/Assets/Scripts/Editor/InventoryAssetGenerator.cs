#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    public static class InventoryAssetGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/Inventory";

        [MenuItem("CrimsonDraft/Generate Inventory Assets")]
        public static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Inventory");

            CreateWeapon("Mk18",       "mk18",       "Mk18 (5.56)",       "5.56", 30);
            CreateWeapon("Benelli_M4", "benelli_m4", "Benelli M4 (12ga)", "12ga", 8);
            CreateWeapon("P229",       "p229",       "P229 (9mm)",        "9mm",  15);
            CreateWeapon("MP5",        "mp5",        "MP5 (9mm)",         "9mm",  30);
            CreateAmmoBox("9mm_Box",   "9mm_box",    "9mm Box",           "9mm",  30);
            CreateAmmoBox("556_Box",   "556_box",    "5.56 Box",          "5.56", 30);
            CreateAmmoBox("12ga_Box",  "12ga_box",   "12ga Box",          "12ga", 30);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[InventoryAssetGenerator] Done. Assets at {OutputPath}");
        }

        private static void CreateWeapon(
            string fileName,
            string itemId,
            string displayName,
            string caliber,
            int magazineCapacity)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            EnsureCorrectType<WeaponData>(path);
            if (AssetDatabase.LoadAssetAtPath<WeaponData>(path) != null)
            {
                Debug.Log($"[InventoryAssetGenerator] Skipped (exists): {path}");
                return;
            }

            var data = ScriptableObject.CreateInstance<WeaponData>();
            var so = new SerializedObject(data);

            so.FindProperty("itemId").stringValue        = itemId;
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.Weapon;
            so.FindProperty("displayName").stringValue   = displayName;
            so.FindProperty("caliber").stringValue       = caliber;
            so.FindProperty("magazineCapacity").intValue = magazineCapacity;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[InventoryAssetGenerator] Created: {path}");
        }

        private static void CreateAmmoBox(
            string fileName,
            string itemId,
            string displayName,
            string caliber,
            int defaultQuantity)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            EnsureCorrectType<AmmoBoxData>(path);
            if (AssetDatabase.LoadAssetAtPath<AmmoBoxData>(path) != null)
            {
                Debug.Log($"[InventoryAssetGenerator] Skipped (already exists): {path}");
                return;
            }

            var data = ScriptableObject.CreateInstance<AmmoBoxData>();
            var so = new SerializedObject(data);

            so.FindProperty("itemId").stringValue        = itemId;
            so.FindProperty("itemType").enumValueIndex   = (int)ItemType.AmmoBox;
            so.FindProperty("displayName").stringValue   = displayName;
            so.FindProperty("caliber").stringValue       = caliber;
            so.FindProperty("defaultQuantity").intValue  = defaultQuantity;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[InventoryAssetGenerator] Created: {path}");
        }

        private static void EnsureCorrectType<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null && existing is not T)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[InventoryAssetGenerator] Deleted wrong-type asset: {path}");
            }
        }
    }
}
