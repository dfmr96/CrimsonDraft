#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.Navigation.Interactables
{
    /// <summary>
    /// Moves the telegraph's Morse key (MorseKey_Lever + MorseKey_Spring) between its resting and
    /// pressed pose. Runs on unscaled time so it still works while the game is paused. The pivot of
    /// the lever mesh is its hinge, so the press is a small rotation about that pivot; the spring
    /// is squashed along its axis while the key is down.
    /// </summary>
    public sealed class MorseKeyAnimator : MonoBehaviour
    {
        [SerializeField] private Transform lever  = null!;
        [SerializeField] private Transform spring = null!;

        [Tooltip("Rotacion local extra de la palanca con la tecla abajo (grados). El eje de bisagra es Y local; el signo negativo la baja.")]
        [SerializeField] private Vector3 leverPressedEuler = new(0f, -12f, 0f);
        [Tooltip("Escala local del resorte con la tecla abajo (multiplica a la escala de reposo).")]
        [SerializeField] private Vector3 springPressedScale = new(1f, 1f, 0.7f);
        [Tooltip("Duracion maxima del movimiento; se acorta para que un punto corto no quede a medias.")]
        [SerializeField] private float moveDuration = 0.04f;

        private Quaternion leverRest;
        private Vector3    springRest;
        private bool       captured;

        private void Capture()
        {
            if (this.captured) return;
            this.captured   = true;
            this.leverRest  = this.lever.localRotation;
            this.springRest = this.spring.localScale;
        }

        private void Awake() => Capture();

        /// <param name="maxSeconds">Longest the move may take (a dot lasts only a few hundredths).</param>
        public void SetPressed(bool pressed, float maxSeconds = 0.04f)
        {
            Capture();
            float duration = Mathf.Min(this.moveDuration, Mathf.Max(0.01f, maxSeconds));

            this.lever.DOKill();
            this.spring.DOKill();

            var leverTarget  = pressed ? this.leverRest * Quaternion.Euler(this.leverPressedEuler) : this.leverRest;
            var springTarget = pressed ? Vector3.Scale(this.springRest, this.springPressedScale) : this.springRest;

            this.lever.DOLocalRotateQuaternion(leverTarget, duration).SetUpdate(true).SetEase(Ease.OutQuad);
            this.spring.DOScale(springTarget, duration).SetUpdate(true).SetEase(Ease.OutQuad);
        }

        private void OnDisable()
        {
            if (!this.captured) return;
            this.lever.DOKill();
            this.spring.DOKill();
            this.lever.localRotation = this.leverRest;
            this.spring.localScale   = this.springRest;
        }
    }
}
