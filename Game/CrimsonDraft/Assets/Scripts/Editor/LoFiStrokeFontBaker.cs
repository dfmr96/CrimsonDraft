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
        private const string SoftOutputPath    = "Assets/Resources/Fonts/Silver LoFi Soft.asset";
        private const string SoftMenuPath      = "CrimsonDraft/Fonts/Bake Silver Lo-Fi Soft";
        private const string PlainSoftOutputPath = "Assets/Resources/Fonts/Silver Soft.asset";
        private const string PlainSoftMenuPath   = "CrimsonDraft/Fonts/Bake Silver Soft (plain shader)";
        private const string BakedShaderName     = "CrimsonDraft/UI/TMP Bitmap Soft";

        // Baked colours of the plain variant (multiplied by the text tint at draw time).
        // Fill tones (index 1 light, 2 mid, 3 dark), so a letter is not one flat colour; the tint multiplies them.
        private static readonly Color32[] BakedFill =
        {
            new(255, 255, 255, 255),
            new(236, 236, 236, 255),
            new(206, 206, 206, 255),
        };
        private static readonly Color32 BakedShadow = new(72, 72, 72, 150);   // darker than the text, ~60% opaque
        private static readonly Color32 BakedSoft   = new(255, 255, 255, 77); // same colour as the text, 30% opaque
        private const string DialoguesFolder   = "Assets/Dialogues";
        private const string ShaderName        = "CrimsonDraft/UI/TMP Lo-Fi Stroke";

        private const byte IndexBorder = 8;
        private const byte IndexShadow = 9;  // 1 px under every ink pixel; the shader draws it with the border colour
        private const byte IndexSoft   = 10; // 1 px halo left/right/above the ink; faint, simulates anti-aliasing

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
        [SerializeField] private bool   softShadow      = false;
        [SerializeField] private bool   flatFill        = false;
        [SerializeField] private bool   softHalo        = false;

        private string lastReport = string.Empty;

        [MenuItem(MenuPath)]
        public static void ShowWindow() => GetWindow<LoFiStrokeFontBaker>("Lo-Fi Stroke Baker");

        [MenuItem(QuickMenuPath)]
        public static void BakeDefault()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
            if (font == null)
            {
                Debug.LogError($"[LoFiStrokeFontBaker] Font not found at {DefaultFontPath}.");
                return;
            }

            var report = Bake(new Settings(font, 19, 2, 512, 1, true, string.Empty, DefaultOutputPath));
            Debug.Log(report);
        }

        [MenuItem(PlainSoftMenuPath)]
        public static void BakePlainSoft()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
            if (font == null)
            {
                Debug.LogError($"[LoFiStrokeFontBaker] Font not found at {DefaultFontPath}.");
                return;
            }

            // Shadow only (a copy of the letter one pixel below), and the letter keeps its light/mid/dark variation.
            // No extra advance: the shadow is only below, so Silver's own 1 px gap between letters is enough.
            Debug.Log(Bake(new Settings(font, 19, 2, 512, 0, true, string.Empty, PlainSoftOutputPath,
                softShadow: true, flatFill: false, bakedColor: true, softHalo: false, antiAlias: 0.45f)));
        }

        [MenuItem(SoftMenuPath)]
        public static void BakeSoft()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
            if (font == null)
            {
                Debug.LogError($"[LoFiStrokeFontBaker] Font not found at {DefaultFontPath}.");
                return;
            }

            Debug.Log(Bake(new Settings(font, 19, 2, 512, 1, true, string.Empty, SoftOutputPath,
                softShadow: true, flatFill: true, softHalo: true)));
        }

        private void OnEnable()
        {
            if (this.sourceFont == null)
                this.sourceFont = AssetDatabase.LoadAssetAtPath<Font>(DefaultFontPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Lo-Fi Stroke Font", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Creates a TextMesh Pro font asset with indexed glyphs (fill, smoothing and outline) " +
                "for the '" + ShaderName + "' shader. Use 19 px for Silver: 100 units = 1 pixel.",
                MessageType.Info);
            EditorGUILayout.Space(6);

            this.sourceFont      = (Font?)EditorGUILayout.ObjectField("Font (TTF)", this.sourceFont, typeof(Font), false);
            this.pointSize       = EditorGUILayout.IntField("Size (px)", this.pointSize);
            this.atlasPadding    = EditorGUILayout.IntSlider("Atlas padding", this.atlasPadding, 2, 8);
            this.atlasSize       = EditorGUILayout.IntPopup("Atlas size", this.atlasSize,
                                       new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });
            this.extraAdvance    = EditorGUILayout.IntSlider(
                new GUIContent("Extra advance (px)", "Added to every glyph advance so the outline does not merge with the next letter."),
                this.extraAdvance, 0, 4);
            this.includeDialogue = EditorGUILayout.Toggle("Characters from .yarn files", this.includeDialogue);
            this.extraCharacters = EditorGUILayout.TextField("Extra characters", this.extraCharacters);
            this.outputPath      = EditorGUILayout.TextField("Output asset", this.outputPath);
            this.softShadow      = EditorGUILayout.Toggle(
                new GUIContent("Soft shadow style", "No outline: bakes a 1 px drop shadow under every pixel (index 9) and a faint halo (index 10) instead."),
                this.softShadow);
            using (new EditorGUI.DisabledScope(!this.softShadow))
                this.softHalo = EditorGUILayout.Toggle(
                    new GUIContent("Soft halo", "Also bakes a faint 1 px halo left, right and above the letter (index 10). Off = shadow only, below."),
                    this.softHalo);
            using (new EditorGUI.DisabledScope(!this.softShadow))
                this.flatFill = EditorGUILayout.Toggle(
                    new GUIContent("Flat fill", "Every fill pixel uses the same shade (index 2) instead of the hashed light/mid/dark mix."), this.flatFill);

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(this.sourceFont == null))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(28)) && this.sourceFont != null)
                {
                    this.lastReport = Bake(new Settings(this.sourceFont, this.pointSize, this.atlasPadding,
                        this.atlasSize, this.extraAdvance, this.includeDialogue, this.extraCharacters, this.outputPath,
                        this.softShadow, this.flatFill, softHalo: this.softHalo));
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
            public readonly bool   SoftShadow;
            public readonly bool   FlatFill;
            public readonly bool   BakedColor;
            public readonly bool   SoftHalo;
            public readonly float  AntiAlias;

            public Settings(Font font, int pointSize, int padding, int atlasSize, int extraAdvance, bool includeDialogue, string extra, string outputPath,
                bool softShadow = false, bool flatFill = false, bool bakedColor = false, bool softHalo = false, float antiAlias = 0f)
            {
                this.AntiAlias       = antiAlias;
                this.SoftHalo        = softHalo;
                this.BakedColor      = bakedColor;
                this.SoftShadow      = softShadow;
                this.FlatFill        = flatFill;
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
            var shaderName = settings.BakedColor ? BakedShaderName : ShaderName;
            var shader = Shader.Find(shaderName);
            if (shader == null)
                return $"Shader '{shaderName}' not found.";

            var charset = BuildCharset(settings.IncludeDialogue, settings.Extra);

            var fontAsset = TMP_FontAsset.CreateFontAsset(settings.Font, settings.PointSize, settings.Padding,
                GlyphRenderMode.RASTER, settings.AtlasSize, settings.AtlasSize, AtlasPopulationMode.Dynamic, false);
            if (fontAsset == null)
                return "TextMesh Pro could not create the font asset.";

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
            var indices = BuildIndexAtlas(source.GetPixelData<byte>(0).ToArray(), width, height, fontAsset.glyphTable,
                settings.SoftShadow, settings.FlatFill, settings.SoftHalo);

            // Plain variant: the colours are baked into an RGBA atlas instead of palette indices.
            var pixelFormat = TextureFormat.Alpha8;
            if (settings.BakedColor)
            {
                indices     = ToBakedColorAtlas(indices, width, height, settings.AntiAlias);
                pixelFormat = TextureFormat.RGBA32;
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            var characterCount = fontAsset.characterTable.Count;
            var glyphCount     = fontAsset.glyphTable.Count;
            var tempMaterial   = fontAsset.material;

            bool isNew = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(settings.OutputPath) == null;
            var report = SaveAsset(fontAsset, indices, width, height, shader, settings.OutputPath, pixelFormat);

            // Strengths of the baked shadow/halo live on the material: set once on creation, then left for tuning.
            if (settings.SoftShadow && isNew)
            {
                var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(settings.OutputPath);
                if (saved != null && saved.material != null)
                {
                    if (!settings.BakedColor)
                    {
                        saved.material.SetFloat("_ShadowAlpha", 0.75f);
                        saved.material.SetFloat("_SoftAlpha", 0.3f);
                    }
                    EditorUtility.SetDirty(saved.material);
                    AssetDatabase.SaveAssets();
                }
            }

            // TMP's own raster atlas and bitmap material were only needed to lay out the glyphs.
            Object.DestroyImmediate(source);
            if (tempMaterial != null)
                Object.DestroyImmediate(tempMaterial);
            var builder = new StringBuilder();
            builder.AppendLine($"[LoFiStrokeFontBaker] {settings.OutputPath}: {characterCount} characters, " +
                               $"{glyphCount} glyphs, atlas {width}x{height}.");
            builder.AppendLine(report);
            if (!string.IsNullOrEmpty(missing))
                builder.AppendLine($"Missing from the font ({missing.Length}): {missing}");
            return builder.ToString().TrimEnd();
        }

        // Alpha8 index atlas (index * 17) -> RGBA32 with the final colours of the soft style.
        // antiAlias (0-1) is the opacity of the "bridges": empty pixels that have ink on one side horizontally AND one
        // side vertically sit between two diagonal ink pixels (a corner or a staircase step), so they are filled
        // partially with the ink colour. That joins the strokes and rounds the corners, changing the letter itself;
        // nothing is drawn around the outside of straight edges. The drop shadow goes underneath and stays sharp.
        private static byte[] ToBakedColorAtlas(byte[] alpha8, int width, int height, float antiAlias)
        {
            var count = alpha8.Length;
            var index = new int[count];
            for (var i = 0; i < count; i++)
                index[i] = (alpha8[i] + 8) / 17;

            bool Ink(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return false;
                var v = index[y * width + x];
                return v >= 1 && v <= 3;
            }

            antiAlias = Mathf.Clamp01(antiAlias);
            var rgba  = new byte[count * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i   = y * width + x;
                    var idx = index[i];
                    Color color;

                    if (idx >= 1 && idx <= 3)
                    {
                        color = BakedFill[idx - 1];
                    }
                    else
                    {
                        var bridge = (Ink(x - 1, y) || Ink(x + 1, y)) && (Ink(x, y - 1) || Ink(x, y + 1));
                        var veil   = (Color)BakedFill[1];
                        veil.a     = bridge ? antiAlias : 0f;
                        var shadow = idx == IndexShadow ? (Color)BakedShadow : new Color(0f, 0f, 0f, 0f);

                        // "over": the bridge on top of the shadow behind it.
                        var outAlpha = veil.a + shadow.a * (1f - veil.a);
                        color = outAlpha <= 0f
                            ? new Color(1f, 1f, 1f, 0f)
                            : new Color(
                                (veil.r * veil.a + shadow.r * shadow.a * (1f - veil.a)) / outAlpha,
                                (veil.g * veil.a + shadow.g * shadow.a * (1f - veil.a)) / outAlpha,
                                (veil.b * veil.a + shadow.b * shadow.a * (1f - veil.a)) / outAlpha,
                                outAlpha);
                    }

                    rgba[i * 4]     = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
                    rgba[i * 4 + 1] = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
                    rgba[i * 4 + 2] = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
                    rgba[i * 4 + 3] = (byte)Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
                }
            }

            return rgba;
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
        private static byte[] BuildIndexAtlas(byte[] alpha, int width, int height, List<Glyph> glyphs, bool softShadow, bool flatFill, bool softHalo)
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
                            value = softShadow && flatFill ? (byte)2 : FillShade(Ink(x, y + 1), Ink(x - 1, y), h % 100);
                        }
                        else if (softShadow)
                        {
                            // Rows grow upwards, so the pixel under an ink pixel has ink at y + 1.
                            if (Ink(x, y + 1))
                                value = IndexShadow;
                            else if (softHalo && (Ink(x - 1, y) || Ink(x + 1, y) || Ink(x, y - 1)))
                                value = IndexSoft;
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

                        // The soft style has no outline: its shadow and halo already surround the ink.
                        if (!softShadow && TouchesSolid(local, stride, maxY - minY + 1, x - minX, y - minY))
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
        private static string SaveAsset(TMP_FontAsset fontAsset, byte[] indices, int width, int height, Shader shader, string path,
            TextureFormat format = TextureFormat.Alpha8)
        {
            var assetName = Path.GetFileNameWithoutExtension(path);
            var existing  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (existing == null)
            {
                var atlas    = CreateAtlas(assetName, indices, width, height, format);
                var material = new Material(shader) { name = assetName + " Material" };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlas);

                fontAsset.name          = assetName;
                fontAsset.atlasTextures = new[] { atlas };
                fontAsset.material      = material;

                AssetDatabase.CreateAsset(fontAsset, path);
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                AssetDatabase.AddObjectToAsset(material, fontAsset);
                AssetDatabase.SaveAssets();
                return "Asset created.";
            }

            var oldAtlas    = existing.atlasTextures != null && existing.atlasTextures.Length > 0 ? existing.atlasTextures[0] : null;
            var oldMaterial = existing.material;

            EditorUtility.CopySerialized(fontAsset, existing);
            existing.name = assetName;

            if (oldAtlas == null)
            {
                oldAtlas = CreateAtlas(assetName, indices, width, height, format);
                AssetDatabase.AddObjectToAsset(oldAtlas, existing);
            }
            else
            {
                oldAtlas.Reinitialize(width, height, format, false);
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
            return "Asset updated (same GUID, references are kept).";
        }

        private static Texture2D CreateAtlas(string assetName, byte[] indices, int width, int height, TextureFormat format = TextureFormat.Alpha8)
        {
            var atlas = new Texture2D(width, height, format, false)
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
