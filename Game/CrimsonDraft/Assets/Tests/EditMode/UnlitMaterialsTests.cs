#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Editor;

namespace CrimsonDraft.Tests
{
    public sealed class UnlitMaterialsTests
    {
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private readonly List<Object> created = new List<Object>();
        private UnlitMaterialCache cache = null!;

        [SetUp]
        public void SetUp() => this.cache = new UnlitMaterialCache();

        [TearDown]
        public void TearDown()
        {
            this.cache.Dispose();
            foreach (var o in this.created) if (o != null) Object.DestroyImmediate(o);
            this.created.Clear();
        }

        [Test]
        public void UnlitFor_urpLit_copiesBaseMapColorAndTiling()
        {
            var texture = Track(new Texture2D(4, 4));
            var lit = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            lit.SetTexture("_BaseMap", texture);
            lit.SetTextureScale("_BaseMap", new Vector2(2, 3));
            lit.SetTextureOffset("_BaseMap", new Vector2(0.5f, 0.25f));
            lit.SetColor("_BaseColor", Color.red);

            var unlit = this.cache.UnlitFor(lit);

            Assert.IsNotNull(unlit);
            Assert.AreEqual(UnlitShader, unlit!.shader.name);
            Assert.AreSame(texture, unlit.GetTexture("_BaseMap"));
            Assert.AreEqual(new Vector2(2, 3), unlit.GetTextureScale("_BaseMap"));
            Assert.AreEqual(new Vector2(0.5f, 0.25f), unlit.GetTextureOffset("_BaseMap"));
            Assert.AreEqual(Color.red, unlit.GetColor("_BaseColor"));
        }

        [Test]
        public void UnlitFor_psxMaterial_usesMainTex()
        {
            var psx = Shader.Find("Shader Graphs/PSXMaster_URP");
            Assume.That(psx != null, "project PSX shader");
            var texture = Track(new Texture2D(4, 4));
            var material = Track(new Material(psx));
            material.SetTexture("_MainTex", texture);

            var unlit = this.cache.UnlitFor(material);

            Assert.IsNotNull(unlit);
            Assert.AreSame(texture, unlit!.GetTexture("_BaseMap"));
        }

        [Test]
        public void UnlitFor_isCachedPerMaterial()
        {
            var lit = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            Assert.AreSame(this.cache.UnlitFor(lit), this.cache.UnlitFor(lit));
        }

        [Test]
        public void UnlitFor_alreadyUnlit_keepsTheOriginal()
        {
            var unlit = Track(new Material(Shader.Find(UnlitShader)));
            Assert.IsNull(this.cache.UnlitFor(unlit));
        }

        [Test]
        public void Swap_replacesMaterialsAndRestoresThemOnDispose()
        {
            var go = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var renderer = go.GetComponent<Renderer>();
            var original = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            renderer.sharedMaterial = original;

            using (UnlitSwap.Begin(new[] { renderer }, this.cache))
            {
                Assert.AreEqual(UnlitShader, renderer.sharedMaterial.shader.name);
            }

            Assert.AreSame(original, renderer.sharedMaterial);
        }

        private T Track<T>(T obj) where T : Object
        {
            this.created.Add(obj);
            return obj;
        }
    }
}
