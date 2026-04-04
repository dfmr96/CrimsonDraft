using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HorrorEngine
{
    public class HorrorEngineScripts
    {
        [MenuItem("Horror Engine/Scripts/Migrate Scene Unlocalized Data")]
        public static void MigrateSceneUnlocalizedData()
        {
            // Find all Choice components in the scene
            Choice[] choices = UnityEngine.Object.FindObjectsByType<Choice>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var choice in choices)
            {
                choice.EditorOnly_MigrateUnlocalizedData();
            }

            ItemContainerBase[] containers = UnityEngine.Object.FindObjectsByType<ItemContainerBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var container in containers)
            {
                container.EditorOnly_MigrateUnlocalizedData();
            }

            SaveStation[] saveStations = UnityEngine.Object.FindObjectsByType<SaveStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var saveStation in saveStations)
            {
                saveStation.EditorOnly_MigrateUnlocalizedData();
            }

            PointOfInterest[] pois = UnityEngine.Object.FindObjectsByType<PointOfInterest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var poi in pois)
            {
                poi.EditorOnly_MigrateUnlocalizedData();
            }
        }
        


        [MenuItem("Horror Engine/Scripts/Migrate Assets Unlocalized Data")]
        public static void MigrateAssetsUnlocalizedData()
        {
            MigrateUnlocalizedItemData();
            MigrateUnlocalizedMapData();
            MigrateUnlocalizedInteractionData();
            MigrateUnlocalizedDocumentData();
            MigrateUnlocalizedStatusData();
            MigrateUnlocalizedCharacterData();

            // Save changes to the AssetDatabase
            AssetDatabase.SaveAssets();
        }

        
        public static void MigrateUnlocalizedItemData()
        {
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:ItemData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No ItemData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                ItemData itemData = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (itemData == null)
                {
                    Debug.LogError($"Failed to load ItemData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(itemData, "Migrate ItemData");

                itemData.EditorOnly_MigrateUnlocalizedData();

            }
        }

        public static void MigrateUnlocalizedMapData()
        {
            // Find all MapData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:MapData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No MapData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                MapData MapData = AssetDatabase.LoadAssetAtPath<MapData>(path);
                if (MapData == null)
                {
                    Debug.LogError($"Failed to load MapData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(MapData, "Migrate MapData");

                MapData.EditorOnly_MigrateUnlocalizedData();

            }
        }

        private static void MigrateUnlocalizedInteractionData()
        {
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:InteractionData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No InteractionData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                InteractionData interactionData = AssetDatabase.LoadAssetAtPath<InteractionData>(path);
                if (interactionData == null)
                {
                    Debug.LogError($"Failed to load InteractionData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(interactionData, "Migrate InteractionData");

                interactionData.EditorOnly_MigrateUnlocalizedData();

            }
        }

        private static void MigrateUnlocalizedStatusData()
        {
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:StatusData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No StatusData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                StatusData statusData = AssetDatabase.LoadAssetAtPath<StatusData>(path);
                if (statusData == null)
                {
                    Debug.LogError($"Failed to load StatusData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(statusData, "Migrate StatusData");

                statusData.EditorOnly_MigrateUnlocalizedData();

            }
        }

        private static void MigrateUnlocalizedDocumentData()
        {
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:DocumentData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No DocumentData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                DocumentData documentData = AssetDatabase.LoadAssetAtPath<DocumentData>(path);
                if (documentData == null)
                {
                    Debug.LogError($"Failed to load DocumentData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(documentData, "Migrate DocumentData");

                documentData.EditorOnly_MigrateUnlocalizedData();

            }
        }

        private static void MigrateUnlocalizedCharacterData()
        {
            // Find all ItemData assets in the project
            string[] guids = AssetDatabase.FindAssets("t:CharacterData");
            if (guids.Length == 0)
            {
                Debug.LogWarning("No CharacterData assets found in the project.");
                return;
            }

            foreach (string guid in guids)
            {
                // Get the path of the asset
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Load the asset
                CharacterData characterData = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (characterData == null)
                {
                    Debug.LogError($"Failed to load CharacterData at path: {path}");
                    continue;
                }

                // Record changes for undo functionality
                Undo.RecordObject(characterData, "Migrate CharacterData");

                characterData.EditorOnly_MigrateUnlocalizedData();

            }
        }
    }
}