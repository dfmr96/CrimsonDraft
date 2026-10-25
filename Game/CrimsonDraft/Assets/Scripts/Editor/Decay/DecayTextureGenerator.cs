#nullable enable

using System.IO;
using CrimsonDraft.Rendering.Decay;
using UnityEditor;
using UnityEngine;

namespace CrimsonDraft.Editor.Decay
{
    // Texturas procedurales del deterioro, en baja resolución y filtradas en punto para que
    // encajen con el resto de las texturas PSX. Se pueden reemplazar por texturas propias:
    // la ruido desde el DecayPalette, las de los decals desde sus materiales.
    internal static class DecayTextureGenerator
    {
        public const string Root          = "Assets/Art/Decay";
        public const string TexturesDir   = Root + "/Textures";
        public const string MaterialsDir  = Root + "/Decals";
        public const string NoisePath     = TexturesDir + "/DecayNoise.png";
        public const string PalettePath   = Root + "/DecayPalette.asset";
        public const string DecalShader   = "CrimsonDraft/Decal Multiply";

        private const int NoiseSize = 128;
        private const int DecalSize = 64;

        // --- Ruido (tileable) ------------------------------------------------------------------

        public static Texture2D GenerateNoise(int seed)
        {
            var rng    = new System.Random(seed);
            float ox   = (float)rng.NextDouble() * 100f;
            var pixels = new Color32[NoiseSize * NoiseSize];

            for (int y = 0; y < NoiseSize; y++)
            for (int x = 0; x < NoiseSize; x++)
            {
                float u = (float)x / NoiseSize;
                float v = (float)y / NoiseSize;

                // R: manchas grandes, con contraste para que haya zonas limpias y sucias.
                float blot = Fbm(u, v, 4, 4, ox);
                blot = Mathf.Clamp01((blot - 0.5f) * 1.8f + 0.5f);

                // G: grano fino.
                float grain = Fbm(u, v, 24, 2, ox + 17f);

                // B: chorreaduras verticales; columnas angostas que se cortan a distintas alturas.
                float column = Fbm(u, 0.37f, 48, 1, ox + 31f, 1);
                float run    = Fbm(u, v, 12, 2, ox + 47f, 3);
                float streak = Mathf.Clamp01((column - 0.55f) * 4f) * Mathf.Clamp01((run - 0.3f) * 2.5f);

                // A: grietas (bordes de Voronoi), cortadas por una máscara para que no sean una malla.
                float crack = Cracks(u, v, 6, seed) * Mathf.Clamp01((Fbm(u, v, 3, 2, ox + 71f) - 0.45f) * 4f);

                pixels[y * NoiseSize + x] = new Color(blot, grain, streak, crack);
            }

            return SavePng(NoisePath, NoiseSize, pixels, true);
        }

        // --- Formas de los decals (alpha; el color lo pone el tint del material) ----------------

        public static Texture2D GenerateDecalShape(DecayDecal.Kind kind, int seed)
        {
            var rng    = new System.Random(seed + (int)kind * 7919);
            float ox   = (float)rng.NextDouble() * 100f;
            var pixels = new Color32[DecalSize * DecalSize];

            for (int y = 0; y < DecalSize; y++)
            for (int x = 0; x < DecalSize; x++)
            {
                float u = (x + 0.5f) / DecalSize;
                float v = (y + 0.5f) / DecalSize; // v = 1 arriba
                float a = kind switch
                {
                    DecayDecal.Kind.Dirt       => DirtShape(u, v, ox),
                    DecayDecal.Kind.Leak       => DripShape(u, v, ox, 6f, 0.55f),
                    DecayDecal.Kind.Rust       => DripShape(u, v, ox, 10f, 0.75f),
                    DecayDecal.Kind.Mold       => MoldShape(u, v, ox),
                    DecayDecal.Kind.FloorStain => StainShape(u, v, ox),
                    _                          => 0f,
                };
                // Pocos niveles de alpha, como una textura de 4 bits.
                a = Mathf.Round(Mathf.Clamp01(a) * 7f) / 7f;
                pixels[y * DecalSize + x] = new Color(1f, 1f, 1f, a);
            }

            return SavePng($"{TexturesDir}/DecayDecal_{kind}.png", DecalSize, pixels, false);
        }

