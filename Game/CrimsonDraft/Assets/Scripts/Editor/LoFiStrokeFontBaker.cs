#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace CrimsonDraft.Editor
{
    /// <summary>
    /// Lo-Fi Stroke: bakes a pixel TTF (Silver) into a TextMesh Pro font asset with indexed glyphs:
    /// each atlas pixel stores a palette index (0 background, 1-3 fill, 4-6 smoothing, 8 border) that the
    /// "CrimsonDraft/UI/TMP Lo-Fi Stroke" shader turns into colour. Layer rules and shade distribution are
    /// documented in Design/Prototypes/fuente-lofi-stroke/README.md.
    /// </summary>
    public sealed class LoFiStrokeFontBaker : EditorWindow
    {
        private const string MenuPath          = "CrimsonDraft/Fonts/Lo-Fi Stroke Baker";
        private const string QuickMenuPath     = "CrimsonDraft/Fonts/Bake Silver Lo-Fi Stroke";
        private const string DefaultFontPath   = "Assets/Resources/Fonts/Silver.ttf";
        private const string DefaultOutputPath = "Assets/Resources/Fonts/Silver LoFi Stroke.asset";
        private const string DialoguesFolder   = "Assets/Dialogues";
        private const string ShaderName        = "CrimsonDraft/UI/TMP Lo-Fi Stroke";

        private const byte IndexBorder = 8;

        // Typographic punctuation used by es/fr/de text that is outside Latin-1.
        private const string ExtraPunctuation = "ŒœŸ€–—‘’‚“”„…•™";

        [SerializeField] private Font?  sourceFont;
        [SerializeField] private int    pointSize       = 19;
        [SerializeField] private int    atlasPadding    = 2;
        [SerializeField] private int    atlasSize       = 512;
        [SerializeField] private int    extraAdvance    = 1;
        [SerializeField] private bool   includeDialogue = true;
        [SerializeField] private string extraCharacters = string.Empty;
        [SerializeField] private string outputPath      = DefaultOutputPath;

        private string lastReport = string.Empty;

        [MenuItem(MenuPath)]
        public static void ShowWindow() => GetWindow<LoFiStrokeFontBaker>("Lo-Fi Stroke Baker");

        [MenuItem(QuickMenuPath)]
        public static void BakeDefault()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
            if (font == null)
            {
                Debug.LogError($"[LoFiStrokeFontBaker] No se encontró la fuente en {DefaultFontPath}.");
                return;
            }

            var report = Bake(new Settings(font, 19, 2, 512, 1, true, string.Empty, DefaultOutputPath));
            Debug.Log(report);
        }

        private void OnEnable()
        {
            if (this.sourceFont == null)
                this.sourceFont = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Fuente Lo-Fi Stroke", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Crea un font asset de TextMesh Pro con glifos indexados (relleno, suavizado y borde) " +
                "para el shader '" + ShaderName + "'. Para Silver usa 19 px: 100 unidades = 1 píxel.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            this.sourceFont      = (Font?)EditorGUILayout.ObjectField("Fuente (TTF)", this.sourceFont, typeof(Font), false);
            this.pointSize       = EditorGUILayout.IntField("Tamaño (px)", this.pointSize);
            this.atlasPadding    = EditorGUILayout.IntSlider("Margen del atlas", this.atlasPadding, 2, 8);
            this.atlasSize       = EditorGUILayout.IntPopup("Tamaño del atlas", this.atlasSize,
                                       new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
            this.extraAdvance    = EditorGUILayout.IntSlider(
                new GUIContent("Espaciado extra (px)", "Se suma al avance de cada glifo para que el borde no se funda con la letra siguiente."),
                this.extraAdvance, 0, 4);
            this.includeDialogue = EditorGUILayout.Toggle("Caracteres de los .yarn", this.includeDialogue);
            this.extraCharacters = EditorGUILayout.TextField("Caracteres extra", this.extraCharacters);
            this.outputPath      = EditorGUILayout.TextField("Asset de salida", this.outputPath);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(this.sourceFont == null))
            {
                if (GUILayout.Button("Hornear", GUILayout.Height(28)) && this.sourceFont != null)
                {
                    this.lastReport = Bake(new Settings(this.sourceFont, this.pointSize, this.atlasPadding,
                        this.atlasSize, this.extraAdvance, this.includeDialogue, this.extraCharacters, this.outputPath));
                }
            }

            if (this.lastReport.Length > 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(this.lastReport, MessageType.None);
            }
        }

        private readonly struct Settings
        {
            public readonly Font   Font;
            public readonly int    PointSize;
            public readonly int    Padding;
            public readonly int    AtlasSize;
            public readonly int    ExtraAdvance;
            public readonly bool   IncludeDialogue;
            public readonly string Extra;
            public readonly string OutputPath;

            public Settings(Font font, int pointSize, int padding, int atlasSize, int extraAdvance, bool includeDialogue, string extra, string outputPath)
            {
                this.Font            = font;
                this.PointSize       = pointSize;
                this.Padding         = padding;
                this.AtlasSize       = atlasSize;
                this.ExtraAdvance    = extraAdvance;
                this.IncludeDialogue = includeDialogue;
                this.Extra           = extra;
                this.OutputPath      = outputPath;
            }
        }

        private static string Bake(Settings settings)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                return $"No se encontró el shader '{ShaderName}'.";

            var charset = BuildCharset(settings.IncludeDialogue, settings.Extra);

            var fontAsset = TMP_FontAsset.CreateFontAsset(settings.Font, settings.PointSize, settings.Padding,
                GlyphRenderMode.RASTER, settings.AtlasSize, settings.AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (fontAsset == null)
                return "TextMesh Pro no pudo crear el font asset.";

            fontAsset.TryAddCharacters(charset, out var missing);

            // Pixel fonts like Silver leave a 1 px gap between letters; with a 1 px border on each side that gap
            // becomes a single shared dark column and the letters merge. Widen every advance instead.
            if (settings.ExtraAdvance != 0)
            {
                foreach (var glyph in fontAsset.glyphTable)
                {
                    var m = glyph.metrics;
                    glyph.metrics = new GlyphMetrics(m.width, m.height, m.horizontalBearingX, m.horizontalBearingY,
                        m.horizontalAdvance + settings.ExtraAdvance);
                }
            }

            var source = fontAsset.atlasTexture;
            var width  = source.width;
            var height = source.height;
            var indices = BuildIndexAtlas(source.GetPixelData<byte>(0).ToArray(), width, height, fontAsset.glyphTable);

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            var characterCount = fontAsset.characterTable.Count;
            var glyphCount     = fontAsset.glyphTable.Count;
            var tempMaterial   = fontAsset.material;

            var report = SaveAsset(fontAsset, indices, width, height, shader, settings.OutputPath);

            // TMP's own raster atlas and bitmap material were only needed to lay out the glyphs.
            Object.DestroyImmediate(source);
            if (tempMaterial != null)
                Object.DestroyImmediate(tempMaterial);
            var builder = new StringBuilder();
            builder.AppendLine($"[LoFiStrokeFontBaker] {settings.OutputPath}: {characterCount} caracteres, " +
                               $"{glyphCount} glifos, atlas {width}x{height}.");
            builder.AppendLine(report);
            if (!string.IsNullOrEmpty(missing))
                builder.AppendLine($"Sin glifo en la fuente ({missing.Length}): {missing}");
            return builder.ToString().TrimEnd();
        }

        private static string BuildCharset(bool includeDialogue, string extra)
        {
            var set = new SortedSet<char>();
            for (var c = (char)0x20; c <= 0x7E; c++) set.Add(c);
            for (var c = (char)0xA1; c <= 0xFF; c++) set.Add(c);
            foreach (var c in ExtraPunctuation) set.Add(c);
            foreach (var c in extra) set.Add(c);

            if (includeDialogue && Directory.Exists(DialoguesFolder))
            {
                foreach (var file in Directory.GetFiles(DialoguesFolder, "*.yarn", SearchOption.AllDirectories))
                {
                    foreach (var c in File.ReadAllText(file))
                    {
                        if (!char.IsControl(c) && !char.IsSurrogate(c))
                            set.Add(c);
                    }
                }
            }

            return new string(set.ToArray());
        }

        /// <summary>
        /// Converts the 1-bit raster atlas into palette indices, glyph by glyph. Shading is hashed from the
        /// glyph index and glyph-local coordinates, so a character always looks identical.
        /// </summary>
        private static byte[] BuildIndexAtlas(byte[] alpha, int width, int height, List<Glyph> glyphs)
        {
            var output = new byte[width * height];

            foreach (var glyph in glyphs)
            {
                var rect = glyph.glyphRect;
                if (rect.width == 0 || rect.height == 0)
                    continue;

                bool Ink(int x, int y) =>
                    x >= rect.x && x < rect.x + rect.width &&
                    y >= rect.y && y < rect.y + rect.height &&
                    alpha[y * width + x] > 127;

                var minX = Mathf.Max(rect.x - 1, 0);
                var minY = Mathf.Max(rect.y - 1, 0);
                var maxX = Mathf.Min(rect.x + rect.width, width - 1);
                var maxY = Mathf.Min(rect.y + rect.height, height - 1);
                var local = new byte[(maxX - minX + 1) * (maxY - minY + 1)];
                var stride = maxX - minX + 1;

                // Fill (1-3) and corner smoothing (4-6). Texture rows grow upwards: "above" is y + 1.
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var h = Hash(glyph.index, (uint)(x - rect.x + 1), (uint)(y - rect.y + 1));
                        byte value = 0;
                        if (Ink(x, y))
                        {
                            value = FillShade(Ink(x, y + 1), Ink(x - 1, y), h % 100);
                        }
                        else if ((Ink(x - 1, y) || Ink(x + 1, y)) && (Ink(x, y - 1) || Ink(x, y + 1)))
                        {
                            value = (byte)(4 + h % 3);
                        }

                        local[(y - minY) * stride + (x - minX)] = value;
                    }
                }

                // 8-connected border around fill and smoothing.
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var i = (y - minY) * stride + (x - minX);
                        if (local[i] != 0)
                        {
                            output[y * width + x] = local[i];
                            continue;
                        }

                        if (TouchesSolid(local, stride, maxY - minY + 1, x - minX, y - minY))
                            output[y * width + x] = IndexBorder;
                    }
                }
            }

            // Alpha8 atlas: the shader reads index = round(alpha * 15).
            for (var i = 0; i < output.Length; i++)
                output[i] = (byte)(output[i] * 17);

            return output;
        }

        /// <summary>
        /// Fill shade distribution (light/mid/dark), conditioned on ink above and to the left
        /// (stroke tops and left edges are slightly lighter).
        /// </summary>
        private static byte FillShade(bool inkAbove, bool inkLeft, uint roll)
        {
            int light, mid;
            if (!inkAbove && !inkLeft)     { light = 40; mid = 76; }
            else if (!inkAbove && inkLeft) { light = 26; mid = 66; }
            else if (inkAbove && inkLeft)  { light = 33; mid = 75; }
            else                           { light = 25; mid = 85; }

            if (roll < light) return 1;
            return roll < mid ? (byte)2 : (byte)3;
        }

        private static bool TouchesSolid(byte[] local, int width, int height, int x, int y)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if (nx >= 0 && ny >= 0 && nx < width && ny < height && local[ny * width + nx] != 0)
                        return true;
                }
            }

            return false;
        }

        private static uint Hash(uint a, uint b, uint c)
        {
            var h = a * 0x9E3779B1u ^ b * 0x85EBCA77u ^ c * 0xC2B2AE3Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du;
            h ^= h >> 12; h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        /// <summary>
        /// Creates the asset, or updates it in place so prefabs that reference it keep working.
        /// </summary>
        private static string SaveAsset(TMP_FontAsset fontAsset, byte[] indices, int width, int height, Shader shader, string path)
        {
            var assetName = Path.GetFileNameWithoutExtension(path);
            var existing  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (existing == null)
            {
                var atlas    = CreateAtlas(assetName, indices, width, height);
                var material = new Material(shader) { name = assetName + " Material" };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlas);

                fontAsset.name          = assetName;
                fontAsset.atlasTextures = new[] { atlas };
                fontAsset.material      = material;

                AssetDatabase.CreateAsset(fontAsset, path);
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                AssetDatabase.AddObjectToAsset(material, fontAsset);
                AssetDatabase.SaveAssets();
                return "Asset creado.";
            }

            var oldAtlas    = existing.atlasTextures != null && existing.atlasTextures.Length > 0 ? existing.atlasTextures[0] : null;
            var oldMaterial = existing.material;

            EditorUtility.CopySerialized(fontAsset, existing);
            existing.name = assetName;

            if (oldAtlas == null)
            {
                oldAtlas = CreateAtlas(assetName, indices, width, height);
                AssetDatabase.AddObjectToAsset(oldAtlas, existing);
            }
            else
            {
                oldAtlas.Reinitialize(width, height, TextureFormat.Alpha8, false);
                oldAtlas.SetPixelData(indices, 0);
                oldAtlas.filterMode = FilterMode.Point;
                oldAtlas.wrapMode   = TextureWrapMode.Clamp;
                oldAtlas.Apply(false, false);
                oldAtlas.name       = assetName + " Atlas";
            }

            if (oldMaterial == null)
            {
                oldMaterial = new Material(shader) { name = assetName + " Material" };
                AssetDatabase.AddObjectToAsset(oldMaterial, existing);
            }

            oldMaterial.shader = shader;
            oldMaterial.name   = assetName + " Material";
            oldMaterial.SetTexture(ShaderUtilities.ID_MainTex, oldAtlas);

            existing.atlasTextures = new[] { oldAtlas };
            existing.material      = oldMaterial;
            existing.ReadFontAssetDefinition();

            EditorUtility.SetDirty(oldAtlas);
            EditorUtility.SetDirty(oldMaterial);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            // The freshly built asset was only a source for CopySerialized.
            Object.DestroyImmediate(fontAsset);
            return "Asset actualizado (mismo GUID, las referencias se conservan).";
        }

        private static Texture2D CreateAtlas(string assetName, byte[] indices, int width, int height)
        {
            var atlas = new Texture2D(width, height, TextureFormat.Alpha8, false)
            {
                name       = assetName + " Atlas",
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            atlas.SetPixelData(indices, 0);
            atlas.Apply(false, false);
            return atlas;
        }
    }
}
