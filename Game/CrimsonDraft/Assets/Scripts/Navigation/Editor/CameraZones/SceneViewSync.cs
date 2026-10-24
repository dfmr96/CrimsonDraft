#nullable enable

using Unity.Cinemachine;
using UnityEditor;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Puts the Scene view exactly where a zone camera is, with its field of view, so the camera
    /// can be seen with the Scene view's own shading (e.g. scene lighting off). The tool's own render can
    /// not reproduce that mode: it only exists inside the Scene view's render.</summary>
    public static class SceneViewSync
    {
        public static void LookThrough(SceneView sceneView, CinemachineCamera camera)
        {
            sceneView.orthographic = false;
            sceneView.cameraSettings.fieldOfView = camera.Lens.FieldOfView;

            // The Scene view orbits a pivot: its eye sits cameraDistance behind it. Instant (no animated
            // align): set the size first to know that distance, then put the pivot that far ahead of the camera.
            var t = camera.transform;
            sceneView.LookAtDirect(t.position, t.rotation, 1f);
            sceneView.LookAtDirect(t.position + t.forward * sceneView.cameraDistance, t.rotation, 1f);
            sceneView.Repaint();
        }
    }
}
