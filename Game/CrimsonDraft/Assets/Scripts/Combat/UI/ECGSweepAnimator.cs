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

        [Header("Health States (ECG_1..ECG_4)")]
        [SerializeField] private Sprite? stageSpriteStable;   // 75-100%, calm/slow
        [SerializeField] private Sprite? stageSpriteCaution;  // 50-75%
        [SerializeField] private Sprite? stageSpriteWarning;  // 25-50%
        [SerializeField] private Sprite? stageSpriteCritical; // 0-25%, fast/erratic

        [SerializeField] private float stageDurationStable = 3f;
        [SerializeField] private float stageDurationCaution = 2f;
        [SerializeField] private float stageDurationWarning = 1.2f;
        [SerializeField] private float stageDurationCritical = 0.6f;

        private float t;
        private bool isResting;
        private float restTimer;
        private Material? runtimeMaterial;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            this.t = 0f;
            this.isResting = false;
            this.restTimer = 0f;
            this.ApplySweep();
        }

        private void Update()
        {
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

            Sprite? sprite;
            float duration;

            if (hpRatio <= 0.25f)      { sprite = this.stageSpriteCritical; duration = this.stageDurationCritical; }
            else if (hpRatio <= 0.50f) { sprite = this.stageSpriteWarning;  duration = this.stageDurationWarning;  }
            else if (hpRatio <= 0.75f) { sprite = this.stageSpriteCaution;  duration = this.stageDurationCaution;  }
            else                       { sprite = this.stageSpriteStable;  duration = this.stageDurationStable;   }

            if (sprite != null)
                this.traceImage.sprite = sprite;

            this.sweepDuration = duration;
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
            {
                this.runtimeMaterial = Instantiate(this.traceImage.material);
                this.traceImage.material = this.runtimeMaterial;
            }

            return this.runtimeMaterial;
        }

        #endregion
    }
}
