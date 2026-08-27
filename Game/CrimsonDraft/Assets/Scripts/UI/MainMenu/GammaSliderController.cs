#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class GammaSliderController : MonoBehaviour
    {
        [SerializeField] private Volume globalVolume  = null!;
        [Tooltip("Corrimiento de gamma (canal master) aplicado en los extremos del slider.")]
        [SerializeField] private float  gammaOffsetRange = 0.5f;

        private LiftGammaGain? liftGammaGain;

        private void Awake()
        {
            if (this.globalVolume.profile.TryGet(out LiftGammaGain lgg))
                this.liftGammaGain = lgg;
        }

        public void SetGamma(float sliderValue)
        {
            if (this.liftGammaGain == null) return;

            float offset = Mathf.Lerp(-this.gammaOffsetRange, this.gammaOffsetRange, sliderValue);
            Vector4 gamma = this.liftGammaGain.gamma.value;
            gamma.w = offset;
            this.liftGammaGain.gamma.value = gamma;
        }
    }
}
