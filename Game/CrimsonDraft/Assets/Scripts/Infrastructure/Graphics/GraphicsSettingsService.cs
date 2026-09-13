#nullable enable

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.Graphics
{
    /// <summary>
    /// Persists gamma the same way AudioSettingsService persists volume -- PlayerPrefs, applied
    /// from a DontDestroyOnLoad singleton (see GameLifetimeScope). Unlike Wwise's RTPCs, a URP
    /// Volume is scene-local (each scene has its own, e.g. MainMenu vs Deck_B_Development use
    /// different profile assets), so it can't just be set once -- it has to be re-applied to
    /// whichever Volume is active every time a scene loads.
    /// </summary>
    public sealed class GraphicsSettingsService : IGraphicsSettingsService, IInitializable, System.IDisposable
    {
        private const string GammaKey          = "Graphics.Gamma";
        private const float  DefaultGamma      = 0.5f;
        private const float  GammaOffsetRange  = 0.5f;

        public float Gamma { get; private set; }

        private int suppressionCount;

        [Preserve]
        public GraphicsSettingsService() { }

        void IInitializable.Initialize()
        {
            this.Gamma = PlayerPrefs.GetFloat(GammaKey, DefaultGamma);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply();
        }

        public void Dispose() => SceneManager.sceneLoaded -= OnSceneLoaded;

        public void SetGamma(float value01)
        {
            this.Gamma = value01;
            PlayerPrefs.SetFloat(GammaKey, value01);
            Apply();
        }

        public void PushGammaSuppression()
        {
            this.suppressionCount++;
            Apply();
        }

        public void PopGammaSuppression()
        {
            this.suppressionCount = Mathf.Max(0, this.suppressionCount - 1);
            Apply();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Pause/Inventory are always closed by the time a scene has genuinely finished
            // loading fresh -- reset defensively so a suppressor that skipped its matching Pop
            // (an unhandled exit path, a future bug) can't leave gamma stuck neutral forever.
            this.suppressionCount = 0;
            Apply();
        }

        private void Apply()
        {
            float offset = this.suppressionCount > 0 ? 0f : Mathf.Lerp(-GammaOffsetRange, GammaOffsetRange, this.Gamma);

            // A scene can have more than one global Volume (e.g. Deck_B_Development layers an
            // "InventoryVolume" over the base "Global Volume") -- both blend into the final
            // image, so gamma has to land on every one of them, not just whichever the search
            // happens to return first.
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                // Scene transitions can hand back a reference mid-destroy (old scene tearing
                // down as the new one finishes loading) -- Unity's overridden == catches that
                // "fake null" safely, a direct property read on it would throw instead.
                if (volume == null || !volume.isGlobal || volume.profile == null) continue;
                if (!volume.profile.TryGet(out LiftGammaGain liftGammaGain)) continue;

                Vector4 gamma = liftGammaGain.gamma.value;
                gamma.w = offset;
                liftGammaGain.gamma.value = gamma;
            }
        }
    }
}
