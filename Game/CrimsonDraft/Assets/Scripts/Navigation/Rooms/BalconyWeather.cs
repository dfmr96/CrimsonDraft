#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    /// <summary>
    /// Open-air weather for the balconies: while the room is active it swaps in the main-menu skybox and
    /// keeps a rain emitter (a flat box above the player, facing down) centred in front of the camera. The
    /// drops collide with the room's colliders and spawn the splash sub-emitter where they land, so
    /// nothing splashes over empty air and nothing falls through the roof. Only the camera yaw is
    /// followed so tilting never tilts the rain. The main camera clears to the skybox while the room is
    /// active (the gameplay camera clears to a solid colour otherwise). Rooms are deactivated when the
    /// player leaves, so OnEnable/OnDisable bound the effect and restore the previous state.
    ///
    /// Every few seconds a lightning flash lights the sky up and lifts the ambient light, like the one
    /// behind the main menu (<c>LightningStrike</c>): it peaks almost at once and then flickers, each
    /// aftershock weaker than the last. The skybox is a runtime copy while the room is active so the
    /// shared asset is never touched. There is no visible bolt, only the light. Real time.
    /// </summary>
    public sealed class BalconyWeather : MonoBehaviour
    {
        [SerializeField] private Material skybox = null!;
        [SerializeField] private Transform rain = null!;

        [Tooltip("Altura del suelo del balcon (donde pegan las gotas).")]
        [SerializeField] private float floorOffset;
        [Tooltip("Que tan arriba del suelo nacen las gotas.")]
        [SerializeField] private float rainHeight = 14f;
        [Tooltip("Distancia, delante de la camara, al centro de la zona de lluvia.")]
        [SerializeField] private float rainForward = 8f;

        [Header("Lightning")]
        [Tooltip("Segundos entre rayos: se elige al azar entre X (min) e Y (max). El primero llega antes.")]
        [SerializeField] private Vector2 strikeInterval = new(5f, 14f);
        [Tooltip("Intensidad del destello en el tiempo: X = segundos, Y = nivel (1 = maximo). Sube casi de golpe y despues titila.")]
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
        [SerializeField] private float skyExposurePeak = 5f;
        [SerializeField] private Color skyTintPeak = new(0.62f, 0.7f, 0.85f, 0.5f);
        [Tooltip("Color del ambiente en el pico del destello (la escena usa ambiente plano).")]
        [SerializeField] private Color ambientPeakColor = new(0.45f, 0.52f, 0.68f, 1f);
        [Tooltip("RenderSettings.ambientIntensity en el pico (solo cuenta si el ambiente es Skybox).")]
        [SerializeField] private float ambientPeakIntensity = 2f;

        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int TintId     = Shader.PropertyToID("_Tint");

        private Material?       previousSkybox;
        private Material?       skyInstance;
        private float           baseExposure;
        private Color          baseTint;
        private Color          baseAmbientColor;
        private float          baseAmbientIntensity;
        private float          nextStrikeAt;
        private float          strikeStart = -1f;
        private Camera?         skyCamera;
        private CameraClearFlags previousClearFlags;
        private bool            skyApplied;

        private void OnEnable()
        {
            this.previousSkybox = RenderSettings.skybox;
            this.skyInstance    = new Material(this.skybox);
            this.baseExposure   = this.skyInstance.HasProperty(ExposureId) ? this.skyInstance.GetFloat(ExposureId) : 1f;
            this.baseTint       = this.skyInstance.HasProperty(TintId) ? this.skyInstance.GetColor(TintId) : Color.gray;
            RenderSettings.skybox = this.skyInstance;

            this.baseAmbientColor     = RenderSettings.ambientLight;
            this.baseAmbientIntensity = RenderSettings.ambientIntensity;
            this.strikeStart  = -1f;
            this.nextStrikeAt = Time.unscaledTime + Random.Range(1.5f, 4f);

            this.skyCamera = Camera.main;
            if (this.skyCamera != null)
            {
                this.previousClearFlags = this.skyCamera.clearFlags;
                this.skyCamera.clearFlags = CameraClearFlags.Skybox;
            }
            this.skyApplied = true;

            this.rain.gameObject.SetActive(true);
            Follow();
            Prewarm(this.rain);
        }

        private void OnDisable()
        {
            if (!this.skyApplied) return;

            RenderSettings.skybox           = this.previousSkybox;
            RenderSettings.ambientLight     = this.baseAmbientColor;
            RenderSettings.ambientIntensity = this.baseAmbientIntensity;
            if (this.skyInstance != null) Destroy(this.skyInstance);
            this.skyInstance = null;
            if (this.skyCamera != null) this.skyCamera.clearFlags = this.previousClearFlags;
            this.skyApplied = false;
        }

        private void LateUpdate()
        {
            Follow();
            UpdateLightning();
        }

        private void UpdateLightning()
        {
            if (this.strikeStart < 0f)
            {
                if (Time.unscaledTime < this.nextStrikeAt) return;
                this.strikeStart = Time.unscaledTime;
            }

            var t = Time.unscaledTime - this.strikeStart;
            if (t >= FlashDuration)
            {
                ApplyFlash(0f);
                this.strikeStart  = -1f;
                this.nextStrikeAt = Time.unscaledTime + Random.Range(this.strikeInterval.x, this.strikeInterval.y);
                return;
            }

            ApplyFlash(EvaluateFlash(t));
        }

        private float FlashDuration => this.flashKeys.Length > 0 ? this.flashKeys[this.flashKeys.Length - 1].x : 0.01f;

        // Linear interpolation through the (seconds, level) keys; 0 outside them.
        private float EvaluateFlash(float t)
        {
            var keys = this.flashKeys;
            if (keys.Length == 0 || t <= keys[0].x) return 0f;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].x) continue;
                var span = Mathf.Max(0.0001f, keys[i].x - keys[i - 1].x);
                return Mathf.Lerp(keys[i - 1].y, keys[i].y, (t - keys[i - 1].x) / span);
            }
            return keys[keys.Length - 1].y;
        }

        // 0 = the room as it was, 1 = the peak of the flash.
        private void ApplyFlash(float f)
        {
            RenderSettings.ambientLight     = Color.Lerp(this.baseAmbientColor, this.ambientPeakColor, f);
            RenderSettings.ambientIntensity = Mathf.Lerp(this.baseAmbientIntensity, this.ambientPeakIntensity, f);

            if (this.skyInstance == null) return;
            this.skyInstance.SetFloat(ExposureId, Mathf.Lerp(this.baseExposure, this.skyExposurePeak, f));
            this.skyInstance.SetColor(TintId, Color.Lerp(this.baseTint, this.skyTintPeak, f));
        }

        // Fast-forwarded before the first frame so the rain is already falling when the room appears.
        private static void Prewarm(Transform system)
        {
            var particles = system.GetComponent<ParticleSystem>();
            if (particles == null) return;

            particles.Clear(true);
            particles.Simulate(particles.main.startLifetime.constantMax, true, true);
            particles.Play(true);
        }

        private void Follow()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var camPos = cam.transform.position;
            var yaw    = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
            var ahead  = yaw * Vector3.forward;
            var floorY = transform.position.y + this.floorOffset;

            this.rain.SetPositionAndRotation(
                new Vector3(camPos.x, floorY + this.rainHeight, camPos.z) + ahead * this.rainForward,
                yaw * Quaternion.Euler(90f, 0f, 0f)); // local forward = straight down
        }
    }
}
