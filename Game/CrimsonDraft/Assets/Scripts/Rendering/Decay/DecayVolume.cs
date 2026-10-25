#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Rendering.Decay
{
    // El deterioro de una sala. Vive dentro del prefab de la sala (hijo "Decay" de su raíz) y
    // solo afecta a los renderers de ese prefab: la Decay Tool les escribe roomId en el
    // rendering layer (ver DecayRoomId). La caja no decide qué se ensucia, solo dónde están
    // el piso, el techo y las paredes; se asume vertical (solo rotación en Y).
    [ExecuteAlways]
    public sealed class DecayVolume : MonoBehaviour
    {
        private static readonly List<DecayVolume> active = new();
        public static IReadOnlyList<DecayVolume> Active => active;

        [SerializeField] private DecayPalette? palette;

        [Tooltip("Lo asigna la Decay Tool; único por sala (1-255).")]
        [SerializeField] private int roomId;

        [Tooltip("Partes de la sala que no se ensucian (por defecto Interactables: ítems, notas).")]
        [SerializeField] private List<Transform> excluded = new();

        [Header("Caja")]
        [SerializeField] private Vector3 center = Vector3.zero;
        [SerializeField] private Vector3 size   = new(6f, 3f, 6f);

        [Header("Deterioro")]
        [SerializeField, Range(0f, 1f)] private float decay = 0.5f;
        [SerializeField, Range(0f, 1f)] private float dirt  = 1f;   // mugre en zócalo, esquinas y piso
        [SerializeField, Range(0f, 1f)] private float damp  = 0.6f; // filtraciones, humedad que sube, moho
        [SerializeField, Range(0f, 1f)] private float rust  = 0.6f; // solo en materiales con DecayMetal > 0
        [SerializeField, Range(0f, 1f)] private float paint = 0.5f; // pintura descascarada y grietas

        [Header("Zócalo (pared/suelo)")]
        [SerializeField, Range(0f, 2f)] private float baseboardHeight   = 0.35f; // metros
        [SerializeField, Range(0f, 1f)] private float baseboardSoftness = 0.7f;  // 0 = línea dura, 1 = gradiente completo
        [SerializeField, Range(0f, 1f)] private float baseboardNoise    = 0.5f;  // irregularidad del borde
        [SerializeField, Range(0f, 1f)] private float baseboardStrength = 1f;

        [Header("Detalles")]
        [SerializeField, Range(0.05f, 2f)] private float cornerSize           = 0.6f;
        [SerializeField, Range(0f, 3f)]    private float ceilingStreakLength  = 1.2f;
        [SerializeField, Range(0f, 1f)]    private float mold                 = 0.5f;
        [SerializeField, Range(0f, 1f)]    private float dust                 = 0.5f;

        [Header("Decals")]
        [SerializeField] private int seed = 1;
        [SerializeField, Range(0f, 3f)] private float decalDensity = 1f;

        public DecayPalette? Palette { get => this.palette; set { this.palette = value; DecayShaderGlobals.MarkDirty(); } }
        public int RoomId { get => this.roomId; set { this.roomId = value; DecayShaderGlobals.MarkDirty(); } }
        public List<Transform> Excluded => this.excluded;
        public Vector3 Center { get => this.center; set { this.center = value; DecayShaderGlobals.MarkDirty(); } }
        public Vector3 Size   { get => this.size;   set { this.size = value;   DecayShaderGlobals.MarkDirty(); } }

        /// <summary>Raíz del prefab de la sala: el volumen es su hijo directo.</summary>
        public Transform Room => this.transform.parent != null ? this.transform.parent : this.transform;

        public float Decay => this.decay;
        public float Dirt  => this.dirt;
        public float Damp  => this.damp;
        public float Rust  => this.rust;
        public float Paint => this.paint;

        public float BaseboardHeight   => this.baseboardHeight;
        public float BaseboardSoftness => this.baseboardSoftness;
        public float BaseboardNoise    => this.baseboardNoise;
        public float BaseboardStrength => this.baseboardStrength;

        public float CornerSize          => this.cornerSize;
        public float CeilingStreakLength => this.ceilingStreakLength;
        public float Mold                => this.mold;
        public float Dust                => this.dust;

        public int   Seed         { get => this.seed; set => this.seed = value; }
        public float DecalDensity => this.decalDensity;

        /// <summary>Tamaño real de la caja en metros (incluye la escala; las salas espejadas dan escala negativa).</summary>
        public Vector3 WorldSize
        {
            get
            {
                Vector3 s = Vector3.Scale(this.size, this.transform.lossyScale);
                return new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
        }

        public Vector3 WorldCenter => this.transform.TransformPoint(this.center);

        /// <summary>Mundo -> caja unitaria [-0.5, 0.5], que es lo que espera Decay.hlsl.</summary>
        public Matrix4x4 WorldToVolume
        {
            get
            {
                var volumeToWorld = Matrix4x4.TRS(this.WorldCenter, this.transform.rotation, Vector3.Max(this.WorldSize, Vector3.one * 0.001f));
                return volumeToWorld.inverse;
            }
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = this.WorldToVolume.MultiplyPoint3x4(worldPoint);
            return Mathf.Abs(local.x) <= 0.5f && Mathf.Abs(local.y) <= 0.5f && Mathf.Abs(local.z) <= 0.5f;
        }

        public bool IsExcluded(Transform t)
        {
            foreach (var e in this.excluded)
                if (e != null && t.IsChildOf(e)) return true;
            return false;
        }

        private void OnEnable()
        {
            active.Add(this);
            DecayShaderGlobals.Register();
            DecayShaderGlobals.MarkDirty();
        }

        private void OnDisable()
        {
            active.Remove(this);
            DecayShaderGlobals.MarkDirty();
            if (active.Count == 0) DecayShaderGlobals.Unregister();
        }

        private void OnValidate() => DecayShaderGlobals.MarkDirty();

        private void Update()
        {
            if (!this.transform.hasChanged) return;
            this.transform.hasChanged = false;
            DecayShaderGlobals.MarkDirty();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color  = new Color(0.9f, 0.5f, 0.2f, 1f);
            Gizmos.matrix = Matrix4x4.TRS(this.WorldCenter, this.transform.rotation, this.WorldSize);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
