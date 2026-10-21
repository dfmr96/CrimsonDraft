#nullable enable

using UnityEngine;

namespace CrimsonDraft.Rendering.Blood
{
    /// <summary>
    /// Charco de sangre bajo un enemigo muerto (BloodBlob del Horror Engine), tanto en
    /// navegación (EnemyNavAgent) como en combate (EnemyDeathMarker). Solo maneja el
    /// crecimiento: escala este transform, y el BlobShadow hijo se encarga de apoyar el quad en
    /// el piso (su localScale es suyo, por eso el crecimiento va en el padre).
    /// </summary>
    public sealed class CorpseBloodBlob : MonoBehaviour
    {
        [Tooltip("Segundos desde la muerte hasta que la sangre empieza a extenderse.")]
        [SerializeField, Min(0f)]    private float growDelay    = 1.5f;
        [Tooltip("Segundos que tarda el charco en llegar a su tamaño final.")]
        [SerializeField, Min(0.01f)] private float growDuration = 3f;

        private float elapsed;
        private bool  growing;

        /// <summary>Shows the pool. A fresh death grows it from nothing; a restored corpse
        /// (save load, room re-entry) gets it at full size straight away.</summary>
        public void Show(bool grow)
        {
            gameObject.SetActive(true);
            this.growing = grow;
            this.elapsed = 0f;
            ApplyScale(grow ? 0f : 1f);
        }

        public static float ScaleAt(float elapsed, float delay, float duration)
            => Mathf.Clamp01((elapsed - delay) / duration);

        private void Update()
        {
            if (!this.growing) return;

            this.elapsed += Time.deltaTime;
            var scale = ScaleAt(this.elapsed, this.growDelay, this.growDuration);
            ApplyScale(scale);
            if (scale >= 1f) this.growing = false;
        }

        private void ApplyScale(float scale) => transform.localScale = Vector3.one * scale;
    }
}
