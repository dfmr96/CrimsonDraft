#nullable enable

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UI;
using VContainer.Unity;

namespace CrimsonDraft.Infrastructure.UI
{
    public sealed class ScreenFader : IInitializable
    {
        private const float FadeDuration = 0.4f;

        private CanvasGroup? canvasGroup;

        [Preserve]
        public ScreenFader() { }

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
        }

        public async UniTask FadeOutAsync()
        {
            if (this.canvasGroup == null) return;
            this.canvasGroup.blocksRaycasts = true;
            await AnimateAlpha(0f, 1f);
        }

        public async UniTask FadeInAsync()
        {
            if (this.canvasGroup == null) return;
            await AnimateAlpha(1f, 0f);
            this.canvasGroup.blocksRaycasts = false;
        }

        private async UniTask AnimateAlpha(float from, float to)
        {
            if (this.canvasGroup == null) return;
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.deltaTime;
                this.canvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / FadeDuration));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            this.canvasGroup.alpha = to;
        }
    }
}
