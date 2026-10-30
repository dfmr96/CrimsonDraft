using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Rendering;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Batch-bakes random zombie looks: for each variant it picks a random texture and HSL tint per
    /// layer of the Clothes and Details sets (same skin on both so body and head match), blends them
    /// through the sets' mix materials, saves the two PNGs and wraps them in a Body and a Details
    /// material. All variants are listed in a <see cref="ZombieVariantSet"/> that
    /// <see cref="ZombieVariantRandomizer"/> reads at runtime.
    /// </summary>
    public class ZombieVariantGenerator : EditorWindow
    {
        private const string MenuPath      = "CrimsonDraft/Zombie Variant Generator";
        private const string HslShaderName = "HorrorEngine/ZombieAddOn/TextureColorAdjustHSL";

        private const string Root        = "Assets/Art/Models/CharactersFBX/ZombieMale";
        private const string OutputDir   = Root + "/Variants";
        private const string SetPath     = OutputDir + "/ZombieVariantSet.asset";
        private const string ClothesSet  = Root + "/TextureEditor/TextureSets/ZombieClothesTextureSet.asset";
        private const string DetailsSet  = Root + "/TextureEditor/TextureSets/ZombieDetailsTextureSet.asset";
        private const string BodyMat     = Root + "/Materials/ZombieMale_Body.mat";
        private const string DetailsMat  = Root + "/Materials/ZombieMale_Details.mat";

        private int m_Count = 12;
        private int m_Seed = 1234;
        private int m_Size = 1024;

        private struct Tint
        {
            public float Hue, Saturation, Lightness;
            public static readonly Tint None = new Tint { Hue = 0f, Saturation = 1f, Lightness = 1f };
        }

        [MenuItem(MenuPath)]
        public static void ShowWindow()
        {
            GetWindow<ZombieVariantGenerator>("Zombie Variants");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                $"Regenerates every file in {OutputDir} (previous variants are deleted) " +
                "and refreshes the ZombieVariantSet used by ZombieVariantRandomizer.",
                MessageType.Info);

            m_Count = EditorGUILayout.IntSlider("Variants", m_Count, 1, 64);
            m_Seed = EditorGUILayout.IntField("Seed", m_Seed);
            m_Size = EditorGUILayout.IntPopup("Texture Size", m_Size, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });

            if (GUILayout.Button("Generate"))
                Generate(m_Count, m_Seed, m_Size);
        }

        public static void Generate(int count, int seed, int size)
        {
            var clothes = AssetDatabase.LoadAssetAtPath<ZombieTextureSet>(ClothesSet);
            var details = AssetDatabase.LoadAssetAtPath<ZombieTextureSet>(DetailsSet);
            var baseBody = AssetDatabase.LoadAssetAtPath<Material>(BodyMat);
            var baseDetails = AssetDatabase.LoadAssetAtPath<Material>(DetailsMat);
            var hslShader = Shader.Find(HslShaderName);

            if (!clothes || !details || !baseBody || !baseDetails || !hslShader
                || !clothes.SetMixMaterial || !details.SetMixMaterial)
            {
                Debug.LogError("[ZombieVariantGenerator] Missing texture sets, base materials or HSL shader.");
                return;
            }

            ClearOutputDir();
            Directory.CreateDirectory(OutputDir);

            var hsl = new Material(hslShader) { hideFlags = HideFlags.HideAndDontSave };
            var clothesMix = new Material(clothes.SetMixMaterial) { hideFlags = HideFlags.HideAndDontSave };
            var detailsMix = new Material(details.SetMixMaterial) { hideFlags = HideFlags.HideAndDontSave };
            var clear = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            clear.SetPixels(new[] { Color.clear, Color.clear, Color.clear, Color.clear });
            clear.Apply();

            var rng = new System.Random(seed);
            var pngPaths = new List<(string body, string details)>();

            try
            {
                for (int i = 0; i < count; i++)
                {
                    int skinCount = Mathf.Min(SkinCount(clothes), SkinCount(details));
                    int skinIndex = skinCount > 0 ? rng.Next(skinCount) : 0;
                    var skinTint = new Tint
                    {
                        Hue = Range(rng, -0.04f, 0.04f),
                        Saturation = Range(rng, 0.6f, 1.1f),
                        Lightness = Range(rng, 0.65f, 1.1f),
                    };

                    string bodyPath = $"{OutputDir}/ZombieBody_{i:00}.png";
                    string detailsPath = $"{OutputDir}/ZombieDetails_{i:00}.png";
                    Bake(clothes, clothesMix, hsl, clear, rng, size, skinIndex, skinTint, bodyPath);
                    Bake(details, detailsMix, hsl, clear, rng, size, skinIndex, skinTint, detailsPath);
                    pngPaths.Add((bodyPath, detailsPath));
                }
            }
            finally
            {
                DestroyImmediate(hsl);
                DestroyImmediate(clothesMix);
                DestroyImmediate(detailsMix);
                DestroyImmediate(clear);
            }

            AssetDatabase.Refresh();

            var variants = new List<ZombieVariantSet.Variant>();
            for (int i = 0; i < pngPaths.Count; i++)
            {
                var bodyTex = ImportAsPointTexture(pngPaths[i].body);
                var detailsTex = ImportAsPointTexture(pngPaths[i].details);

                variants.Add(new ZombieVariantSet.Variant
                {
                    Body = CreateMaterial(baseBody, $"{OutputDir}/ZombieMale_Body_{i:00}.mat", bodyTex),
                    Details = CreateMaterial(baseDetails, $"{OutputDir}/ZombieMale_Details_{i:00}.mat", detailsTex),
                });
            }

            var set = AssetDatabase.LoadAssetAtPath<ZombieVariantSet>(SetPath);
            if (!set)
            {
                set = CreateInstance<ZombieVariantSet>();
                AssetDatabase.CreateAsset(set, SetPath);
            }
            set.BaseBody = baseBody;
            set.BaseDetails = baseDetails;
            set.Variants = variants.ToArray();
            EditorUtility.SetDirty(set);

            AssetDatabase.SaveAssets();
            Debug.Log($"[ZombieVariantGenerator] Generated {variants.Count} variants in {OutputDir}");
        }

        private static void ClearOutputDir()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
                return;

            foreach (var guid in AssetDatabase.FindAssets("", new[] { OutputDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".png") || path.EndsWith(".mat"))
                    AssetDatabase.DeleteAsset(path);
            }
        }

        private static int SkinCount(ZombieTextureSet set)
        {
            foreach (var entry in set.Entries)
                if (entry.Name == "_Skin")
                    return entry.Textures?.Length ?? 0;
            return 0;
        }

        private static void Bake(ZombieTextureSet set, Material mix, Material hsl, Texture clear,
            System.Random rng, int size, int skinIndex, Tint skinTint, string assetPath)
        {
            var layers = new List<RenderTexture>();
            var result = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;

            try
            {
                foreach (var entry in set.Entries)
                {
                    var textures = entry.Textures;
                    Texture source = clear;
                    var tint = Tint.None;

                    if (textures != null && textures.Length > 0)
                    {
                        int index;
                        switch (entry.Name)
                        {
                            case "_Skin":
                                index = Mathf.Min(skinIndex, textures.Length - 1);
                                tint = skinTint;
                                break;
                            case "_Overlay":
                                // Index 0 is the clean layer; keep it half the time.
                                index = rng.NextDouble() < 0.5 ? 0 : rng.Next(textures.Length);
                                break;
                            case "_Hair":
                                index = rng.Next(textures.Length);
                                tint = new Tint { Hue = Range(rng, -0.08f, 0.08f), Saturation = Range(rng, 0.5f, 1.2f), Lightness = Range(rng, 0.4f, 1.2f) };
                                break;
                            case "_Face":
                            case "_Sleeves":
                                index = rng.Next(textures.Length);
                                break;
                            case "_Shoes":
                            case "_Forearms":
                                index = rng.Next(textures.Length);
                                tint = new Tint { Hue = 0f, Saturation = Range(rng, 0.6f, 1.1f), Lightness = Range(rng, 0.5f, 1.1f) };
                                break;
                            default: // clothes: _UpperBody, _LowerBody, _Accesories
                                index = rng.Next(textures.Length);
                                tint = new Tint { Hue = Range(rng, -1f, 1f), Saturation = Range(rng, 0.5f, 1.2f), Lightness = Range(rng, 0.5f, 1.1f) };
                                break;
                        }

                        source = textures[index] ? textures[index] : clear;
                    }

                    var layer = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
                    hsl.SetTexture("_MainTex", source);
                    hsl.SetFloat("_Hue", tint.Hue);
                    hsl.SetFloat("_Saturation", tint.Saturation);
                    hsl.SetFloat("_Lightness", tint.Lightness);
                    Graphics.Blit(source, layer, hsl);

                    mix.SetTexture(entry.Name, layer);
                    layers.Add(layer);
                }

                Graphics.Blit(Texture2D.blackTexture, result, mix);

                RenderTexture.active = result;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
                DestroyImmediate(texture);
            }
            finally
            {
                RenderTexture.active = previous;
                foreach (var layer in layers)
                    RenderTexture.ReleaseTemporary(layer);
                RenderTexture.ReleaseTemporary(result);
            }
        }

        private static Texture2D ImportAsPointTexture(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Material CreateMaterial(Material template, string path, Texture2D texture)
        {
            var material = new Material(template);
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}
