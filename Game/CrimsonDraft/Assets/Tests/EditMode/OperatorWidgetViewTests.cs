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

        [SetUp]
        public void SetUp()
        {
            this.card    = new GameObject("Card");
            this.overlay = new GameObject("deadOverlay");
            this.overlay.transform.SetParent(this.card.transform);
            this.view    = this.card.AddComponent<OperatorWidgetView>();
            var so = new SerializedObject(this.view);
            so.FindProperty("deadOverlay").objectReferenceValue = this.overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.card);

        [Test]
        public void Dead_notLootable_showsOverlay()
        {
            this.view.Bind(Dead(0), canLoot: false);
            Assert.IsTrue(this.overlay.activeSelf);
        }

        [Test]
        public void Dead_lootable_hidesOverlay()
        {
            this.view.Bind(Dead(0), canLoot: true);
            Assert.IsFalse(this.overlay.activeSelf);
        }

        [Test]
        public void Alive_hidesOverlay()
        {
            this.view.Bind(Alive(0), canLoot: false);
            Assert.IsFalse(this.overlay.activeSelf);
        }
    }
}
