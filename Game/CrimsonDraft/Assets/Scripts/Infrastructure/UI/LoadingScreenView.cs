#nullable enable

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    /// <summary>Loading screen: a random quote with its author and work, plus an animated
    /// "LOADING" label. Persistent (DontDestroyOnLoad) prefab driven by <see cref="ScreenFader"/>.</summary>
    public sealed class LoadingScreenView : MonoBehaviour
    {
        [Serializable]
        public struct Quote
        {
            [TextArea(2, 4)] public string text;
            public string author;
            public string source;
        }

        private const float DotInterval = 0.35f;
        private const float MaxAnimationStep = 1f / 20f;

        [SerializeField] private GameObject root            = null!;
        [SerializeField] private TMP_Text   quoteText       = null!;
        [SerializeField] private TMP_Text   attributionText = null!;
        [SerializeField] private TMP_Text?  loadingLabel;
        [SerializeField] private string     loadingWord     = "LOADING";
        [Tooltip("Camera that renders the content canvas into the CRT render texture. Its target is cleared on Hide so the next Show never flashes the previous quote.")]
        [SerializeField] private Camera?    uiCamera;
        [SerializeField] private Quote[]    quotes          = Array.Empty<Quote>();

        private int lastIndex = -1;
        private RawImage? composite;

        void Awake() => this.composite = GetComponentInChildren<RawImage>(true);

        /// <summary>Picks a quote and fades the screen in from transparent.</summary>
        public async UniTask ShowAsync(float fadeDuration)
        {
            PickQuote();
            UpdateLabel(0f);
            SetAlpha(0f);
            this.root.SetActive(true);
            await FadeAsync(0f, 1f, fadeDuration);
        }

        /// <summary>Fades the screen out, then hides it.</summary>
        public async UniTask HideAsync(float fadeDuration)
        {
            await FadeAsync(1f, 0f, fadeDuration);
            Hide();
        }

        private async UniTask FadeAsync(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxAnimationStep); // a hitch must not skip the fade
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration))));
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            SetAlpha(to);
        }

        private void SetAlpha(float alpha)
        {
            if (this.composite == null) return;
            var c = this.composite.color;
            c.a = alpha;
            this.composite.color = c;
        }

        public void Show()
        {
            PickQuote();
            UpdateLabel(0f);
            SetAlpha(1f);
            this.root.SetActive(true);
        }

        public void Hide()
        {
            this.root.SetActive(false);
            ClearTarget();
        }

        private void ClearTarget()
        {
            var target = this.uiCamera != null ? this.uiCamera.targetTexture : null;
            if (target == null) return;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;
        }

        public void UpdateLabel(float elapsed)
        {
            if (this.loadingLabel == null) return;

            int dots = Mathf.FloorToInt(elapsed / DotInterval) % 4;
            this.loadingLabel.text = this.loadingWord + new string('.', dots);
        }

        // Random quote, never the same one twice in a row.
        private void PickQuote()
        {
            if (this.quotes.Length == 0)
            {
                this.quoteText.text       = string.Empty;
                this.attributionText.text = string.Empty;
                return;
            }

            int index = UnityEngine.Random.Range(0, this.quotes.Length);
            if (this.quotes.Length > 1 && index == this.lastIndex)
                index = (index + 1 + UnityEngine.Random.Range(0, this.quotes.Length - 1)) % this.quotes.Length;
            this.lastIndex = index;

            var q = this.quotes[index];
            this.quoteText.text = $"“{q.text}”";
            this.attributionText.text = string.IsNullOrEmpty(q.source)
                ? $"— {q.author}"
                : $"— {q.author}, <i>{q.source}</i>";
        }
    }
}
