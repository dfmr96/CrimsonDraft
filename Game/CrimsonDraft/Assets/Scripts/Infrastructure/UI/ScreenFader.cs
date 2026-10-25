#nullable enable

using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.UI
{
    public sealed class ScreenFader : IInitializable
    {
        private const float FadeDuration        = 0.4f;
        private const float LoadFadeDuration    = 1f;   // Slow fade to/from black around a scene load, so it actually reads.
        private const float LoadingViewFade     = 0.7f; // Loading screen fading in over the black / out into it.
        private const float MinLoadingDuration  = 3f; // Floor so the loading screen never just flashes on a fast/cached load.
        private const float MaxAnimationStep   = 1f / 20f; // A load hitch advances a fade by at most this, so it is never skipped.
        private const string LoadingSceneName  = "Loading"; // Near-empty scene swapped in while the real one loads.
        private const string MainMenuSceneName = "MainMenu";

        private CanvasGroup? canvasGroup;
        private TMP_Text?    endMessageText;
        private readonly LoadingScreenView loadingScreen;
        private bool         isLoadingScene;

        [Preserve]
        public ScreenFader(LoadingScreenView loadingScreen)
        {
            this.loadingScreen = loadingScreen;
        }

        void IInitializable.Initialize()
        {
            var root = new GameObject("ScreenFader");
            Object.DontDestroyOnLoad(root);

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(root.transform, false);

            var image = overlay.AddComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            this.canvasGroup = overlay.AddComponent<CanvasGroup>();
            this.canvasGroup.alpha           = 0f;
            this.canvasGroup.blocksRaycasts  = false;
            this.canvasGroup.interactable    = false;

            var textGo = new GameObject("EndMessageText");
            textGo.transform.SetParent(overlay.transform, false);

            this.endMessageText = textGo.AddComponent<TextMeshProUGUI>();
            this.endMessageText.alignment    = TextAlignmentOptions.Center;
            this.endMessageText.fontSize     = 48f;
            this.endMessageText.color        = Color.white;
            this.endMessageText.raycastTarget = false;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            textGo.SetActive(false);

            this.loadingScreen.Hide();
        }

        /// <summary>
        /// Fades to black, loads <paramref name="sceneName"/> as the only scene while showing an
        /// random quote and an animated loading label, then fades back in. The fade-out hides the load's
        /// GC/shader-warmup hitch behind a solid color instead of letting it read as a freeze,
        /// and the dot animation plus MinLoadingDuration floor keep the screen visibly alive
        /// even when the load itself is nearly instant.
        /// </summary>
        /// <param name="fadeOutDuration">How long the fade to black takes before the load starts; the
        /// main menu passes a long one so the way into the game reads as a slow fade.</param>
        public async UniTask LoadSceneAsync(string sceneName, float fadeOutDuration = LoadFadeDuration)
        {
            if (this.isLoadingScene) return;
            this.isLoadingScene = true;

            try
            {
                await FadeOutAsync(fadeOutDuration);
                // Swap to a near-empty scene first so the outgoing (heavy) scene is released
                // before the destination starts loading, instead of both sitting in memory at
                // once. The loading view itself is a persistent prefab, so the scene is empty.
                // It happens while the screen is still solid black and before the loading view
                // starts fading in: tearing the old scene down stalls the frames, which used to
                // freeze that fade half-way and leave the screen dimmed for a while.
                if (Application.CanStreamedLevelBeLoaded(LoadingSceneName))
                {
                    var swap = SceneManager.LoadSceneAsync(LoadingSceneName, LoadSceneMode.Single);
                    while (swap != null && !swap.isDone)
                        await UniTask.Yield(PlayerLoopTiming.Update);

                    // The first frame after a scene swap carries the teardown hitch; let it pass.
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // The loading view always fades in completely, stays at least MinLoadingDuration and
                // always fades out completely, however fast the scene is ready: the destination only
                // starts loading once the fade-in is over, and is only activated after the fade-out.
                await this.loadingScreen.ShowAsync(LoadingViewFade);

                var operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                operation.allowSceneActivation = false;

                float elapsed = 0f;
                while (operation.progress < 0.9f || elapsed < MinLoadingDuration)
                {
                    elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationStep);
                    this.loadingScreen.UpdateLabel(elapsed);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                await this.loadingScreen.HideAsync(LoadingViewFade);

                operation.allowSceneActivation = true;
                await operation.ToUniTask();

                // The activation frame carries the hitch of the new scene; let it pass so the
                // fade-in that follows is not eaten by it.
                await UniTask.Yield(PlayerLoopTiming.Update);
                await FadeInAsync(LoadFadeDuration);
            }
            finally
            {
                this.isLoadingScene = false;
            }
        }

        public async UniTask FadeOutAsync(float duration = FadeDuration)
        {
            if (this.canvasGroup == null) return;
            this.canvasGroup.blocksRaycasts = true;
            await AnimateAlpha(0f, 1f, duration);
        }

        public async UniTask FadeInAsync(float duration = FadeDuration)
        {
            if (this.canvasGroup == null) return;
            await AnimateAlpha(1f, 0f, duration);
            this.canvasGroup.blocksRaycasts = false;
        }

        public async UniTask ShowEndScreenAsync(string message)
        {
            await FadeOutAsync();

            if (this.endMessageText != null)
            {
                this.endMessageText.text = message;
                this.endMessageText.gameObject.SetActive(true);
            }

            Time.timeScale = 0f;

            var tcs = new UniTaskCompletionSource();
            using var subscription = InputSystem.onAnyButtonPress.CallOnce(_ => tcs.TrySetResult());
            await tcs.Task;

            Time.timeScale = 1f;

            if (this.endMessageText != null)
                this.endMessageText.gameObject.SetActive(false);

            // Async rather than the bare synchronous LoadScene(Single) this used to call --
            // that tears the gameplay scene down and activates MainMenu in the same frame,
            // with no gap for Wwise to unregister the old AkAudioListener before the new one
            // registers, which drops all audio in the destination scene. The screen is already
            // held at black here (FadeOutAsync ran above), so this only swaps the load itself
            // for the async form -- not the full LoadSceneAsync() helper, which would fade out
            // a second time and flash the screen visible again first.
            var operation = SceneManager.LoadSceneAsync(MainMenuSceneName, LoadSceneMode.Single);
            await operation.ToUniTask();
            await FadeInAsync();
        }

        private async UniTask AnimateAlpha(float from, float to, float duration)
        {
            if (this.canvasGroup == null) return;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationStep);
                this.canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            this.canvasGroup.alpha = to;
        }
    }
}