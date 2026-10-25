#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor.Decay
{
    // Pasa los materiales de una sala de PSXMaster_URP a PSXDecay_URP (y vuelta). Las
    // propiedades se llaman igual, así que el material queda idéntico; solo se suman las de
    // Decay. El cambio es sobre el asset y un material se comparte entre salas, pero solo se
    // ensucian los renderers con id de sala: en cualquier otro lado PSXDecay se ve igual que PSXMaster.
    internal static class DecayMaterialMigrator
    {
        public const string SourceShader = "Shader Graphs/PSXMaster_URP";
        public const string DecayShader  = "Shader Graphs/PSXDecay_URP";

        private static readonly int MetalId = Shader.PropertyToID("_DecayMetal");
        private static readonly int PaintId = Shader.PropertyToID("_DecayPaint");

        // Palabras en el nombre del material/textura que sugieren metal (barco: casi todo).
        private static readonly string[] MetalHints =
        {
            "metal", "steel", "iron", "pipe", "rust", "hull", "rail", "grate", "vent", "locker",
            "hierro", "acero", "tubo", "caño", "cano", "chapa", "reja", "oxido",
        };

        // Superficies que no son pintura (telas, madera cruda, vidrio): no se descascaran.
        private static readonly string[] NoPaintHints =
        {
            "fabric", "cloth", "curtain", "bed", "carpet", "glass", "wood", "book", "paper",
            "tela", "cortina", "cama", "alfombra", "vidrio", "madera", "libro", "papel",
        };

        public static List<Material> FindMaterials(IEnumerable<MeshRenderer> renderers, string shaderName)
        {
            var result = new List<Material>();
            var seen   = new HashSet<Material>();

            foreach (var renderer in renderers)
            foreach (var m in renderer.sharedMaterials)
            {
                if (m == null || m.shader == null || m.shader.name != shaderName) continue;
                if (!AssetDatabase.Contains(m) || !seen.Add(m)) continue;
                result.Add(m);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        public static int Migrate(IReadOnlyList<Material> materials, bool guessSurface)
        {
            var target = Shader.Find(DecayShader);
            if (target == null) throw new System.InvalidOperationException($"No se encontró \"{DecayShader}\".");

            var roots = ShaderOwners(materials);
            Undo.RecordObjects(ToArray(roots), "Migrate materials to PSXDecay");
            foreach (var m in roots)
            {
                int queue = m.renderQueue;
                m.shader = target;
                m.renderQueue = queue;
                EditorUtility.SetDirty(m);
            }

            foreach (var m in materials)
            {

                if (guessSurface)
                {
                    string name = (m.name + " " + (m.mainTexture != null ? m.mainTexture.name : "")).ToLowerInvariant();
                    m.SetFloat(MetalId, ContainsAny(name, MetalHints) ? 0.8f : 0f);
                    m.SetFloat(PaintId, ContainsAny(name, NoPaintHints) ? 0f : 0.6f);
                }
                EditorUtility.SetDirty(m);
            }

            AssetDatabase.SaveAssets();
            return materials.Count;
        }

        public static int Revert(IReadOnlyList<Material> materials)
        {
            var source = Shader.Find(SourceShader);
            if (source == null) throw new System.InvalidOperationException($"No se encontró \"{SourceShader}\".");

            var roots = ShaderOwners(materials);
            Undo.RecordObjects(ToArray(roots), "Revert materials to PSXMaster");
            foreach (var m in roots)
            {
                int queue = m.renderQueue;
                m.shader = source;
                m.renderQueue = queue;
                EditorUtility.SetDirty(m);
            }

            AssetDatabase.SaveAssets();
            return materials.Count;
        }

        // En un Material Variant el shader lo define el material raíz (Unity no deja cambiarlo
        // en la variante): se cambia el de la raíz y la variante lo hereda.
        private static List<Material> ShaderOwners(IReadOnlyList<Material> materials)
        {
            var roots = new List<Material>();
            foreach (var m in materials)
            {
                var root = m;
                while (root.isVariant && root.parent != null) root = root.parent;
                if (!roots.Contains(root)) roots.Add(root);
            }
            return roots;
        }

        private static bool ContainsAny(string text, string[] hints)
        {
            foreach (var h in hints)
                if (text.Contains(h)) return true;
            return false;
        }

        private static Object[] ToArray(List<Material> materials)
        {
            var arr = new Object[materials.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = materials[i];
            return arr;
        }
    }
}
