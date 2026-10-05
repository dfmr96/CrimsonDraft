#nullable enable

using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CrimsonDraft.Infrastructure.Map;
using CrimsonDraft.Navigation.UI;

namespace CrimsonDraft.Tests
{
    public sealed class MapScreenViewTests
    {
        private GameObject host = null!;
        private RectTransform content = null!;
        private GameObject up = null!, down = null!;
        private TextMeshProUGUI label = null!;
        private MapScreenView view = null!;

        [SetUp]
        public void SetUp()
        {
            this.host = new GameObject("Host", typeof(RectTransform));
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(this.host.transform, false);

            var viewport = new GameObject("Viewport", typeof(RectTransform)).GetComponent<RectTransform>();
            viewport.SetParent(root.transform, false);
            viewport.sizeDelta = new Vector2(100f, 100f);

            this.content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            this.content.SetParent(viewport, false);

            this.label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            this.label.transform.SetParent(root.transform, false);
            this.up   = new GameObject("Up");
            this.down = new GameObject("Down");
            this.up.transform.SetParent(root.transform, false);
            this.down.transform.SetParent(root.transform, false);

            this.view = this.host.AddComponent<MapScreenView>();
            var so = new SerializedObject(this.view);
            so.FindProperty("root").objectReferenceValue      = root;
            so.FindProperty("viewport").objectReferenceValue  = viewport;
            so.FindProperty("content").objectReferenceValue   = this.content;
            so.FindProperty("deckName").objectReferenceValue  = this.label;
            so.FindProperty("upArrow").objectReferenceValue   = this.up;
            so.FindProperty("downArrow").objectReferenceValue = this.down;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.host);

        private static MapRoomVisual Visual(int x, int y, int turns = 0, bool current = false)
            => new("r", MapTestData.Sprite(10, 10), new Vector2Int(x, y), turns, current);

        private RectTransform Room(int i) => (RectTransform)this.content.GetChild(i);

        [Test]
        public void Show_placesOneImagePerVisual_relativeToBoundsCentre()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0) }, "DECK B", false, false);

            Assert.AreEqual(2, this.content.childCount);
            Assert.AreEqual(new Vector2(-10f, 0f), Room(0).anchoredPosition);
            Assert.AreEqual(new Vector2(10f, 0f), Room(1).anchoredPosition);
            Assert.AreEqual(new Vector2(10f, 10f), Room(0).sizeDelta);
            Assert.IsNotNull(Room(0).GetComponent<Image>().sprite);
            Assert.IsTrue(this.view.IsVisible);
        }

        [Test]
        public void Show_oddSizedRoom_keepsEdgesOnWholePixels()
        {
            this.view.Show(new[] { new MapRoomVisual("r", MapTestData.Sprite(11, 9), new Vector2Int(0, 0), 0, false) }, "DECK B", false, false);
            var left   = Room(0).anchoredPosition.x - Room(0).sizeDelta.x * 0.5f;
            var bottom = Room(0).anchoredPosition.y - Room(0).sizeDelta.y * 0.5f;
            Assert.AreEqual(Mathf.Round(left), left, 0.0001f);
            Assert.AreEqual(Mathf.Round(bottom), bottom, 0.0001f);
        }

        [Test]
        public void Show_again_hidesSurplusImages()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0) }, "DECK B", false, false);
            this.view.Show(new[] { Visual(0, 0) }, "DECK B", false, false);

            Assert.IsTrue(Room(0).gameObject.activeSelf);
            Assert.IsFalse(Room(1).gameObject.activeSelf);
        }

        [Test]
        public void Show_rotatesByQuarterTurns()
        {
            this.view.Show(new[] { Visual(0, 0, turns: 1) }, "DECK B", false, false);
            Assert.AreEqual(90f, Room(0).localEulerAngles.z, 0.01f);
        }

        [Test]
        public void Show_setsHeaderAndArrows()
        {
            this.view.Show(new[] { Visual(0, 0) }, "DECK C", hasUp: true, hasDown: false);
            Assert.AreEqual("DECK C", this.label.text);
            Assert.IsTrue(this.up.activeSelf);
            Assert.IsFalse(this.down.activeSelf);
        }

        [Test]
        public void Show_nonCurrentRooms_areWhite()
        {
            this.view.Show(new[] { Visual(0, 0), Visual(20, 0, current: true) }, "DECK B", false, false);
            Assert.AreEqual(Color.white, Room(0).GetComponent<Image>().color);
        }

        [Test]
        public void Hide_deactivatesRoot()
        {
            this.view.Show(new[] { Visual(0, 0) }, "DECK B", false, false);
            this.view.Hide();
            Assert.IsFalse(this.view.IsVisible);
        }

        [Test]
        public void PulseColor_onlyChangesAlpha_betweenMinAndOne()
        {
            float speed = 3f;
            var peak   = MapScreenView.PulseColor(Mathf.PI * 0.5f / speed, speed, 0.3f);
            var trough = MapScreenView.PulseColor(Mathf.PI * 1.5f / speed, speed, 0.3f);

            Assert.AreEqual(new Color(1f, 1f, 1f, 1f),   peak);
            Assert.AreEqual(1f, trough.r);
            Assert.AreEqual(1f, trough.g);
            Assert.AreEqual(1f, trough.b);
            Assert.AreEqual(0.3f, trough.a, 0.0001f);
        }

        [Test]
        public void FitScale_onlyShrinks()
        {
            Assert.AreEqual(0.5f, MapScreenView.FitScale(new Vector2(200f, 100f), new Vector2(100f, 100f)), 0.0001f);
            Assert.AreEqual(1f,   MapScreenView.FitScale(new Vector2(50f, 50f),   new Vector2(100f, 100f)), 0.0001f);
            Assert.AreEqual(1f,   MapScreenView.FitScale(Vector2.zero,             new Vector2(100f, 100f)), 0.0001f);
        }
    }
}
