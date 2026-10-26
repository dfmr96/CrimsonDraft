#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Lighting
{
    // Fluorescent-style flicker: the light hums with a little noise most of the time and,
    // every few seconds, cuts out in a short burst of on/off blinks. The emissive material of
    // the fixture follows the same intensity through a MaterialPropertyBlock so the shared
    // material asset is never touched.
    public sealed class LightFlicker : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light     targetLight = null!;
        [SerializeField] private Light[]   additionalLights = System.Array.Empty<Light>(); // p.ej. un point light de relleno junto al spot
        [SerializeField] private Renderer? emissiveRenderer;
        [SerializeField] private int       emissiveMaterialIndex;

        [Header("Zumbido constante")]
        [SerializeField, Range(0f, 0.5f)] private float noiseAmount = 0.08f;
        [SerializeField]                  private float noiseSpeed  = 12f;

        [Header("Cortes")]
        [SerializeField] private Vector2    burstInterval = new(1.5f, 5f);    // segundos entre cortes
        [SerializeField] private Vector2Int blinksPerBurst = new(2, 5);
        [SerializeField] private Vector2    blinkDuration = new(0.03f, 0.12f); // segundos de cada parpadeo
        [SerializeField, Range(0f, 1f)] private float offIntensity = 0.05f;   // fracción de la intensidad cuando se "apaga"

        private MaterialPropertyBlock? block;
        private Color baseEmission;
        private float baseIntensity;
        private float[] additionalBaseIntensities = System.Array.Empty<float>();
        private float noiseSeed;

        private float nextBurstTime;
        private int   blinksLeft;
        private float blinkEndTime;
        private bool  blinkOff;

        private void Awake()
        {
            this.baseIntensity = this.targetLight.intensity;
            this.noiseSeed     = Random.value * 100f;

            this.additionalBaseIntensities = new float[this.additionalLights.Length];
            for (int i = 0; i < this.additionalLights.Length; i++)
                if (this.additionalLights[i] != null)
                    this.additionalBaseIntensities[i] = this.additionalLights[i].intensity;

            if (this.emissiveRenderer != null)
            {
                var mats = this.emissiveRenderer.sharedMaterials;
                var mat  = this.emissiveMaterialIndex < mats.Length ? mats[this.emissiveMaterialIndex] : null;
                if (mat != null && mat.HasProperty(EmissionColorId))
                {
                    this.baseEmission = mat.GetColor(EmissionColorId);
                    this.block        = new MaterialPropertyBlock();
                }
            }

            ScheduleNextBurst();
        }

        private void OnDisable() => Apply(1f);

        private void Update()
        {
            var now = Time.time;

            if (this.blinksLeft == 0 && now >= this.nextBurstTime)
                this.blinksLeft = Random.Range(this.blinksPerBurst.x, this.blinksPerBurst.y + 1) * 2; // cada parpadeo = apagado + encendido

            if (this.blinksLeft > 0 && now >= this.blinkEndTime)
            {
                this.blinksLeft--;
                this.blinkOff     = this.blinksLeft % 2 == 1;
                this.blinkEndTime = now + Random.Range(this.blinkDuration.x, this.blinkDuration.y);
                if (this.blinksLeft == 0) ScheduleNextBurst();
            }

            float noise = 1f - this.noiseAmount * Mathf.PerlinNoise(this.noiseSeed, now * this.noiseSpeed);
            Apply(this.blinkOff && this.blinksLeft > 0 ? this.offIntensity : noise);
        }

        private void ScheduleNextBurst()
        {
            this.blinkOff      = false;
            this.nextBurstTime = Time.time + Random.Range(this.burstInterval.x, this.burstInterval.y);
        }

        private void Apply(float factor)
        {
            this.targetLight.intensity = this.baseIntensity * factor;

            for (int i = 0; i < this.additionalLights.Length; i++)
                if (this.additionalLights[i] != null)
                    this.additionalLights[i].intensity = this.additionalBaseIntensities[i] * factor;

            if (this.block == null || this.emissiveRenderer == null) return;
            this.emissiveRenderer.GetPropertyBlock(this.block, this.emissiveMaterialIndex);
            this.block.SetColor(EmissionColorId, this.baseEmission * factor);
            this.emissiveRenderer.SetPropertyBlock(this.block, this.emissiveMaterialIndex);
        }

        private void Reset()
        {
            this.targetLight      = GetComponentInChildren<Light>();
            this.emissiveRenderer = GetComponent<Renderer>();
        }
    }
}