        private static float DirtShape(float u, float v, float ox)
        {
            float r    = Radial(u, v);
            float n    = Fbm(u, v, 4, 3, ox, 0, false);
            float mask = Mathf.Clamp01((1f - r) * 1.6f + (n - 0.5f) * 1.4f - 0.2f);
            return mask * (0.7f + Fbm(u, v, 16, 1, ox + 9f, 0, false) * 0.5f);
        }

        // Banda arriba (la fuente: techo, caño, remache) y hilos que bajan con largos distintos.
        private static float DripShape(float u, float v, float ox, float columns, float width)
        {
            float edge   = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);
            float top    = Mathf.Clamp01((v - 0.82f) * 8f) * (0.6f + Fbm(u, v, 6, 2, ox, 0, false) * 0.6f);

            float col    = u * columns;
            float cell   = Mathf.Floor(col);
            float local  = col - cell - 0.5f;
            float len    = 0.25f + Hash(cell, ox) * 0.75f;              // hasta dónde baja este hilo
            float thick  = width * (0.3f + Hash(cell, ox + 3f) * 0.7f) * 0.5f;
            float taper  = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(1f - len, 1f, v));
            float line   = Mathf.Clamp01(1f - Mathf.Abs(local) / Mathf.Max(thick * taper, 1e-3f));
            float drip   = line * (v > 1f - len ? 1f : 0f) * (0.75f + Fbm(u, v, 3, 2, ox + 5f, 0, false) * 0.5f);

            // Aureola de agua alrededor de los hilos: se lee como mancha y no solo como líneas.
            float wash   = Mathf.Clamp01((v - 0.15f) * 1.4f) * (0.35f + Fbm(u, v, 4, 2, ox + 11f, 1, false) * 0.45f);

