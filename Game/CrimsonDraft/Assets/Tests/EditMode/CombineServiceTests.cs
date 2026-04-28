#nullable enable

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using VContainer.Unity;
using CrimsonDraft.Inventory;

namespace CrimsonDraft.Tests
{
    public sealed class CombineServiceTests
    {
        // ── Helpers ────────────────────────────────────────────────────────────

        private static ConsumableData MakeItem(string id)
        {
            var d  = ScriptableObject.CreateInstance<ConsumableData>();
            var so = new SerializedObject(d);
            so.FindProperty("itemId").stringValue      = id;
            so.FindProperty("itemType").enumValueIndex = (int)ItemType.Consumable;
            so.FindProperty("displayName").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        private static CombineRecipeLibrary MakeLibrary(params (ItemData a, ItemData b, ItemData output)[] recipes)
        {
            var lib  = ScriptableObject.CreateInstance<CombineRecipeLibrary>();
            var so   = new SerializedObject(lib);
            var prop = so.FindProperty("recipes");
            prop.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
            {
                var elem = prop.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("inputA").objectReferenceValue = recipes[i].a;
                elem.FindPropertyRelative("inputB").objectReferenceValue = recipes[i].b;
                elem.FindPropertyRelative("output").objectReferenceValue = recipes[i].output;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return lib;
        }

        private static CombineService MakeService(CombineRecipeLibrary lib)
        {
            var svc = new CombineService(lib);
            ((IInitializable)svc).Initialize();
            return svc;
        }

        // ── Tests ──────────────────────────────────────────────────────────────

        [Test]
        public void TryGetResult_returnsOutput_whenRecipeExists()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var docs = MakeItem("documents");
            var svc  = MakeService(MakeLibrary((key, port, docs)));

            var result = svc.TryGetResult(key, port);

            Assert.AreEqual(docs, result);
        }

        [Test]
        public void TryGetResult_isSymmetric_sameResultRegardlessOfOrder()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var docs = MakeItem("documents");
            var svc  = MakeService(MakeLibrary((key, port, docs)));

            Assert.AreEqual(docs, svc.TryGetResult(key,  port));
            Assert.AreEqual(docs, svc.TryGetResult(port, key));
        }

        [Test]
        public void TryGetResult_returnsNull_whenNoRecipeExists()
        {
            var key  = MakeItem("key");
            var port = MakeItem("portfolio");
            var svc  = MakeService(MakeLibrary()); // empty library

            Assert.IsNull(svc.TryGetResult(key, port));
        }

        [Test]
        public void TryGetResult_returnsNull_whenOnlyOneInputMatches()
        {
            var key   = MakeItem("key");
            var port  = MakeItem("portfolio");
            var other = MakeItem("other");
            var docs  = MakeItem("documents");
            var svc   = MakeService(MakeLibrary((key, port, docs)));

            Assert.IsNull(svc.TryGetResult(key, other));
        }

        [Test]
        public void TryGetResult_supportsMultipleRecipes()
        {
            var a1 = MakeItem("a1"); var b1 = MakeItem("b1"); var c1 = MakeItem("c1");
            var a2 = MakeItem("a2"); var b2 = MakeItem("b2"); var c2 = MakeItem("c2");
            var svc = MakeService(MakeLibrary((a1, b1, c1), (a2, b2, c2)));

            Assert.AreEqual(c1, svc.TryGetResult(a1, b1));
            Assert.AreEqual(c2, svc.TryGetResult(a2, b2));
        }
    }
}
