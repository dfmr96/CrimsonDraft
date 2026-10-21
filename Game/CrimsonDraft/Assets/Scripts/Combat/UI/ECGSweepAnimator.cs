#nullable enable

using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    [DisallowMultipleComponent]
    public sealed class ECGSweepAnimator : MonoBehaviour
    {
        #region Fields

        private static readonly int HeadId = Shader.PropertyToID("_Head");
        private static readonly int TrailLengthId = Shader.PropertyToID("_TrailLength");
        private static readonly int FadeExponentId = Shader.PropertyToID("_FadeExponent");
        private static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
        private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
        private static readonly int GlitchSeedId = Shader.PropertyToID("_GlitchSeed");
        private static readonly int GlitchTintId = Shader.PropertyToID("_GlitchTint");

        [SerializeField] private Image traceImage = null!;
        [SerializeField] private float sweepDuration = 2f;
        [SerializeField, Min(0f)] private float restDuration = 0f;
        [SerializeField, Range(0.01f, 0.5f)] private float trailFraction = 0.18f;
        [SerializeField, Range(0.1f, 4f)] private float fadeExponent = 1f;

        // Shared with the aim debuff system (AimViewController) so the sprite the player sees
        // here and the aim difficulty debuffs always agree on what "Yellow/Orange/Danger" means --
        // null falls back to this component's original hardcoded 0.75/0.50/0.25 breakpoints.
        [SerializeField] private HpTierConfig? hpTierConfig;

        // Four stages that match the status text (Fine / Caution / Danger / Critical) plus KIA.
        // The serialized names are older and stay as they are so prefab overrides keep working:
        // Stable = Fine, Warning = Danger, Severe = Critical. Mercy is a combat-only fifth look
        // (0 HP, still alive); outside combat it does not exist -- see SetHealthState.
        [Header("Health States (Fine / Caution / Danger / Critical, KIA; Mercy is combat-only)")]
        [Tooltip("Fine: 75-100%")]
        [SerializeField] private Sprite? stageSpriteStable;
        [Tooltip("Caution: 50-75%")]
        [SerializeField] private Sprite? stageSpriteCaution;
        [Tooltip("Danger: 25-50%")]
        [SerializeField] private Sprite? stageSpriteWarning;
        [Tooltip("Critical: 0-25%, alive")]
        [SerializeField] private Sprite? stageSpriteSevere;
        [SerializeField, FormerlySerializedAs("stageSpriteCritical")] private Sprite? stageSpriteMercy; // Mercy: 0 HP, alive, fast/erratic
        [SerializeField] private Sprite? stageSpriteDead;     // KIA: static flatline

        [SerializeField] private float stageDurationStable  = 3f;
        [SerializeField] private float stageDurationCaution = 2f;
        [SerializeField] private float stageDurationWarning = 1.2f;
        [SerializeField] private float stageDurationSevere   = 0.9f;
        [SerializeField, FormerlySerializedAs("stageDurationCritical")] private float stageDurationMercy = 0.6f;

        // Soft color-matched glow sitting behind the trace so the ECG's own backdrop isn't
        // flat black — a gradient sprite tinted per health band instead of a fixed color,
        // so it reads at a glance which of the 5 stages (4 normal + Mercy) is currently active.
        [Header("Background Effect (tints the gradient 'Effect' image behind the trace)")]
        [SerializeField] private Image? effectImage;
        [SerializeField] private Color effectColorStable   = new(0.4901961f, 0.7058824f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorCaution   = new(0.6901961f, 0.7058824f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorWarning   = new(0.6901961f, 0.5568628f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorSevere    = new(0.70980394f, 0.5019608f, 0.2862745f, 0.55f);
        [SerializeField, FormerlySerializedAs("effectColorCritical")] private Color effectColorMercy  = new(0.73333335f, 0.44705883f, 0.26666668f, 0.55f);

        [Header("Status Text (CDG_Status: Fine / Caution / Danger / Critical)")]
        [SerializeField] private TMP_Text? statusLabel;

        [Header("Damage Glitch (CRT signal-loss burst)")]
        [SerializeField, Min(0f)] private float glitchDuration = 0.35f;
        [SerializeField, Min(0f)] private float glitchJitterAmount = 6f;
        [SerializeField, Range(0f, 1f)] private float glitchMinAlpha = 0.15f;
        [Tooltip("Colour the trace and its glow flash toward when hit (fades back over the glitch).")]
        [SerializeField] private Color glitchTint = new(1f, 0.2f, 0.15f, 1f);
        [SerializeField, Range(0f, 1f)] private float glitchTintStrength = 0.9f;
        [Tooltip("How many times per second the torn bands / fringes change during the glitch.")]
        [SerializeField, Min(1f)] private float glitchSeedRate = 24f;
        [Tooltip("Vertical shake of the whole panel during the glitch, in pixels.")]
        [SerializeField, Min(0f)] private float glitchJitterAmountY = 2f;

        [Header("Damage Glitch - whole panel")]
        [Tooltip("Tear / ghost-fringe shader (CrimsonDraft/UI/GlitchPanel) applied to the frame sprites while glitching. Falls back to Shader.Find if unset.")]
        [SerializeField] private Shader? glitchPanelShader;
        [Tooltip("Graphics that make up the box around the ECG (frame, backdrop, status text). Empty = every Image/Text under this object except the trace and its glow.")]
        [SerializeField] private Graphic[] glitchPanelGraphics = System.Array.Empty<Graphic>();
        [SerializeField, Range(0f, 1f)] private float glitchPanelMinAlpha = 0.45f;

        // Mercy (0 HP, still alive): the sprite and its glow breathe in and out like a
        // warning beacon instead of sitting at a flat tint, so the card visibly flags
        // "needs healing" instead of just looking like another low-HP band.
        [Header("Mercy Pulse (needs-healing warning)")]
        [SerializeField, Min(0.01f), FormerlySerializedAs("criticalPulseSpeed")] private float mercyPulseSpeed = 2.5f; // pulses/sec
        [SerializeField, Range(0f, 1f), FormerlySerializedAs("criticalPulseMinAlpha")] private float mercyPulseMinAlpha = 0.35f;

        private float t;
        private bool isResting;
        private bool isFlatlined;
        private bool isMercy;
        private float restTimer;
        private Material? sourceMaterial;
        private Material? runtimeMaterial;

        // Whatever alpha the effect Image was left at in the Editor -- SetHealthState only
        // ever retints RGB per band, it never overwrites this, so a designer's opacity
        // tweak on the Image component survives entering Play instead of being clobbered
        // by the hardcoded 0.55f baked into effectColorStable/Caution/Warning/Mercy.
        private float effectBaseAlpha = 1f;
        private bool  effectBaseAlphaCaptured;

        private RectTransform? glitchRect;
        private Vector2 glitchRestPosition;
        private Color glitchTraceRestColor;
        private Color glitchEffectRestColor;
        private bool glitchInitialized;
        private bool isGlitching;
        private float glitchTimer;

        private Graphic[] glitchPanelResolved = System.Array.Empty<Graphic>();
        private Color[] glitchPanelRestColors = System.Array.Empty<Color>();
        private Material?[] glitchPanelRestMaterials = System.Array.Empty<Material?>();
        private Material?[] glitchPanelMaterials = System.Array.Empty<Material?>();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.sourceMaterial = this.traceImage.material;
            this.EnsureEffectBaseAlphaCaptured();
        }

        // Unity doesn't guarantee Awake() order across different GameObjects -- if
        // CombatActionMenuView reaches SetHealthState() on a freshly-instantiated card
        // before this component's own Awake() has run, effectBaseAlpha was still sitting at
        // its C# default (1f), and SetHealthState would paint the effect fully opaque with
        // that default -- which Awake() would then read back as the "authored" alpha,
        // permanently baking the bug in. Capturing lazily on first use (from whichever of
        // Awake()/SetHealthState() runs first) makes the result independent of that order.
        private void EnsureEffectBaseAlphaCaptured()
        {
            if (this.effectBaseAlphaCaptured || this.effectImage == null) return;
            this.effectBaseAlpha = this.effectImage.color.a;
            this.effectBaseAlphaCaptured = true;
        }

        private void OnEnable()
        {
            this.t = 0f;
            this.isResting = false;
            this.restTimer = 0f;
            if (!this.isFlatlined && !this.isMercy)
                this.ApplySweep();
        }

        private void Update()
        {
            if (this.isGlitching)
                this.UpdateGlitch();
            else if (this.isMercy)
                this.UpdateMercyPulse();

            // Mercy shows only the on/off pulse -- no scanning sweep, same as KIA's flatline.
            if (this.isFlatlined || this.isMercy) return;

            if (this.isResting)
            {
                this.restTimer += Time.unscaledDeltaTime;
                if (this.restTimer < this.restDuration)
                {
                    return;
                }

                this.isResting = false;
                this.t = 0f;
            }
            else
            {
                this.t += Time.unscaledDeltaTime / this.sweepDuration;
                if (this.t >= 1f)
                {
                    this.t = 1f;
                    if (this.restDuration > 0f)
                    {
                        this.isResting = true;
                        this.restTimer = 0f;
                    }
                    else
                    {
                        this.t = 0f;
                    }
                }
            }

            this.ApplySweep();
        }

        #endregion

        #region Health State

        // isAlive distinguishes Mercy (0 HP, still alive -- one more hit from KIA) from a
        // confirmed kill: hpRatio alone can't tell them apart since both sit at 0.
        // mercyApplies is false outside combat (inventory cards): there 0 HP alive is just the
        // Critical stage, and the only thing after Critical is KIA.
        public void SetHealthState(float hpRatio, bool isAlive, bool mercyApplies = true)
        {
            this.EnsureEffectBaseAlphaCaptured();
            hpRatio = Mathf.Clamp01(hpRatio);

            if (!isAlive)
            {
                // KIA: solid, no sweep, no pulse, no glow -- just the flatline.
                this.isMercy = false;
                this.isFlatlined = true;
                this.ApplyStatusLabel(OperatorHealthStatus.Critical);
                if (this.stageSpriteDead != null)
                    this.traceImage.sprite = this.stageSpriteDead;
                this.traceImage.material = null;
                this.traceImage.color = Color.white;
                if (this.effectImage != null)
                    this.effectImage.enabled = false;
                return;
            }

            if (this.isFlatlined)
            {
                this.isFlatlined = false;
                this.GetRuntimeMaterial();
                if (this.effectImage != null)
                    this.effectImage.enabled = true;
            }

            Sprite? sprite;
            float duration;
            Color effectColor;

            OperatorHealthStatus status;
            if (hpRatio <= 0f && mercyApplies)
            {
                status = OperatorHealthStatus.Critical;
                sprite = this.stageSpriteMercy; duration = this.stageDurationMercy; effectColor = this.effectColorMercy;
            }
            else
            {
                HpTier tier = this.hpTierConfig != null
                    ? HpTierCalculator.ComputeTier(hpRatio, this.hpTierConfig)
                    : LegacyComputeTier(hpRatio);

                switch (tier)
                {
                    case HpTier.Danger: status = OperatorHealthStatus.Critical; sprite = this.stageSpriteSevere;  duration = this.stageDurationSevere;  effectColor = this.effectColorSevere;  break;
                    case HpTier.Orange: status = OperatorHealthStatus.Danger;   sprite = this.stageSpriteWarning; duration = this.stageDurationWarning; effectColor = this.effectColorWarning; break;
                    case HpTier.Yellow: status = OperatorHealthStatus.Caution;  sprite = this.stageSpriteCaution; duration = this.stageDurationCaution; effectColor = this.effectColorCaution; break;
                    default:            status = OperatorHealthStatus.Fine;     sprite = this.stageSpriteStable; duration = this.stageDurationStable;  effectColor = this.effectColorStable;  break;
                }
            }

            this.ApplyStatusLabel(status);

            if (sprite != null)
                this.traceImage.sprite = sprite;

            this.sweepDuration = duration;
            this.isMercy       = mercyApplies && hpRatio <= 0f;

            // Mercy drops the scanning-sweep material entirely -- only the pulse (in
            // UpdateMercyPulse) drives it, on/off, no barrido.
            if (this.isMercy)
                this.traceImage.material = null;
            else
                this.traceImage.color = Color.white; // clear any pulse dimming left from Mercy

            if (this.effectImage != null)
            {
                effectColor.a = this.effectBaseAlpha;
                this.effectImage.color = effectColor;
            }
        }

        private void ApplyStatusLabel(OperatorHealthStatus status)
        {
            if (this.statusLabel == null) return;
            this.statusLabel.text  = OperatorHealthStatusStyle.GetLabel(status);
            var color = OperatorHealthStatusStyle.GetColor(status);
            color.a = this.statusLabel.color.a;
            this.statusLabel.color = color;
        }

        // Only used when hpTierConfig isn't assigned -- reproduces this component's original
        // breakpoints (0.75/0.50/0.25) exactly, so unwired instances keep their old behavior.
        private static HpTier LegacyComputeTier(float hpRatio)
        {
            if (hpRatio <= 0.25f) return HpTier.Danger;
            if (hpRatio <= 0.50f) return HpTier.Orange;
            if (hpRatio <= 0.75f) return HpTier.Yellow;
            return HpTier.Full;
        }

        // Breathes the trace sprite and its background glow between full brightness and
        // mercyPulseMinAlpha so Mercy visibly nags at the player instead of sitting
        // still like a normal (if low) health band.
        private void UpdateMercyPulse()
        {
            float wave   = (Mathf.Sin(Time.unscaledTime * this.mercyPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha  = Mathf.Lerp(this.mercyPulseMinAlpha, 1f, wave);

            var traceColor = Color.white;
            traceColor.a = alpha;
            this.traceImage.color = traceColor;

            if (this.effectImage != null)
            {
                var effectColor = this.effectColorMercy;
                effectColor.a = this.effectBaseAlpha * alpha;
                this.effectImage.color = effectColor;
            }
        }

        #endregion

        #region Damage Glitch

        // Fire-and-forget CRT-static burst on top of whatever the sweep/health state is
        // currently doing -- an old TV losing signal: the trace line and its glow tear
        // horizontally and flicker in opacity for a moment, then snap back to rest. Runs
        // even while flatlined, since Update() checks isGlitching before the isFlatlined
        // early-return above.
        public void PlayDamageGlitch()
        {
            this.EnsureGlitchInitialized();
            if (!this.isGlitching)
                this.BeginPanelGlitch();
            this.isGlitching = true;
            this.glitchTimer = 0f;
        }

        // Swaps the frame sprites onto the tear shader (one material instance each, since the
        // shader needs each sprite's own UV rect) and remembers what to put back afterwards.
        private void BeginPanelGlitch()
        {
            if (this.glitchPanelGraphics.Length > 0)
                this.glitchPanelResolved = this.glitchPanelGraphics;
            else
            {
                var found = new System.Collections.Generic.List<Graphic>();
                foreach (var g in GetComponentsInChildren<Graphic>(true))
                {
                    if (g == this.traceImage || g == this.effectImage) continue;
                    found.Add(g);
                }
                this.glitchPanelResolved = found.ToArray();
            }

            var shader = this.glitchPanelShader != null ? this.glitchPanelShader : Shader.Find("CrimsonDraft/UI/GlitchPanel");
            int n = this.glitchPanelResolved.Length;
            this.glitchPanelRestColors    = new Color[n];
            this.glitchPanelRestMaterials = new Material?[n];
            this.glitchPanelMaterials     = new Material?[n];

            for (int i = 0; i < n; i++)
            {
                var g = this.glitchPanelResolved[i];
                this.glitchPanelRestColors[i] = g.color;
                if (g is not Image img || shader == null) continue;

                this.glitchPanelRestMaterials[i] = img.material;
                var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetVector(SpriteRectId, GetSpriteUvRect(img.sprite));
                img.material = mat;
                this.glitchPanelMaterials[i] = mat;
            }
        }

        private void EndPanelGlitch()
        {
            for (int i = 0; i < this.glitchPanelResolved.Length; i++)
            {
                var g = this.glitchPanelResolved[i];
                if (g == null) continue;
                g.color = this.glitchPanelRestColors[i];
                if (g is Image img && this.glitchPanelMaterials[i] != null)
                {
                    img.material = this.glitchPanelRestMaterials[i];
                    Destroy(this.glitchPanelMaterials[i]);
                    this.glitchPanelMaterials[i] = null;
                }
            }
        }

        private void UpdatePanelGlitch(float amount, float step, float tintMix)
        {
            for (int i = 0; i < this.glitchPanelResolved.Length; i++)
            {
                var g = this.glitchPanelResolved[i];
                if (g == null) continue;

                var color = TintTowards(this.glitchPanelRestColors[i], this.glitchTint, tintMix);
                color.a *= Random.Range(this.glitchPanelMinAlpha, 1f);
                g.color = color;

                var mat = this.glitchPanelMaterials[i];
                if (mat == null) continue;
                mat.SetFloat(GlitchAmountId, amount);
                mat.SetFloat(GlitchSeedId, step);
                mat.SetColor(GlitchTintId, this.glitchTint);
            }
        }

        private void EnsureGlitchInitialized()
        {
            if (this.glitchInitialized) return;
            this.glitchRect = (RectTransform)this.transform;
            this.glitchRestPosition = this.glitchRect.anchoredPosition;
            this.glitchTraceRestColor = this.traceImage.color;
            if (this.effectImage != null)
                this.glitchEffectRestColor = this.effectImage.color;
            this.glitchInitialized = true;
        }

        private void UpdateGlitch()
        {
            this.glitchTimer += Time.unscaledDeltaTime;
            if (this.glitchTimer >= this.glitchDuration)
            {
                this.isGlitching = false;
                this.glitchRect!.anchoredPosition = this.glitchRestPosition;
                this.traceImage.color = this.glitchTraceRestColor;
                if (this.effectImage != null)
                    this.effectImage.color = this.glitchEffectRestColor;
                this.SetShaderGlitch(0f, 0f);
                this.EndPanelGlitch();
                return;
            }

            // 1 at the moment of impact, easing to 0 as the glitch ends.
            float envelope = 1f - this.glitchTimer / Mathf.Max(0.0001f, this.glitchDuration);
            float step     = Mathf.Floor(Time.unscaledTime * this.glitchSeedRate);
            float amount   = envelope * Mathf.Lerp(0.65f, 1f, Hash01(step));
            this.SetShaderGlitch(amount, step);

            float jitterX = Random.Range(-this.glitchJitterAmount, this.glitchJitterAmount);
            float jitterY = Random.Range(-this.glitchJitterAmountY, this.glitchJitterAmountY);
            this.glitchRect!.anchoredPosition = this.glitchRestPosition + new Vector2(jitterX, jitterY);

            // The trace (and its glow) flash toward the glitch tint, then settle back to their own color.
            float tintMix = this.glitchTintStrength * Mathf.Pow(envelope, 0.6f);
            this.UpdatePanelGlitch(amount, step, tintMix);

            var traceColor = TintTowards(this.glitchTraceRestColor, this.glitchTint, tintMix);
            traceColor.a *= Random.Range(this.glitchMinAlpha, 1f);
            this.traceImage.color = traceColor;

            if (this.effectImage != null)
            {
                var effectColor = TintTowards(this.glitchEffectRestColor, this.glitchTint, tintMix);
                effectColor.a *= Random.Range(this.glitchMinAlpha, 1f);
                this.effectImage.color = effectColor;
            }
        }

        // Only the sweeping ECG material knows the tear/fringe shader; in the flatline and Mercy
        // looks the trace uses the default UI material, which just gets the color flash above.
        private void SetShaderGlitch(float amount, float seed)
        {
            if (this.runtimeMaterial == null || this.traceImage.material != this.runtimeMaterial) return;

            this.runtimeMaterial.SetFloat(GlitchAmountId, amount);
            if (amount <= 0f) return;
            this.runtimeMaterial.SetFloat(GlitchSeedId, seed);
            this.runtimeMaterial.SetColor(GlitchTintId, this.glitchTint);
        }

        private static Color TintTowards(Color rest, Color tint, float amount)
        {
            var mixed = Color.Lerp(rest, tint, amount);
            mixed.a = rest.a; // brightness/flicker is handled separately; only the hue shifts here
            return mixed;
        }

        private static float Hash01(float n) => Mathf.Repeat(Mathf.Sin(n * 12.9898f) * 43758.5453f, 1f);

        private void OnDisable()
        {
            if (!this.isGlitching || this.glitchRect == null) return;
            this.isGlitching = false;
            this.glitchRect.anchoredPosition = this.glitchRestPosition;
            this.SetShaderGlitch(0f, 0f);
            this.EndPanelGlitch();
        }

        #endregion

        #region Sweep

        private void ApplySweep()
        {
            var material = this.GetRuntimeMaterial();
            material.SetFloat(HeadId, this.t);
            material.SetFloat(TrailLengthId, this.trailFraction);
            material.SetFloat(FadeExponentId, this.fadeExponent);
            material.SetVector(SpriteRectId, GetSpriteUvRect(this.traceImage.sprite));
        }

        // Image UVs are in atlas space, not 0..1 local space, whenever the sprite shares
        // a texture with others (e.g. a sprite sheet). The shader needs the sprite's own
        // UV bounds to remap the sweep so it travels the full visible width.
        private static Vector4 GetSpriteUvRect(Sprite? sprite)
        {
            if (sprite == null || sprite.texture == null)
                return new Vector4(0f, 0f, 1f, 1f);

            var rect = sprite.textureRect;
            var texWidth = sprite.texture.width;
            var texHeight = sprite.texture.height;
            if (texWidth <= 0 || texHeight <= 0)
                return new Vector4(0f, 0f, 1f, 1f);

            return new Vector4(
                rect.xMin / texWidth,
                rect.yMin / texHeight,
                rect.xMax / texWidth,
                rect.yMax / texHeight);
        }

        private Material GetRuntimeMaterial()
        {
            if (this.runtimeMaterial == null)
                this.runtimeMaterial = Instantiate(this.sourceMaterial);

            this.traceImage.material = this.runtimeMaterial;
            return this.runtimeMaterial;
        }

        #endregion
    }
}
