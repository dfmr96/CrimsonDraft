#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.UI;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorWidgetViewTests
    {
        private GameObject card = null!;
        private GameObject overlay = null!;
        private OperatorWidgetView view = null!;
        private TMPro.TextMeshProUGUI label = null!;

        [SetUp]
        public void SetUp()
        {
            this.card    = new GameObject("Card");
            this.overlay = new GameObject("deadOverlay");
            this.overlay.transform.SetParent(this.card.transform);
            this.view    = this.card.AddComponent<OperatorWidgetView>();
            var so = new SerializedObject(this.view);
            so.FindProperty("deadOverlay").objectReferenceValue = this.overlay;
            this.label = new GameObject("Label", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
            this.label.transform.SetParent(this.overlay.transform);
            so.FindProperty("deadLabel").objectReferenceValue = this.label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.card);

        [Test]
        public void Dead_notLootable_showsOverlay()
        {
            this.view.Bind(Dead(0), canLoot: false, corpseRoomId: null);
            Assert.IsTrue(this.overlay.activeSelf);
            Assert.IsTrue(this.label.gameObject.activeSelf);
        }

        [Test]
        public void Dead_lootable_hidesOverlay()
        {
            this.view.Bind(Dead(0), canLoot: true, corpseRoomId: null);
            Assert.IsFalse(this.overlay.activeSelf);
            Assert.IsFalse(this.label.gameObject.activeSelf);
        }

        [Test]
        public void Alive_hidesOverlay()
        {
            this.view.Bind(Alive(0), canLoot: false, corpseRoomId: null);
            Assert.IsFalse(this.overlay.activeSelf);
        }

        [Test]
        public void Dead_withCorpse_labelPointsToRoom()
        {
            this.view.Bind(Dead(0), canLoot: false, corpseRoomId: "HALLWAY_B1");
            Assert.AreEqual("RETRIEVE ITEMS IN\nHALLWAY_B1", this.label.text);
        }

        [Test]
        public void Dead_withoutCorpse_labelFallsBackToKia()
        {
            this.view.Bind(Dead(0), canLoot: false, corpseRoomId: null);
            Assert.AreEqual("KIA", this.label.text);
        }
    }
}
