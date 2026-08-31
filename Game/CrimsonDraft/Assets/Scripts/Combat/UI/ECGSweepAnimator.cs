#nullable enable

using UnityEngine;
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

        [SerializeField] private Image traceImage = null!;
        [SerializeField] private float sweepDuration = 2f;
        [SerializeField, Min(0f)] private float restDuration = 0f;
        [SerializeField, Range(0.01f, 0.5f)] private float trailFraction = 0.18f;
        [SerializeField, Range(0.1f, 4f)] private float fadeExponent = 1f;

        [Header("Health States (ECG_1..ECG_4, Dead)")]
        [SerializeField] private Sprite? stageSpriteStable;   // 75-100%, calm/slow
        [SerializeField] private Sprite? stageSpriteCaution;  // 50-75%
        [SerializeField] private Sprite? stageSpriteWarning;  // 25-50%
        [SerializeField] private Sprite? stageSpriteCritical; // 0-25%, fast/erratic
        [SerializeField] private Sprite? stageSpriteDead;     // 0%, static flatline

        [SerializeField] private float stageDurationStable = 3f;
        [SerializeField] private float stageDurationCaution = 2f;
        [SerializeField] private float stageDurationWarning = 1.2f;
        [SerializeField] private float stageDurationCritical = 0.6f;

        // Soft color-matched glow sitting behind the trace so the ECG's own backdrop isn't
        // flat black — a gradient sprite tinted per health band instead of a fixed color,
        // so it reads at a glance which of the 4 stages is currently active.
        [Header("Background Effect (tints the gradient 'Effect' image behind the trace)")]
        [SerializeField] private Image? effectImage;
        [SerializeField] private Color effectColorStable   = new(0.4901961f, 0.7058824f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorCaution   = new(0.6901961f, 0.7058824f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorWarning   = new(0.6901961f, 0.5568628f, 0.29803923f, 0.55f);
        [SerializeField] private Color effectColorCritical  = new(0.73333335f, 0.44705883f, 0.26666668f, 0.55f);

        [Header("Damage Glitch (CRT signal-loss burst)")]
        [SerializeField, Min(0f)] private float glitchDuration = 0.35f;
        [SerializeField, Min(0f)] private float glitchJitterAmount = 6f;
        [SerializeField, Range(0f, 1f)] private float glitchMinAlpha = 0.15f;

        private float t;
        private bool isResting;
        private bool isFlatlined;
        private float restTimer;
        private Material? sourceMaterial;
        private Material? runtimeMaterial;

        // Whatever alpha the effect Image was left at in the Editor -- SetHealthState only
        // ever retints RGB per band, it never overwrites this, so a designer's opacity
        // tweak on the Image component survives entering Play instead of being clobbered
        // by the hardcoded 0.55f baked into effectColorStable/Caution/Warning/Critical.
        private float effectBaseAlpha = 1f;

        private RectTransform? glitchRect;
        private Vector2 glitchRestPosition;
        private Color glitchTraceRestColor;
        private Color glitchEffectRestColor;
        private bool glitchInitialized;
        private bool isGlitching;
        private float glitchTimer;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            this.sourceMaterial = this.traceImage.material;
            if (this.effectImage != null)
                this.effectBaseAlpha = this.effectImage.color.a;
        }

        private void OnEnable()
        {
            this.t = 0f;
            this.isResting = false;
            this.restTimer = 0f;
            if (!this.isFlatlined)
                this.ApplySweep();
        }

        private void Update()
        {
            if (this.isGlitching)
                this.UpdateGlitch();

            if (this.isFlatlined) return;

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

        public void SetHealthState(float hpRatio)
        {
            hpRatio = Mathf.Clamp01(hpRatio);

            if (hpRatio <= 0f)
            {
                this.isFlatlined = true;
                if (this.stageSpriteDead != null)
                    this.traceImage.sprite = this.stageSpriteDead;
                this.traceImage.material = null;
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

            if (hpRatio <= 0.25f)      { sprite = this.stageSpriteCritical; duration = this.stageDurationCritical; effectColor = this.effectColorCritical; }
            else if (hpRatio <= 0.50f) { sprite = this.stageSpriteWarning;  duration = this.stageDurationWarning;  effectColor = this.effectColorWarning;  }
            else if (hpRatio <= 0.75f) { sprite = this.stageSpriteCaution;  duration = this.stageDurationCaution;  effectColor = this.effectColorCaution;  }
            else                       { sprite = this.stageSpriteStable;  duration = this.stageDurationStable;   effectColor = this.effectColorStable;   }

            if (sprite != null)
                this.traceImage.sprite = sprite;

            this.sweepDuration = duration;

            if (this.effectImage != null)
            {
                effectColor.a = this.effectBaseAlpha;
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
            this.isGlitching = true;
            this.glitchTimer = 0f;
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
                return;
            }

            float jitterX = Random.Range(-this.glitchJitterAmount, this.glitchJitterAmount);
            this.glitchRect!.anchoredPosition = this.glitchRestPosition + new Vector2(jitterX, 0f);

            var traceColor = this.glitchTraceRestColor;
            traceColor.a *= Random.Range(this.glitchMinAlpha, 1f);
            this.traceImage.color = traceColor;

            if (this.effectImage != null)
            {
                var effectColor = this.glitchEffectRestColor;
                effectColor.a *= Random.Range(this.glitchMinAlpha, 1f);
                this.effectImage.color = effectColor;
            }
        }

        private void OnDisable()
        {
            if (!this.isGlitching || this.glitchRect == null) return;
            this.isGlitching = false;
            this.glitchRect.anchoredPosition = this.glitchRestPosition;
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
