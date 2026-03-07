#nullable enable

using UnityEditor;
using UnityEngine;
using CrimsonDraft.Operators;

namespace CrimsonDraft.Editor
{
    public static class OperatorAssetGenerator
    {
        private const string OutputPath = "Assets/ScriptableObjects/Operators";

        [MenuItem("CrimsonDraft/Generate Operator Assets")]
        public static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(OutputPath))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Operators");

            CreateOperator("Operator_0", "op_0", "BRAVO-1");
            CreateOperator("Operator_1", "op_1", "BRAVO-2");
            CreateOperator("Operator_2", "op_2", "BRAVO-3");
            CreateOperator("Operator_3", "op_3", "BRAVO-4");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[OperatorAssetGenerator] Done. Assets at {OutputPath}");
        }

        private static void CreateOperator(string fileName, string operatorId, string displayName)
        {
            string path = $"{OutputPath}/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<OperatorData>(path) != null)
            {
                Debug.Log($"[OperatorAssetGenerator] Skipped (exists): {path}");
                return;
            }

            var data = ScriptableObject.CreateInstance<OperatorData>();
            var so = new SerializedObject(data);
            so.FindProperty("operatorId").stringValue = operatorId;
            so.FindProperty("displayName").stringValue = displayName;
            so.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[OperatorAssetGenerator] Created: {path}");
        }
    }
}
