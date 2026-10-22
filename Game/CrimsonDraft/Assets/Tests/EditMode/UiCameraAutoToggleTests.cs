#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Infrastructure.UI;

namespace CrimsonDraft.Tests
{
    // UiCameraAutoToggle's per-frame on/off is a decision over live UGUI objects, so these tests
    // build real Canvases/Images (Graphic is ExecuteAlways, so it registers with GraphicRegistry
    // in EditMode too) and check the pure HasVisibleContent() it delegates to.
    public sealed class UiCameraAutoToggleTests
    {
        private readonly List<GameObject> spawned = new();
        private Camera uiCamera = null!;

        [SetUp]
        public void SetUp()
        {
            this.uiCamera = Spawn("UiCamera").AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in this.spawned)
                if (go != null) Object.DestroyImmediate(go);
            this.spawned.Clear();
            UiCameraAutoToggle.GatingEnabled = true;
        }

        [Test]
        public void GatingEnabled_flip_raisesGatingChangedOnce()
        {
            int raised = 0;
            void OnChanged() => raised++;
            UiCameraAutoToggle.GatingChanged += OnChanged;
            try
            {
                UiCameraAutoToggle.GatingEnabled = false;
                UiCameraAutoToggle.GatingEnabled = false;
            }
            finally { UiCameraAutoToggle.GatingChanged -= OnChanged; }

            Assert.AreEqual(1, raised);
        }

        [Test]
        public void NoCanvases_isNotVisible()
        {
            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas>()));
        }

        [Test]
        public void CanvasBoundToCamera_withVisibleImage_isVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            AddImage(canvas.transform);

            Assert.IsTrue(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void CanvasBoundToAnotherCamera_isNotVisible()
        {
            var other  = Spawn("OtherCamera").AddComponent<Camera>();
            var canvas = CanvasFor(other);
            AddImage(canvas.transform);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void OverlayCanvas_isNotVisible_evenIfItsCameraFieldPointsHere()
        {
            var canvas = CanvasFor(this.uiCamera);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            AddImage(canvas.transform);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void InactivePanel_isNotVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            var panel  = Spawn("Panel", canvas.transform);
            AddImage(panel.transform);
            panel.SetActive(false);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void InactiveCanvas_isNotVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            AddImage(canvas.transform);
            canvas.gameObject.SetActive(false);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void FullyTransparentImage_isNotVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            AddImage(canvas.transform).color = new Color(1f, 1f, 1f, 0f);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void ImageUnderZeroAlphaCanvasGroup_isNotVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            var panel  = Spawn("Panel", canvas.transform);
            panel.AddComponent<CanvasGroup>().alpha = 0f;
            AddImage(panel.transform);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void ImageUnderPartialAlphaCanvasGroup_isVisible()
        {
            var canvas = CanvasFor(this.uiCamera);
            var panel  = Spawn("Panel", canvas.transform);
            panel.AddComponent<CanvasGroup>().alpha = 0.2f;
            AddImage(panel.transform);

            Assert.IsTrue(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { canvas }));
        }

        [Test]
        public void ImageInNestedCanvas_underRootBoundToCamera_isVisible()
        {
            var root   = CanvasFor(this.uiCamera);
            var nested = Spawn("Nested", root.transform).AddComponent<Canvas>();
            AddImage(nested.transform);

            Assert.IsTrue(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { root, nested }));
        }

        [Test]
        public void DestroyedCanvasInList_isSkipped()
        {
            var gone = CanvasFor(this.uiCamera);
            Object.DestroyImmediate(gone.gameObject);

            Assert.IsFalse(UiCameraAutoToggle.HasVisibleContent(this.uiCamera, new List<Canvas> { gone }));
        }

        private Canvas CanvasFor(Camera camera)
        {
            var canvas = Spawn("Canvas").AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            return canvas;
        }

        private Image AddImage(Transform parent) => Spawn("Image", parent).AddComponent<Image>();

        private GameObject Spawn(string name, Transform? parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            else this.spawned.Add(go);
            return go;
        }
    }
}
