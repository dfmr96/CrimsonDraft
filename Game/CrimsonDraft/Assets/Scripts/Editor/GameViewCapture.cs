#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Saves a PNG of the Game view (UI and post-processing included) at its current resolution.
    /// Captures land in &lt;project&gt;/Screenshots (git-ignored, outside Assets). Best used in Play mode:
    /// the file is written at the end of the next frame.
    /// </summary>
    public static class GameViewCapture
    {
        // %#k = Ctrl+Shift+K (Cmd+Shift+K on macOS)
        private const string MenuPath = "CrimsonDraft/Tools/Capture Game View %#k";
        private const string Folder   = "Screenshots";

        [MenuItem(MenuPath)]
        public static void Capture()
        {
            var folder = Path.GetFullPath(Folder);
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            ScreenCapture.CaptureScreenshot(path, 1);
            if (!Application.isPlaying)
                EditorApplication.QueuePlayerLoopUpdate();

            Debug.Log($"[GameViewCapture] {path}");
        }
    }
}
