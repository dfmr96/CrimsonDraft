#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    // Dev-only tool: lets one AimView-shaped panel run every debuff effect independently and
    // continuously, with live Inspector toggles exposing the actual shader data (not just
    // Intensity), bypassing the HpTier/profile system entirely -- so effects can be freely
    // combined and retuned in Play mode without waiting for real combat damage. See
    // Assets/Scenes/Test/AimDebuffPreview.unity.
    //
    // Every material this panel touches is cloned in Awake() so it never shares state with any
    // other preview panel, or with the real Combat.unity AimView -- toggling things here can
    // never bleed into actual gameplay.
    public sealed class AimDebuffPreviewRig : MonoBehaviour
    {
        [Header("Overlay Images (each gets its own cloned material in Awake; disabled entirely when the slot is off)")]
        [SerializeField] private Image vignetteOverlay          = null!;
        [SerializeField] private Image breathingVignetteOverlay = null!;
        [SerializeField] private Image grainOverlay              = null!;
        [SerializeField] private Image desaturationOverlay       = null!;
        [SerializeField] private Image paletteOverlay            = null!;
        [SerializeField] private Image heartbeatOverlay          = null!;

        // The silhouette itself must always stay visible (it's the actual QTE target), so its
        // distort material can't be fully disabled like the overlays -- turning its slots off
        // just zeroes _Aberration/_BlurSize, which the shader treats as an exact passthrough.
        [Header("Silhouette (shared distort material: aberration + blur -- always stays visible)")]
        [SerializeField] private Image silhouetteImage = null!;

        [Header("Aim Bars (auto-started so they oscillate without needing real combat)")]
        [SerializeField] private AimViewController aimView = null!;

        [Header("Effect Slots -- the actual shader data, live")]
        public TunnelVignetteSlot    Vignette                 = new();
        public BreathingVignetteSlot BreathingVignette         = new();
        public GrainSlot             Grain                     = new();
        public TintSlot              Desaturation              = new() { Color = new Color(0.6f, 0.6f, 0.6f) };
        public TintSlot              PaletteShift              = new() { Color = new Color(1f, 0.55f, 0.5f) };
        public HeartbeatSlot         ScreenHeartbeat           = new();
        public PulseDistortSlot      ChromaticAberrationPulse  = new() { PulseHz = 1.4f, MaxAmount = 0.02f };
        public PulseDistortSlot      BlurPulse                 = new() { PulseHz = 0.5f, MaxAmount = 0.03f };

        private static readonly int ColorId      = Shader.PropertyToID("_Color");
        private static readonly int IntensityId  = Shader.PropertyToID("_Intensity");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int ResponseId   = Shader.PropertyToID("_Response");
        private static readonly int ScaleId      = Shader.PropertyToID("_Scale");
        private static readonly int AberrationId = Shader.PropertyToID("_Aberration");
        private static readonly int BlurSizeId   = Shader.PropertyToID("_BlurSize");

        private Material vignetteMat   = null!;
        private Material breathingMat  = null!;
        private Material grainMat      = null!;
        private Material desatMat      = null!;
        private Material paletteMat    = null!;
        private Material heartbeatMat  = null!;
        private Material distortMat    = null!;

        private void Awake()
        {
            this.vignetteMat  = this.CloneInto(this.vignetteOverlay);
            this.breathingMat = this.CloneInto(this.breathingVignetteOverlay);
            this.grainMat     = this.CloneInto(this.grainOverlay);
            this.desatMat     = this.CloneInto(this.desaturationOverlay);
            this.paletteMat   = this.CloneInto(this.paletteOverlay);
            this.heartbeatMat = this.CloneInto(this.heartbeatOverlay);
            this.distortMat   = this.CloneInto(this.silhouetteImage);
        }

        private Material CloneInto(Image image)
        {
            var instance = Instantiate(image.material);
            image.material = instance;
            return instance;
        }

        private void Start()
        {
            // Show() starts the vertical/horizontal oscillation and never stops it on its own --
            // nothing here ever calls Confirm(), so the bars just keep oscillating indefinitely,
            // exactly like a live QTE, without needing a real shot/combat flow.
            this.aimView.Show();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            ApplyTunnelVignette(this.Vignette, this.vignetteMat, this.vignetteOverlay);
            ApplyBreathingVignette(this.BreathingVignette, this.breathingMat, this.breathingVignetteOverlay, dt);
            ApplyGrain(this.Grain, this.grainMat, this.grainOverlay);
            ApplyTint(this.Desaturation, this.desatMat, this.desaturationOverlay);
            ApplyTint(this.PaletteShift, this.paletteMat, this.paletteOverlay);
            ApplyHeartbeat(this.ScreenHeartbeat, this.heartbeatMat, this.heartbeatOverlay, dt);
            ApplyAberration(this.ChromaticAberrationPulse, this.distortMat, dt);
            ApplyBlur(this.BlurPulse, this.distortMat, dt);
        }

        private static void ApplyTunnelVignette(TunnelVignetteSlot slot, Material mat, Image overlay)
        {
            overlay.enabled = slot.Enabled;
            if (!slot.Enabled) return;

            mat.SetColor(ColorId, slot.Color);
            mat.SetFloat(SmoothnessId, slot.Smoothness);
            mat.SetFloat(IntensityId, slot.Intensity);
        }

        private static void ApplyBreathingVignette(BreathingVignetteSlot slot, Material mat, Image overlay, float dt)
        {
            overlay.enabled = slot.Enabled;
            if (!slot.Enabled)
            {
                slot.ElapsedTotal = 0f;
                return;
            }

            slot.ElapsedTotal += dt;
            float breath = 1f - slot.BreathDepth * 0.5f * (1f + Mathf.Sin(slot.ElapsedTotal * slot.BreathHz * Mathf.PI * 2f));

            mat.SetColor(ColorId, slot.Color);
            mat.SetFloat(SmoothnessId, slot.Smoothness);
            mat.SetFloat(IntensityId, slot.Intensity * breath);
        }

        private static void ApplyGrain(GrainSlot slot, Material mat, Image overlay)
        {
            overlay.enabled = slot.Enabled;
            if (!slot.Enabled) return;

            mat.SetColor(ColorId, slot.Color);
            mat.SetFloat(ResponseId, slot.Response);
            mat.SetFloat(ScaleId, slot.Scale);
            mat.SetFloat(IntensityId, slot.Intensity);
        }

        private static void ApplyTint(TintSlot slot, Material mat, Image overlay)
        {
            overlay.enabled = slot.Enabled;
            if (!slot.Enabled) return;

            mat.SetColor(ColorId, slot.Color);
            mat.SetFloat(IntensityId, slot.Intensity);
        }

        private static void ApplyHeartbeat(HeartbeatSlot slot, Material mat, Image overlay, float dt)
        {
            overlay.enabled = slot.Enabled;
            if (!slot.Enabled)
            {
                slot.ElapsedTotal = 0f;
                return;
            }

            slot.ElapsedTotal += dt;
            float beatInterval = Mathf.Lerp(slot.CalmBeatInterval, slot.PanicBeatInterval, slot.Intensity);
            int   beatIndex    = Mathf.FloorToInt(slot.ElapsedTotal / beatInterval);
            float cycleT       = slot.ElapsedTotal - beatIndex * beatInterval;

            float lub  = PulseEnvelope(cycleT, slot.PulseDecay);
            float dub  = PulseEnvelope(cycleT - beatInterval * slot.DubOffsetFraction, slot.PulseDecay) * slot.DubStrength;
            float beat = Mathf.Max(lub, dub);

            mat.SetColor(ColorId, slot.Color);
            mat.SetFloat(IntensityId, slot.Intensity * beat);
        }

        // Silhouette stays visible regardless -- Enabled=false just zeroes _Aberration, an exact
        // passthrough per AimDebuffDistort.shader.
        private static void ApplyAberration(PulseDistortSlot slot, Material mat, float dt)
        {
            if (!slot.Enabled)
            {
                mat.SetFloat(AberrationId, 0f);
                slot.ElapsedTotal = 0f;
                return;
            }

            slot.ElapsedTotal += dt;
            float pulse = Mathf.Abs(Mathf.Sin(slot.ElapsedTotal * slot.PulseHz * Mathf.PI));
            mat.SetFloat(AberrationId, slot.Intensity * slot.MaxAmount * pulse);
        }

        // Silhouette stays visible regardless -- Enabled=false just zeroes _BlurSize, an exact
        // passthrough per AimDebuffDistort.shader.
        private static void ApplyBlur(PulseDistortSlot slot, Material mat, float dt)
        {
            if (!slot.Enabled)
            {
                mat.SetFloat(BlurSizeId, 0f);
                slot.ElapsedTotal = 0f;
                return;
            }

            slot.ElapsedTotal += dt;
            float pulse = 0.5f * (1f + Mathf.Sin(slot.ElapsedTotal * slot.PulseHz * Mathf.PI * 2f));
            mat.SetFloat(BlurSizeId, slot.Intensity * slot.MaxAmount * pulse);
        }

        private static float PulseEnvelope(float t, float decay) => t < 0f ? 0f : Mathf.Exp(-decay * t);
    }
}
