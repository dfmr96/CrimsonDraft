#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Decay
{
    // Look global del deterioro: colores y textura de ruido que comparten todas las salas.
    // Lo que cambia sala por sala (cuánto deterioro, zócalo, etc.) vive en DecayVolume.
    [CreateAssetMenu(menuName = "CrimsonDraft/Decay/Palette", fileName = "DecayPalette")]
    public sealed class DecayPalette : ScriptableObject
    {
        [Tooltip("R manchas, G grano, B chorreaduras verticales, A grietas. La genera la ventana Decay Tool, pero se puede reemplazar por una propia.")]
        [SerializeField] private Texture2D? noiseTexture;
        [SerializeField, Min(0.01f)] private float textureScale = 0.35f; // repeticiones por metro

        [Header("Colores")]
        [SerializeField] private Color dirtColor = new(0.30f, 0.24f, 0.18f);  // multiplica
        [SerializeField] private Color dampColor = new(0.50f, 0.44f, 0.30f);  // multiplica
        [SerializeField] private Color moldColor = new(0.22f, 0.27f, 0.16f);
        [SerializeField] private Color rustColor = new(0.45f, 0.20f, 0.08f);
        [SerializeField] private Color dustColor = new(0.55f, 0.52f, 0.47f);
        [Tooltip("Lo que aparece debajo de la pintura descascarada. Alpha = cuánto reemplaza al color original.")]
        [SerializeField] private Color bareColor = new(0.36f, 0.35f, 0.33f, 0.7f);

        public Texture2D? NoiseTexture { get => this.noiseTexture; set => this.noiseTexture = value; }
        public float      TextureScale => this.textureScale;
        public Color      DirtColor    => this.dirtColor;
        public Color      DampColor    => this.dampColor;
        public Color      MoldColor    => this.moldColor;
        public Color      RustColor    => this.rustColor;
        public Color      DustColor    => this.dustColor;
        public Color      BareColor    => this.bareColor;

        private void OnValidate() => DecayShaderGlobals.MarkDirty();
    }
}
