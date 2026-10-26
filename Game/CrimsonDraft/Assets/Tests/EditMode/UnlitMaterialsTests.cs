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

        // Props like the Seaman Room 2 bed: PSX material with alpha clip on but _AlphaClipThreshold 0 and a
        // _BaseColor alpha of 0. In game nothing is cut (alpha 0 >= threshold 0); a stand-in clipping at a
        // fixed 0.5 cut the whole prop away. It must clip at the original's own threshold.
        [Test]
        public void UnlitFor_psxClipWithZeroThreshold_keepsThatThreshold()
        {
            var psx = Shader.Find("Shader Graphs/PSXDecay_URP");
            Assume.That(psx != null, "project PSX shader");
            var material = Track(new Material(psx));
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_AlphaClipThreshold", 0f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));

            var unlit = this.cache.UnlitFor(material);

            Assert.IsNotNull(unlit);
            Assert.AreEqual(0f, unlit!.GetFloat("_Cutoff"));
        }

        // An original that doesn't clip at all ignores alpha in game: the stand-in neither clips nor
        // carries a see-through colour.
        [Test]
        public void UnlitFor_opaqueOriginalWithZeroColorAlpha_isOpaque()
        {
            var lit = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            lit.SetFloat("_AlphaClip", 0f);
            lit.DisableKeyword("_ALPHATEST_ON");
            lit.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));

            var unlit = this.cache.UnlitFor(lit);

            Assert.IsNotNull(unlit);
            Assert.AreEqual(1f, unlit!.GetColor("_BaseColor").a);
            Assert.IsFalse(unlit.IsKeywordEnabled("_ALPHATEST_ON"));
        }

        [Test]
        public void UnlitFor_alphaClippedOriginal_keepsItsCutoutAndThreshold()
        {
            var lit = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            lit.SetFloat("_AlphaClip", 1f);
            lit.SetFloat("_Cutoff", 0.3f);
            lit.EnableKeyword("_ALPHATEST_ON");
            lit.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));

            var unlit = this.cache.UnlitFor(lit);

            Assert.IsNotNull(unlit);
            Assert.IsTrue(unlit!.IsKeywordEnabled("_ALPHATEST_ON"));
            Assert.AreEqual(0.3f, unlit.GetFloat("_Cutoff"), 1e-4f);
            Assert.AreEqual(0.8f, unlit.GetColor("_BaseColor").a, 1e-4f);
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
