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
        private const float FadeDuration = 0.4f;
        private const string MainMenuSceneName = "MainMenu";

        private CanvasGroup? canvasGroup;
        private TMP_Text?    endMessageText;

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

            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
            await FadeInAsync();
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
