#nullable enable

using UnityEngine;
using CrimsonDraft.Rendering.Shadows;

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

        /// <summary>The corpse got back up: leaves a copy of the pool on the floor (under
        /// <paramref name="parent"/>, e.g. the room), frozen at its current world size, and hides
        /// this one, which stays on the enemy ready to grow again on its next death. The copy
        /// only lives for this session.</summary>
        public CorpseBloodBlob LeaveBehind(Transform? parent)
        {
            // Growth state isn't serialized, so the copy starts frozen at whatever size this one had.
            var copy = Instantiate(this, parent, true); // keep the world pose and size
            copy.name = name + " (left behind)";
            // Without this the copy's BlobShadow would keep tracking the enemy as it walks away.
            foreach (var shadow in copy.GetComponentsInChildren<BlobShadow>(includeInactive: true))
                shadow.enabled = false;

            gameObject.SetActive(false);
            return copy;
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
