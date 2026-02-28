#nullable enable

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Infrastructure;
using CrimsonDraft.Combat;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Editor
{
    public static class SceneSetupTool
    {
        private const string ScenesFolder = "Assets/Scenes";

        [MenuItem("CrimsonDraft/Scenes/Create All Scenes", priority = 0)]
        public static void CreateAllScenes()
        {
            SaveCurrentSceneIfDirty();
            CreateBootScene();
            CreateNavigationScene();
            CreateCombatScene();
            UpdateBuildSettings();
            EditorUtility.DisplayDialog(
                "CrimsonDraft",
                "Scenes created and Build Settings updated.\n\nRemember to set parent scopes in the Inspector:\n• NavigationScope → parent: GameLifetimeScope\n• CombatScope → parent: NavigationScope",
                "OK");
        }

        [MenuItem("CrimsonDraft/Scenes/Create Boot Scene", priority = 11)]
        public static void CreateBootScene() =>
            CreateSceneWithScope<GameLifetimeScope>("Boot", "GameLifetimeScope");

        [MenuItem("CrimsonDraft/Scenes/Create Navigation Scene", priority = 12)]
        public static void CreateNavigationScene() =>
            CreateSceneWithScope<NavigationScope>("Navigation", "NavigationScope");

        [MenuItem("CrimsonDraft/Scenes/Create Combat Scene", priority = 13)]
        public static void CreateCombatScene() =>
            CreateSceneWithScope<CombatScope>("Combat", "CombatScope");

        [MenuItem("CrimsonDraft/Scenes/Update Build Settings", priority = 21)]
        public static void UpdateBuildSettings()
        {
            var paths = new[]
            {
                $"{ScenesFolder}/Boot.unity",
                $"{ScenesFolder}/Navigation.unity",
                $"{ScenesFolder}/Combat.unity",
            };

            EditorBuildSettings.scenes = paths
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            Debug.Log("[SceneSetupTool] Build Settings updated: Boot → Navigation → Combat");
        }

        private static void SaveCurrentSceneIfDirty()
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty)
                EditorSceneManager.SaveScene(active);
        }

        private static void CreateSceneWithScope<TScope>(string sceneName, string gameObjectName)
            where TScope : VContainer.Unity.LifetimeScope
        {
            using var _ = new SceneCreationContext(sceneName);

            var go = new GameObject(gameObjectName);
            SceneManager.MoveGameObjectToScene(go, _.Scene);
            go.AddComponent<TScope>();

            var path = $"{ScenesFolder}/{sceneName}.unity";
            EditorSceneManager.SaveScene(_.Scene, path);
            Debug.Log($"[SceneSetupTool] Created {path}");
        }

        private readonly struct SceneCreationContext : System.IDisposable
        {
            public Scene Scene { get; }

            public SceneCreationContext(string sceneName)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = sceneName;
                Scene = scene;
            }

            public void Dispose() => EditorSceneManager.CloseScene(Scene, true);
        }
    }
}