            return Mathf.Max(Mathf.Max(top, drip), wash * 0.55f) * edge;
        }

        private static float MoldShape(float u, float v, float ox)
        {
            float r      = Radial(u, v);
            float cluster = Mathf.Clamp01((1f - r) * 1.4f + (Fbm(u, v, 3, 2, ox, 0, false) - 0.5f) * 1.6f - 0.25f);
            float speck  = Mathf.Clamp01((Fbm(u, v, 20, 2, ox + 13f, 0, false) - 0.45f) * 4f);
            return cluster * Mathf.Lerp(0.4f, 1f, speck);
        }

        // Mancha de agua seca: interior tenue y borde más marcado.
        private static float StainShape(float u, float v, float ox)
        {
            float r      = Radial(u, v) + (Fbm(u, v, 4, 3, ox, 0, false) - 0.5f) * 0.5f;
            float inside = Mathf.Clamp01((0.85f - r) * 4f);
            float ring   = Mathf.Clamp01(1f - Mathf.Abs(r - 0.8f) * 12f);
            return Mathf.Max(inside * 0.45f, ring * 0.9f);
        }

        // --- Materiales y paleta -------------------------------------------------------------

        public static Material GetOrCreateDecalMaterial(DecayDecal.Kind kind, Texture2D texture)
        {
            string path = $"{MaterialsDir}/Decay_{kind}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find(DecalShader);
                if (shader == null) throw new System.InvalidOperationException($"No se encontró el shader \"{DecalShader}\".");

                EnsureFolder(MaterialsDir);
                mat = new Material(shader) { name = $"Decay_{kind}" };
                mat.SetColor("_BloodTint", DefaultTint(kind));
                mat.SetFloat("_BloodStrength", 1f);
                AssetDatabase.CreateAsset(mat, path);
            }

            // La textura se reasigna siempre (por si se regeneró); el tint se respeta si ya lo tocaron.
            mat.SetTexture("Base_Map", texture);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // x2 multiply: 1 = sin cambio.
        private static Color DefaultTint(DecayDecal.Kind kind) => kind switch
        {
            DecayDecal.Kind.Dirt       => new Color(0.38f, 0.32f, 0.25f),
            DecayDecal.Kind.Leak       => new Color(0.48f, 0.40f, 0.26f),
            DecayDecal.Kind.Rust       => new Color(1.25f, 0.48f, 0.22f),
            DecayDecal.Kind.Mold       => new Color(0.32f, 0.42f, 0.24f),
            DecayDecal.Kind.FloorStain => new Color(0.45f, 0.40f, 0.32f),
            _                          => Color.white,
        };

        public static DecayPalette GetOrCreatePalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<DecayPalette>(PalettePath);
            if (palette == null)
            {
                EnsureFolder(Root);
                palette = ScriptableObject.CreateInstance<DecayPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            if (palette.NoiseTexture == null)
            {
                palette.NoiseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath) ?? GenerateNoise(1);
                EditorUtility.SetDirty(palette);
                AssetDatabase.SaveAssets();
            }

            return palette;
        }

        // --- Helpers ---------------------------------------------------------------------------

        private static Texture2D SavePng(string path, int size, Color32[] pixels, bool repeat)
        {
            EnsureFolder(Path.GetDirectoryName(path)!.Replace('\\', '/'));

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType         = TextureImporterType.Default;
                importer.sRGBTexture         = !repeat; // el ruido son datos, no color
                importer.alphaIsTransparency = !repeat;
                importer.mipmapEnabled       = false;
                importer.filterMode          = FilterMode.Point;
                importer.wrapMode            = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.textureCompression  = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static float Radial(float u, float v)
        {
            float dx = (u - 0.5f) * 2f;
            float dy = (v - 0.5f) * 2f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Hash(float n, float seed)
        {
            float s = Mathf.Sin(n * 127.1f + seed * 311.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        private static float Hash2(int x, int y, float seed)
        {
            float s = Mathf.Sin(x * 127.1f + y * 311.7f + seed * 74.7f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        // Value noise con período entero en cada eje, así la textura repite sin costuras.
        private static float ValueNoise(float u, float v, int periodX, int periodY, float seed, bool wrap)
        {
            float x = u * periodX, y = v * periodY;
            int   xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            int WrapX(int i) => wrap ? ((i % periodX) + periodX) % periodX : i;
            int WrapY(int i) => wrap ? ((i % periodY) + periodY) % periodY : i;
            float a = Hash2(WrapX(xi),     WrapY(yi),     seed);
            float b = Hash2(WrapX(xi + 1), WrapY(yi),     seed);
            float c = Hash2(WrapX(xi),     WrapY(yi + 1), seed);
            float d = Hash2(WrapX(xi + 1), WrapY(yi + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        // yStretch > 0 achica el período vertical: el ruido se alarga en v (chorreaduras).
        // yStretch = 1 con período horizontal alto da columnas que solo varían en u.
        private static float Fbm(float u, float v, int basePeriod, int octaves, float seed, int yStretch = 0, bool wrap = true)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int period = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                int periodY = Mathf.Max(1, period / (yStretch + 1));
                sum  += ValueNoise(u, v, period, periodY, seed + o * 13f, wrap) * amp;
                norm += amp;
                amp  *= 0.5f;
                period *= 2;
            }
            return sum / norm;
        }

        private static float Cracks(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int   xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float f1 = 9f, f2 = 9f;

            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int cx = xi + i, cy = yi + j;
                int wx = ((cx % cells) + cells) % cells, wy = ((cy % cells) + cells) % cells;
                float px = cx + Hash2(wx, wy, seed), py = cy + Hash2(wx, wy, seed + 5f);
                float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                if (d < f1) { f2 = f1; f1 = d; }
                else if (d < f2) f2 = d;
            }

            return Mathf.Clamp01(1f - (f2 - f1) / 0.08f);
        }
    }
}
