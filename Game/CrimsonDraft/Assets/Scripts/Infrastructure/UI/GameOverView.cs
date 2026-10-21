#nullable enable

using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using CrimsonDraft.UI;

namespace CrimsonDraft.Infrastructure.UI
{
    public sealed class GameOverView : MonoBehaviour
    {
        private const string UiLayerName = "UICRTOnly";

        [SerializeField] private GameObject root      = null!;
        [SerializeField] private TMP_Text   titleText = null!;

        [SerializeField] private float secondsPerChar = 0.12f;
        [SerializeField] private float holdAfterTyped = 0.5f;

        private Canvas? canvas;

        void Awake()
        {
            this.canvas = GetComponent<Canvas>();
        }

        public void Hide()
        {
            this.root.SetActive(false);
        }

        /// <summary>Shows the screen with an empty title, routed through the UI CRT camera so
        /// the UI volume filter applies.</summary>
        public void Show()
        {
            BindToUiCamera();

            this.titleText.maxVisibleCharacters = 0;
            this.root.SetActive(true);
        }

        /// <summary>Types the title out char by char, then holds.</summary>
        public async UniTask PlayAsync(InventorySfxData? sfx = null)
        {
            string full = this.titleText.text;

            for (int i = 1; i <= full.Length; i++)
            {
                this.titleText.maxVisibleCharacters = i;
                if (!char.IsWhiteSpace(full[i - 1]))
                    sfx?.PlayVoiceTypewriter(gameObject);
                await UniTask.Delay(TimeSpan.FromSeconds(this.secondsPerChar), DelayType.UnscaledDeltaTime);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(this.holdAfterTyped), DelayType.UnscaledDeltaTime);
        }

        // The view is a persistent (DontDestroyOnLoad) prefab, so it can't reference the scene's
        // "UI CRT Camera" directly: find it each time and move the whole hierarchy onto its layer.
        private void BindToUiCamera()
        {
            int layer = LayerMask.NameToLayer(UiLayerName);
            if (layer < 0 || this.canvas == null) return;

            Camera? uiCamera = null;
            foreach (var cam in Camera.allCameras)
            {
                if (cam.targetTexture != null || (cam.cullingMask & (1 << layer)) == 0) continue;

                uiCamera ??= cam;
                if (cam.cullingMask == (1 << layer)) { uiCamera = cam; break; } // the dedicated UI camera
            }
            if (uiCamera == null)
            {
                Debug.LogWarning("GameOverView: no camera renders the UICRTOnly layer; staying in overlay mode.");
                return;
            }

            foreach (var t in GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;

            this.canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            this.canvas.worldCamera   = uiCamera;
            this.canvas.planeDistance = 1f;
        }
    }
}
