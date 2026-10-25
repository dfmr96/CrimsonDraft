#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrimsonDraft.Navigation.Editor
{
    /// <summary>Unlit stand-ins for the level's materials: same texture, colour, tiling and alpha cut-out on
    /// URP's Unlit shader. The PSX shaders ignore URP's debug modes and Unity's ambient light, so swapping
    /// materials is the only way to see the level without lighting in the Camera Zone Viewer.</summary>
    public sealed class UnlitMaterialCache : IDisposable
    {
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        private static readonly string[] TextureProperties = { "_BaseMap", "_MainTex", "_Texture2D", "baseColorTexture" };
        private static readonly string[] ColorProperties = { "_BaseColor", "_Color", "baseColorFactor" };

        private readonly Dictionary<Material, Material?> cache = new Dictionary<Material, Material?>();
        private Shader? unlitShader;

        /// <summary>The unlit stand-in, or null when the material is already unlit or an effect (kept as is).</summary>
        public Material? UnlitFor(Material original)
        {
            if (this.cache.TryGetValue(original, out var cached)) return cached;

            Material? unlit = null;
            if (!KeepAsIs(original))
            {
                this.unlitShader ??= Shader.Find(UnlitShaderName);
                if (this.unlitShader != null) unlit = Build(original, this.unlitShader);
            }
            this.cache[original] = unlit;
            return unlit;
        }

        public void Dispose()
        {
            foreach (var material in this.cache.Values)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            this.cache.Clear();
        }

        private static bool KeepAsIs(Material material)
        {
            var name = material.shader != null ? material.shader.name : "";
            return name.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Particles", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Material Build(Material original, Shader shader)
        {
            var unlit = new Material(shader) { name = original.name + " (Unlit)", hideFlags = HideFlags.HideAndDontSave };

            foreach (var property in TextureProperties)
            {
                if (!original.HasTexture(property) || original.GetTexture(property) == null) continue;
                unlit.SetTexture("_BaseMap", original.GetTexture(property));
                unlit.SetTextureScale("_BaseMap", original.GetTextureScale(property));
                unlit.SetTextureOffset("_BaseMap", original.GetTextureOffset(property));
                break;
            }

            var color = Color.white;
            foreach (var property in ColorProperties)
            {
                if (!original.HasColor(property)) continue;
                color = original.GetColor(property);
                break;
            }
            unlit.SetColor("_BaseColor", color);

            // Keep cut-outs (fences, foliage) cut out; fully opaque texels are unaffected.
            unlit.SetFloat("_AlphaClip", 1f);
            unlit.SetFloat("_Cutoff", 0.5f);
            unlit.EnableKeyword("_ALPHATEST_ON");
            unlit.renderQueue = (int)RenderQueue.AlphaTest;
            return unlit;
        }
    }

    /// <summary>Swaps renderers to their unlit stand-ins for the duration of a <c>using</c> block. Meant
    /// around one synchronous Camera.Render: the scene is back to its own materials before anything else
    /// runs, and nothing is recorded as a change.</summary>
    public sealed class UnlitSwap : IDisposable
    {
        private readonly List<(Renderer renderer, Material[] materials)> originals = new List<(Renderer, Material[])>();

        private UnlitSwap() { }

        public static UnlitSwap Begin(IEnumerable<Renderer> renderers, UnlitMaterialCache cache)
        {
            var swap = new UnlitSwap();
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                Material[]? replaced = null;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    var unlit = cache.UnlitFor(materials[i]);
                    if (unlit == null) continue;
                    replaced ??= (Material[])materials.Clone();
                    replaced[i] = unlit;
                }
                if (replaced == null) continue;
                swap.originals.Add((renderer, materials));
                renderer.sharedMaterials = replaced;
            }
            return swap;
        }

        public void Dispose()
        {
            foreach (var (renderer, materials) in this.originals)
                if (renderer != null) renderer.sharedMaterials = materials;
            this.originals.Clear();
        }
    }
}
