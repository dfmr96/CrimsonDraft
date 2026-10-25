#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Decay
{
    // Opción "Detalle" del deterioro. Bajo: una sola muestra de ruido por píxel, sin grietas
    // y sin los decals marcados como de detalle. Medio: todo.
    public enum DecayDetail
    {
        Low    = 0,
        Medium = 1,
    }

    public static class DecayQuality
    {
        private static readonly int LowDetailId = Shader.PropertyToID("_DecayLowDetail");

        private static DecayDetail current = DecayDetail.Medium;

        public static DecayDetail Current => current;

        /// <summary>Raised after every Set, so the decals already in the scene can turn on/off.</summary>
        public static event System.Action? Changed;

        public static void Set(DecayDetail detail)
        {
            current = detail;
            Shader.SetGlobalFloat(LowDetailId, detail == DecayDetail.Low ? 1f : 0f);
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            current = DecayDetail.Medium;
            Changed = null;
            Shader.SetGlobalFloat(LowDetailId, 0f);
        }
    }
}
