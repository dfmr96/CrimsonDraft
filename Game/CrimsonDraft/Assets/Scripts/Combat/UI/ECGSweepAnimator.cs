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
                this.restTimer += Time.deltaTime;
                if (this.restTimer < this.restDuration)
                {
                    return;
                }

                this.isResting = false;
                this.t = 0f;
            }
            else
            {
                this.t += Time.deltaTime / this.sweepDuration;
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
