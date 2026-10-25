#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Decay
{
    // El id de sala viaja en los bits 24-31 del rendering layer de cada renderer (Decay.hlsl
    // lo lee de unity_RenderingLayer). Los bits bajos, que usan luces y decal layers, no se tocan.
    public static class DecayRoomId
    {
        public const int  Shift = 24;
        public const uint Mask  = 0xFFu << Shift;
        public const int  Max   = 255;

        public static int Get(Renderer renderer) => (int)((renderer.renderingLayerMask & Mask) >> Shift);

        public static uint With(uint renderingLayerMask, int roomId) =>
            (renderingLayerMask & ~Mask) | ((uint)Mathf.Clamp(roomId, 0, Max) << Shift);
    }
}
