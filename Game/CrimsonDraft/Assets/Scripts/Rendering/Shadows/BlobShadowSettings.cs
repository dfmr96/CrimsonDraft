#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Shadows
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Blob Shadow Settings", fileName = "BlobShadowSettings")]
    public sealed class BlobShadowSettings : ScriptableObject
    {
        [Header("Ground")]
        [SerializeField] private LayerMask groundMask;
        [SerializeField, Min(0f)] private float rayStartHeight = 0.25f;
        [SerializeField, Min(0f)] private float rayDistance    = 2f;
        [SerializeField, Min(0f)] private float surfaceBias    = 0.01f;

        [Header("Shape")]
        [SerializeField, Tooltip("x = width across the shoulders, y = depth front-to-back (meters)")]
        private Vector2 size = new Vector2(0.85f, 0.5f);

        [Header("Footstep Pulse")]
        [SerializeField, Min(0f)]    private float walkPulseAmplitude = 0.25f;
        [SerializeField, Min(0f)]    private float runPulseAmplitude  = 0.4f;
        [SerializeField, Min(0.01f)] private float pulseDuration      = 0.3f;

        public LayerMask GroundMask     => this.groundMask;
        public float     RayStartHeight => this.rayStartHeight;
        public float     RayDistance    => this.rayDistance;
        public float     SurfaceBias    => this.surfaceBias;
        public Vector2   Size           => this.size;
        public float     PulseDuration  => this.pulseDuration;

        public float PulseAmplitude(FootstepPace pace)
            => pace == FootstepPace.Run ? this.runPulseAmplitude : this.walkPulseAmplitude;
    }
}
