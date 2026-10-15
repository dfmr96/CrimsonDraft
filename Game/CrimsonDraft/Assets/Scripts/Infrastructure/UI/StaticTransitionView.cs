#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    // Full-screen "TV static" glitch used to cover the Navigation -> Combat scene swap (like an old
    // TV changing channel). It grabs a frame of whatever is on screen and glitches THAT (shifting
    // its pixels around, see TVStatic.shader), so the effect looks like the game's own image
    // breaking up. Everything runs on unscaled time so it works while the game is slowed.
    public sealed class StaticTransitionView : MonoBehaviour
    {
        private static readonly int IntensityId    = Shader.PropertyToID("_Intensity");
        private static readonly int AlphaId        = Shader.PropertyToID("_Alpha");
        private static readonly int UnscaledTimeId = Shader.PropertyToID("_UnscaledTime");

        [SerializeField] private RawImage rawImage = null!;
        [SerializeField] private float rampInDuration  = 0.35f; // glitch builds up progressively
        [SerializeField] private float rampOutDuration = 0.45f; // glitch fades away over the new scene

        private Material? runtimeMaterial;
        private Texture2D? capturedFrame;

        private void Awake()
        {
            // Own copy so the shared material asset is never mutated.
            this.runtimeMaterial = Instantiate(this.rawImage.material);
            this.rawImage.material = this.runtimeMaterial;
            this.rawImage.enabled  = false;
        }

        private void OnDestroy()
        {
            if (this.runtimeMaterial != null)
                Destroy(this.runtimeMaterial);
            this.ReleaseFrame();
        }

        private void Update()
        {
            if (this.rawImage.enabled && this.runtimeMaterial != null)
                this.runtimeMaterial.SetFloat(UnscaledTimeId, Time.unscaledTime);
        }

        public async UniTask ShowAsync()
        {
            // Frozen copy of the current frame; taken while the overlay is still hidden so it
            // does not capture itself.
            await UniTask.WaitForEndOfFrame(this);
            this.ReleaseFrame();
            this.capturedFrame            = ScreenCapture.CaptureScreenshotAsTexture();
            this.capturedFrame.filterMode = FilterMode.Point;
            this.capturedFrame.wrapMode   = TextureWrapMode.Repeat;

            this.rawImage.texture = this.capturedFrame;
            this.SetIntensity(0f);
            this.SetAlpha(1f);
            this.rawImage.enabled = true;

            await this.RampAsync(0f, 1f, this.rampInDuration, fadeAlpha: false);
        }

        public async UniTask HideAsync()
        {
            await this.RampAsync(1f, 0f, this.rampOutDuration, fadeAlpha: true);
            this.rawImage.enabled = false;
            this.ReleaseFrame();
        }

        // Intensity (distortion + noise) goes from -> to; with fadeAlpha the overlay also becomes
        // transparent in step, revealing the scene underneath.
        private async UniTask RampAsync(float from, float to, float duration, bool fadeAlpha)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float eased = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                this.SetIntensity(eased);
                if (fadeAlpha) this.SetAlpha(eased);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            this.SetIntensity(to);
            if (fadeAlpha) this.SetAlpha(to);
        }

        private void ReleaseFrame()
        {
            if (this.capturedFrame != null)
                Destroy(this.capturedFrame);
            this.capturedFrame = null;
        }

        private void SetIntensity(float value) => this.runtimeMaterial?.SetFloat(IntensityId, value);
        private void SetAlpha(float value)     => this.runtimeMaterial?.SetFloat(AlphaId, value);
    }
}
