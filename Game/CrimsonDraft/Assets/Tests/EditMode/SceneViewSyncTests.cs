#nullable enable

using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Editor;

namespace CrimsonDraft.Tests
{
    public sealed class SceneViewSyncTests
    {
        [Test]
        public void LookThrough_putsTheSceneViewCameraAtTheZoneCameraPoseAndFov()
        {
            var sceneView = SceneView.lastActiveSceneView;
            Assume.That(sceneView != null, "needs an open Scene view");

            var pivot = sceneView!.pivot;
            var rotation = sceneView.rotation;
            var size = sceneView.size;
            var ortho = sceneView.orthographic;
            var fov = sceneView.cameraSettings.fieldOfView;
            var go = new GameObject("Zone camera");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(3f, 4f, -7f), Quaternion.Euler(35f, 120f, 0f));
                var camera = go.AddComponent<CinemachineCamera>();
                var lens = camera.Lens;
                lens.FieldOfView = 47f;
                camera.Lens = lens;

                SceneViewSync.LookThrough(sceneView, camera);

                // The Scene view moves its own camera on the next repaint; where it will be is pivot - forward * distance.
                var eye = sceneView.pivot - sceneView.rotation * Vector3.forward * sceneView.cameraDistance;
                Assert.That(Vector3.Distance(eye, go.transform.position), Is.LessThan(0.01f));
                Assert.That(Quaternion.Angle(sceneView.rotation, go.transform.rotation), Is.LessThan(0.1f));
                Assert.That(sceneView.cameraSettings.fieldOfView, Is.EqualTo(47f).Within(0.01f));
                Assert.IsFalse(sceneView.orthographic);
            }
            finally
            {
                Object.DestroyImmediate(go);
                sceneView.cameraSettings.fieldOfView = fov;
                sceneView.orthographic = ortho;
                sceneView.LookAtDirect(pivot, rotation, size);
            }
        }
    }
}
