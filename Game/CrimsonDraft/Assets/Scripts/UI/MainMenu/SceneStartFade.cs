#nullable enable

using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// The scene opens on black and fades up to the title. While it holds and fades, nothing can be
    /// pressed (<see cref="Finished"/> is what PressAnyButtonIntro waits for) -- this is the time
    /// the rain and the rest of the scene need to spawn in, so none of it appears to pop in.
    /// Put on a full-screen black Image in an Overlay canvas above everything.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class SceneStartFade : MonoBehaviour
    {
        [Tooltip("Cuanto se queda totalmente negro antes de empezar a aclarar.")]
        [SerializeField] private float holdDuration = 0.9f;
        [SerializeField] private float fadeDuration = 1.6f;
        [Tooltip("Mayor a este dt por cuadro no se cuenta: los tirones de los primeros cuadros (carga de la escena, shaders) no se comen el fundido.")]
        [SerializeField] private float maxFrameTime = 0.05f;

        private Image image = null!;
        private float elapsed;

        /// <summary>True once the screen is fully visible and input is allowed.</summary>
        public bool Finished { get; private set; }

        private void Awake()
        {
            this.image = GetComponent<Image>();
            SetAlpha(1f);
            this.image.raycastTarget = true; // blocks pointer clicks too, while it lasts
        }

        private void Update()
        {
            if (this.Finished) return;

            // The first frames of a scene are long ones; skip them and cap the rest, so the hold and the
            // fade are really seen.
            if (Time.frameCount < 4) return;
            this.elapsed += Mathf.Min(Time.unscaledDeltaTime, this.maxFrameTime);

            if (this.elapsed < this.holdDuration) return;

            float t = Mathf.Clamp01((this.elapsed - this.holdDuration) / Mathf.Max(0.0001f, this.fadeDuration));
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, t));

            if (t < 1f) return;
            this.Finished = true;
            gameObject.SetActive(false);
        }

        private void SetAlpha(float alpha)
        {
            var color = this.image.color;
            color.a = alpha;
            this.image.color = color;
        }
    }
}
