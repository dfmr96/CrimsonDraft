#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// One lightning flash in the 3D world behind the main menu: after a short build-up the scene
    /// lights up -- the skybox, the ambient light and the sun jump to cold white, and the screen whites
    /// out -- following <see cref="flashKeys"/>: it reaches its maximum almost at once and then
    /// flickers, brightening and dimming a few more times like the aftershocks of a real strike,
    /// each weaker than the last, before going out completely. Then everything goes back to how it
    /// was. There is no visible bolt, only the light. Unscaled time.
    ///
    /// The skybox is swapped for a runtime copy while it plays so the shared asset is never touched.
    /// </summary>
    public sealed class LightningStrike : MonoBehaviour
    {
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId     = Shader.PropertyToID("_Tint");

        [Header("Flash shape")]
        [Tooltip("Intensidad del destello en el tiempo, despues del impacto: X = segundos, Y = nivel (1 = maximo). Sube al maximo casi de golpe y despues titila: sube y baja varias veces, cada vez menos, hasta apagarse en 0. Se interpola linealmente entre puntos, asi que mas puntos = mas titileo.")]
        [SerializeField] private Vector2[] flashKeys =
        {
            new(0.00f, 0.00f),
            new(0.05f, 1.00f),
            new(0.14f, 0.45f),
            new(0.20f, 0.85f),
            new(0.32f, 0.30f),
            new(0.38f, 0.60f),
            new(0.55f, 0.18f),
            new(0.62f, 0.32f),
            new(0.85f, 0.07f),
            new(1.50f, 0.00f),
        };

        [Header("White-out")]
        [Tooltip("Blanco plano a pantalla completa, en un canvas Overlay por encima de toda la UI. Su opacidad sigue la forma del destello.")]
        [SerializeField] private Image? whiteFill;
        [Tooltip("Opacidad del blanco en el pico del destello (1 = pantalla totalmente blanca).")]
        [Range(0f, 1f)]
        [SerializeField] private float whitePeak = 0.9f;
        [Tooltip("Cuando, despues del impacto, el destello ya se calmo y puede empezar el oscurecimiento del fondo.")]
        [SerializeField] private float dimStartAfterImpact = 0.7f;

        [Header("Scene light")]
        [Tooltip("Luz direccional del diorama: sube a Sun Peak con el destello. Opcional.")]
        [SerializeField] private Light? sun;
        [SerializeField] private float sunPeak = 2.5f;
        [SerializeField] private Color sunPeakColor = new(0.75f, 0.85f, 1f);
        [Tooltip("RenderSettings.ambientIntensity en el pico del destello.")]
        [SerializeField] private float ambientPeak = 2f;
        [SerializeField] private float skyExposurePeak = 5f;
        [SerializeField] private Color skyTintPeak = new(0.62f, 0.7f, 0.85f, 0.5f);
        [Tooltip("Cuanto del destello ya se nota durante la previa, justo antes del impacto (0 = nada).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float preGlow = 0.12f;

        [Header("Timing (seconds)")]
        [Tooltip("Previa: el cielo empieza a brillar apenas antes del destello.")]
        [SerializeField] private float buildUpDuration = 0.28f;

        private Sequence? sequence;

        // Captured when a strike starts, restored when it ends.
        private bool      active;
        private Material? originalSkybox;
        private Material? skyInstance;
        private float     baseSkyExposure;
        private Color     baseSkyTint;
        private float     baseAmbient;
        private float     baseSunIntensity;
        private Color     baseSunColor;

        /// <param name="onImpact">The build-up is over (the flash starts this frame).</param>
        /// <param name="onFinished">The flash has fully faded and the scene is back to normal.</param>
        public void Play(Action? onImpact, Action? onFinished)
        {
            Kill();
            Capture();

            this.sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(DOVirtual.Float(0f, this.preGlow, this.buildUpDuration, ApplyFlash).SetEase(Ease.InSine))
                .AppendCallback(() =>
                {
                    ShowWhite(true);
                    onImpact?.Invoke();
                })
                .Append(DOVirtual.Float(0f, FlashDuration, FlashDuration, t => Flash(EvaluateFlash(t))).SetEase(Ease.Linear))
                .OnComplete(() =>
                {
                    this.sequence = null;
                    ShowWhite(false);
                    Restore();
                    onFinished?.Invoke();
                });
        }

        private void OnDisable() => Kill();

        private void OnDestroy() => Kill();

        private void Kill()
        {
            this.sequence?.Kill();
            this.sequence = null;
            ShowWhite(false);
            Restore();
        }

        /// <summary>How long after the impact the screen has calmed down enough to start dimming.</summary>
        public float WhiteHoldAfterImpact => this.dimStartAfterImpact;

        private float FlashDuration => this.flashKeys.Length > 0 ? this.flashKeys[this.flashKeys.Length - 1].x : 0.01f;

        // Linear interpolation through the (seconds, level) keys; 0 outside them.
        private float EvaluateFlash(float t)
        {
            var keys = this.flashKeys;
            if (keys.Length == 0 || t <= keys[0].x) return 0f;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].x) continue;
                float span = Mathf.Max(0.0001f, keys[i].x - keys[i - 1].x);
                return Mathf.Lerp(keys[i - 1].y, keys[i].y, (t - keys[i - 1].x) / span);
            }
            return keys[keys.Length - 1].y;
        }

        // One frame of the flash: the world's light and the white-out move together.
        private void Flash(float level)
        {
            ApplyFlash(level);
            SetWhite(level * this.whitePeak);
        }

        private void ShowWhite(bool show)
        {
            if (this.whiteFill == null) return;
            if (show) SetWhite(0f);
            this.whiteFill.gameObject.SetActive(show);
        }

        private void SetWhite(float alpha)
        {
            if (this.whiteFill == null) return;
            var color = this.whiteFill.color;
            color.a = alpha;
            this.whiteFill.color = color;
        }

        private void Capture()
        {
            this.active = true;

            this.originalSkybox = RenderSettings.skybox;
            if (this.originalSkybox != null)
            {
                this.skyInstance = new Material(this.originalSkybox);
                this.baseSkyExposure = this.skyInstance.HasProperty(ExposureId) ? this.skyInstance.GetFloat(ExposureId) : 1f;
                this.baseSkyTint     = this.skyInstance.HasProperty(TintId) ? this.skyInstance.GetColor(TintId) : Color.gray;
                RenderSettings.skybox = this.skyInstance;
            }

            this.baseAmbient = RenderSettings.ambientIntensity;
            if (this.sun != null)
            {
                this.baseSunIntensity = this.sun.intensity;
                this.baseSunColor     = this.sun.color;
            }
        }

        private void Restore()
        {
            if (!this.active) return;
            this.active = false;

            RenderSettings.ambientIntensity = this.baseAmbient;
            if (this.sun != null)
            {
                this.sun.intensity = this.baseSunIntensity;
                this.sun.color     = this.baseSunColor;
            }

            RenderSettings.skybox = this.originalSkybox;
            if (this.skyInstance != null) Destroy(this.skyInstance);
            this.skyInstance = null;
            this.originalSkybox = null;
        }

        // 0 = the scene as it was, 1 = the peak of the flash.
        private void ApplyFlash(float f)
        {
            RenderSettings.ambientIntensity = Mathf.Lerp(this.baseAmbient, this.ambientPeak, f);

            if (this.skyInstance != null)
            {
                this.skyInstance.SetFloat(ExposureId, Mathf.Lerp(this.baseSkyExposure, this.skyExposurePeak, f));
                this.skyInstance.SetColor(TintId, Color.Lerp(this.baseSkyTint, this.skyTintPeak, f));
            }

            if (this.sun != null)
            {
                this.sun.intensity = Mathf.Lerp(this.baseSunIntensity, this.sunPeak, f);
                this.sun.color     = Color.Lerp(this.baseSunColor, this.sunPeakColor, f);
            }
        }
    }
}
