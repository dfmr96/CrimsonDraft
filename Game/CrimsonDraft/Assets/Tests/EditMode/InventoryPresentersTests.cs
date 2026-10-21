#nullable enable

using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Inventory;
using CrimsonDraft.UI;
using static CrimsonDraft.Tests.InventoryTestData;

namespace CrimsonDraft.Tests
{
    public sealed class InventoryPresentersTests
    {
        private GameObject root = null!;
        private InventoryGrid[] grids = null!;

        [SetUp]
        public void SetUp()
        {
            this.root  = new GameObject("Group");
            this.grids = Enumerable.Range(0, 3).Select(i =>
            {
                var go = new GameObject($"Grid{i}", typeof(RectTransform));
                go.transform.SetParent(this.root.transform);
                var grid = go.AddComponent<InventoryGrid>();
                var so   = new SerializedObject(grid);
                so.FindProperty("containerIndex").intValue = i;
                so.ApplyModifiedPropertiesWithoutUndo();
                return grid;
            }).ToArray();

            for (int i = 0; i < 3; i++)
            {
                var so = new SerializedObject(this.grids[i]);
                so.FindProperty("left").objectReferenceValue  = i > 0 ? this.grids[i - 1] : null;
                so.FindProperty("right").objectReferenceValue = i < 2 ? this.grids[i + 1] : null;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(this.root);

        private InventoryPresenters Build(int deadSlot)
        {
            var group = this.root.AddComponent<InventoryGridGroup>();
            var so    = new SerializedObject(group);
            var array = so.FindProperty("grids");
            array.arraySize = 3;
            for (int i = 0; i < 3; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = this.grids[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var roster     = new FakeRoster(Enumerable.Range(0, 3).Select(i => i == deadSlot ? Dead(i) : Alive(i)).ToArray());
            var inventory  = new InventoryService(roster, FakeCombineService.None, FakeCorpseAccess.None);
            var presenters = new InventoryPresenters(inventory, group);
            presenters.Initialize();
            return presenters;
        }

        [Test]
        public void BuildLinks_skipsInaccessibleDeadGrid()
        {
            var links = Build(deadSlot: 1).BuildLinks();
            Assert.AreEqual(ContainerId.Operator(2), links[ContainerId.Operator(0)].Right);
            Assert.AreEqual(ContainerId.Operator(0), links[ContainerId.Operator(2)].Left);
        }

        [Test]
        public void FirstGrid_skipsInaccessibleDeadGrid()
        {
            Assert.AreEqual(ContainerId.Operator(1), Build(deadSlot: 0).FirstGrid);
        }

        [Test]
        public void BuildStorageRows_operatorRowExcludesInaccessibleGrid()
        {
            var rows = Build(deadSlot: 0).BuildStorageRows();
            CollectionAssert.AreEqual(new[] { ContainerId.Operator(1), ContainerId.Operator(2) }, rows[1]);
        }

        [Test]
        public void RefreshAccess_dimsOnlyInaccessibleGrids()
        {
            Build(deadSlot: 1).RefreshAccess();
            Assert.AreEqual(1f, Alpha(this.grids[0]));
            Assert.Less(Alpha(this.grids[1]), 1f);
            Assert.AreEqual(1f, Alpha(this.grids[2]));
        }

        private static float Alpha(InventoryGrid grid) =>
            grid.TryGetComponent(out CanvasGroup group) ? group.alpha : 1f;
    }
}
