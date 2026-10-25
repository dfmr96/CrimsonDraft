#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// Keeps the rain particle systems in front of the diorama camera. The rain emitter is a wide flat
    /// box high above the water, facing down, that stays centred on what the camera looks at (only its
    /// yaw is followed, so tilting the camera never tilts the rain); the splash emitter is a box on the
    /// water surface under the same area. Both simulate in world space, so the drops themselves are
    /// real 3D points with proper parallax.
    /// </summary>
    public sealed class RainRig : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera = null!;
        [Tooltip("Altura del agua en el mundo (la superficie donde pegan las gotas).")]
        [SerializeField] private float waterLevel = -1.15f;

        [Header("Rain (falling streaks)")]
        [SerializeField] private Transform rain = null!;
        [Tooltip("Que tan arriba del agua nacen las gotas.")]
        [SerializeField] private float rainHeight = 24f;
        [Tooltip("Distancia, delante de la camara, al centro de la zona de lluvia.")]
        [SerializeField] private float rainForward = 32f;

        [Header("Splashes (on the water)")]
        [SerializeField] private Transform? splashes;
        [SerializeField] private float splashForward = 22f;

        // Both systems are put where they belong and fast-forwarded before the first frame is drawn, so
        // the rain is already falling when the scene appears instead of starting to spawn.
        private void Start()
        {
            LateUpdate();
            Prewarm(this.rain);
            if (this.splashes != null) Prewarm(this.splashes);
        }

        private static void Prewarm(Transform system)
        {
            var particles = system.GetComponent<ParticleSystem>();
            if (particles == null) return;

            particles.Clear(true);
            particles.Simulate(particles.main.startLifetime.constantMax, true, true);
            particles.Play(true);
        }

        private void LateUpdate()
        {
            var cam   = this.worldCamera.transform.position;
            var yaw   = Quaternion.Euler(0f, this.worldCamera.transform.eulerAngles.y, 0f);
            var ahead = yaw * Vector3.forward;

            this.rain.SetPositionAndRotation(
                new Vector3(cam.x, this.waterLevel + this.rainHeight, cam.z) + ahead * this.rainForward,
                yaw * Quaternion.Euler(90f, 0f, 0f)); // local forward = straight down

            if (this.splashes != null)
                this.splashes.SetPositionAndRotation(
                    new Vector3(cam.x, this.waterLevel + 0.01f, cam.z) + ahead * this.splashForward,
                    yaw);
        }
    }
}
