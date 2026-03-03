#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    public static class ScreenshotToolbar
    {
        private const string k_ElementPath = "CrimsonDraft/Screenshot";

        [MainToolbarElement(k_ElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        private static IEnumerable<MainToolbarElement> CreateScreenshotButton()
        {
            var icon = EditorGUIUtility.IconContent("d_FrameCapture").image as Texture2D;
            yield return new MainToolbarButton(
                new MainToolbarContent(icon, "Take Screenshot  (Ctrl+Shift+S)"),
                () => EditorApplication.delayCall += Capture);
        }

        [MenuItem("Tools/Screenshot Capture %#s")]
        public static void Capture()
        {
            var camera = Camera.main ?? Object.FindAnyObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("ScreenshotToolbar: no camera found in scene.");
                return;
            }

            var gameViewSize = GetGameViewSize();
            int w = (int)gameViewSize.x;
            int h = (int)gameViewSize.y;

            if (w <= 0 || h <= 0)
            {
                Debug.LogWarning($"ScreenshotToolbar: invalid Game View size {w}x{h}.");
                return;
            }

            Debug.Log($"ScreenshotToolbar: capturing {w}x{h} from {camera.name}...");

            var rt  = new RenderTexture(w, h, 24);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, mipChain: false);

            var prevTarget       = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            camera.targetTexture = prevTarget;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);

            var folder    = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(folder);

            var timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var path      = Path.Combine(folder, $"{timestamp}.png");

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Debug.Log($"Screenshot saved: {path}");
            EditorUtility.RevealInFinder(path);
        }

        private static Vector2 GetGameViewSize()
        {
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return new Vector2(Screen.width, Screen.height);

            var gameView = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gameView == null) return new Vector2(Screen.width, Screen.height);

            var prop = gameViewType.GetProperty(
                "targetSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop?.GetValue(gameView) is Vector2 size && size.x > 0)
                return size;

            return gameView.position.size;
        }
    }
}
