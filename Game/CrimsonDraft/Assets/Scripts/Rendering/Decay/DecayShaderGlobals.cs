#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CrimsonDraft.Rendering.Decay
{
    // Sube los DecayVolume activos a los globales que lee Decay.hlsl. Se hace justo antes de
    // renderizar: si hay más salas que MaxVolumes (un deck entero cargado), se mandan las más
    // cercanas a la cámara, que son las únicas que se pueden estar viendo con cámaras fijas.
    // Las salas sin id (0) no se suben: ningún renderer las referencia.
    public static class DecayShaderGlobals
    {
        public const int MaxVolumes = 16; // tiene que coincidir con DECAY_MAX_VOLUMES

        private static readonly int CountId         = Shader.PropertyToID("_DecayVolumeCount");
        private static readonly int WorldToVolumeId = Shader.PropertyToID("_DecayWorldToVolume");
        private static readonly int SizeId          = Shader.PropertyToID("_DecayVolumeSize");
        private static readonly int AmountsId       = Shader.PropertyToID("_DecayAmounts");
        private static readonly int BaseboardId     = Shader.PropertyToID("_DecayBaseboard");
        private static readonly int ExtraId         = Shader.PropertyToID("_DecayExtra");
        private static readonly int NoiseTexId      = Shader.PropertyToID("_DecayNoiseTex");
        private static readonly int TexScaleId      = Shader.PropertyToID("_DecayTexScale");
        private static readonly int DirtColorId     = Shader.PropertyToID("_DecayDirtColor");
        private static readonly int DampColorId     = Shader.PropertyToID("_DecayDampColor");
        private static readonly int MoldColorId     = Shader.PropertyToID("_DecayMoldColor");
        private static readonly int RustColorId     = Shader.PropertyToID("_DecayRustColor");
        private static readonly int DustColorId     = Shader.PropertyToID("_DecayDustColor");
        private static readonly int BareColorId     = Shader.PropertyToID("_DecayBareColor");

        private static readonly Matrix4x4[]       matrices  = new Matrix4x4[MaxVolumes];
        private static readonly Vector4[]         sizes     = new Vector4[MaxVolumes];
        private static readonly Vector4[]         amounts   = new Vector4[MaxVolumes];
        private static readonly Vector4[]         baseboard = new Vector4[MaxVolumes];
        private static readonly Vector4[]         extra     = new Vector4[MaxVolumes];
        private static readonly List<DecayVolume> sorted    = new();

        private static bool    registered;
        private static bool    dirty = true;
        private static Vector3 lastCameraPosition = new(float.NaN, 0f, 0f);

        public static void MarkDirty() => dirty = true;

        internal static void Register()
        {
            if (registered) return;
            registered = true;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }

        internal static void Unregister()
        {
            if (!registered) return;
            registered = false;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            Shader.SetGlobalInt(CountId, 0);
        }

        private static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            var volumes = DecayVolume.Active;
            Vector3 cameraPosition = cameras.Count > 0 && cameras[0] != null ? cameras[0].transform.position : Vector3.zero;

            // Con pocas salas el orden no importa; con muchas, mover la cámara cambia cuáles entran.
            bool cameraMatters = volumes.Count > MaxVolumes && cameraPosition != lastCameraPosition;
            if (!dirty && !cameraMatters) return;

            dirty = false;
            lastCameraPosition = cameraPosition;
            Upload(volumes, cameraPosition);
        }

        private static void Upload(IReadOnlyList<DecayVolume> volumes, Vector3 cameraPosition)
        {
            sorted.Clear();
            for (int i = 0; i < volumes.Count; i++)
                if (volumes[i].RoomId > 0) sorted.Add(volumes[i]);
            if (sorted.Count > MaxVolumes)
                sorted.Sort((a, b) => DistanceTo(a, cameraPosition).CompareTo(DistanceTo(b, cameraPosition)));

            int count = Mathf.Min(sorted.Count, MaxVolumes);
            DecayPalette? palette = null;

            for (int i = 0; i < count; i++)
            {
                var v = sorted[i];
                if (palette == null) palette = v.Palette;

                Vector3 worldSize = v.WorldSize;
                matrices[i]  = v.WorldToVolume;
                sizes[i]     = new Vector4(worldSize.x, worldSize.y, worldSize.z, v.RoomId);
                amounts[i]   = new Vector4(v.Dirt, v.Damp, v.Rust, v.Paint) * v.Decay;
                baseboard[i] = new Vector4(v.BaseboardHeight, v.BaseboardSoftness, v.BaseboardNoise, v.BaseboardStrength);
                extra[i]     = new Vector4(v.CornerSize, v.CeilingStreakLength, v.Mold, v.Dust);
            }

            Shader.SetGlobalInt(CountId, palette != null && palette.NoiseTexture != null ? count : 0);
            Shader.SetGlobalMatrixArray(WorldToVolumeId, matrices);
            Shader.SetGlobalVectorArray(SizeId, sizes);
            Shader.SetGlobalVectorArray(AmountsId, amounts);
            Shader.SetGlobalVectorArray(BaseboardId, baseboard);
            Shader.SetGlobalVectorArray(ExtraId, extra);

            if (palette == null) return;
            if (palette.NoiseTexture != null) Shader.SetGlobalTexture(NoiseTexId, palette.NoiseTexture);
            Shader.SetGlobalFloat(TexScaleId, palette.TextureScale);
            Shader.SetGlobalColor(DirtColorId, palette.DirtColor);
            Shader.SetGlobalColor(DampColorId, palette.DampColor);
            Shader.SetGlobalColor(MoldColorId, palette.MoldColor);
            Shader.SetGlobalColor(RustColorId, palette.RustColor);
            Shader.SetGlobalColor(DustColorId, palette.DustColor);
            Shader.SetGlobalColor(BareColorId, palette.BareColor);
        }

        private static float DistanceTo(DecayVolume volume, Vector3 point)
        {
            Vector3 local   = volume.WorldToVolume.MultiplyPoint3x4(point);
            Vector3 outside = Vector3.Max(new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z)) - Vector3.one * 0.5f, Vector3.zero);
            return Vector3.Scale(outside, volume.WorldSize).sqrMagnitude;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            dirty = true;
            lastCameraPosition = new Vector3(float.NaN, 0f, 0f);
        }
    }
}
