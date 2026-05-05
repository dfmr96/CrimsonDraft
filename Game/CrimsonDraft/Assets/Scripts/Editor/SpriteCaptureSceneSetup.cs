#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Editor
{
    public static class SpriteCaptureSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tools/SpriteCaptureScene.unity";

        [MenuItem("CrimsonDraft/Scenes/Create Sprite Capture Scene", priority = 14)]
        public static void CreateSpriteCaptureScene()
        {
            using var ctx = new SceneCreationContext("SpriteCaptureScene");

            // ── Camera ───────────────────────────────────────────────────────────
            var camGO = new GameObject("CaptureCamera");
            SceneManager.MoveGameObjectToScene(camGO, ctx.Scene);
            var cam                = camGO.AddComponent<Camera>();
            cam.clearFlags         = CameraClearFlags.SolidColor;
            cam.backgroundColor    = Color.clear;
            cam.orthographic       = false;
            cam.fieldOfView        = 40f;
            cam.nearClipPlane      = 0.01f;
            cam.farClipPlane       = 100f;
            cam.tag                = "MainCamera";
            camGO.transform.position = new Vector3(0f, 0.5f, -3f);
            camGO.transform.LookAt(Vector3.zero);

            // ── ItemSpriteCapture component ───────────────────────────────────────
            var capture = camGO.AddComponent<ItemSpriteCapture>();
            var so      = new SerializedObject(capture);
            so.FindProperty("captureCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ── Directional Light ─────────────────────────────────────────────────
            var lightGO  = new GameObject("DirectionalLight");
            SceneManager.MoveGameObjectToScene(lightGO, ctx.Scene);
            var light    = lightGO.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.color     = Color.white;
            light.intensity = 1.2f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── Save ─────────────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(ctx.Scene, ScenePath);
            Debug.Log($"[SpriteCaptureSceneSetup] Created {ScenePath}");
        }

        private readonly struct SceneCreationContext : System.IDisposable
        {
            public Scene Scene { get; }

            public SceneCreationContext(string sceneName)
            {
                var scene  = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                scene.name = sceneName;
                Scene      = scene;
            }

            public void Dispose() => EditorSceneManager.CloseScene(Scene, true);
        }
    }
}
#endif
