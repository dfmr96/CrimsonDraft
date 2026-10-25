#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.UI
{
    /// <summary>
    /// Old-CRT "page change" distortion for an inventory card. Plays a short 0 -> 1 -> 0 glitch on
    /// every Image under this object using CrimsonDraft/UI/ScreenSwitchGlitch, and runs the swap
    /// callback at the peak so the content change is hidden behind the distortion.
    /// </summary>
    public sealed class CardSwitchGlitch : MonoBehaviour
    {
        private static readonly int GlitchAmountId = Shader.PropertyToID("_GlitchAmount");
        private static readonly int GlitchSeedId   = Shader.PropertyToID("_GlitchSeed");
        private static readonly int SpriteRectId   = Shader.PropertyToID("_SpriteRect");

        [Tooltip("Material using CrimsonDraft/UI/ScreenSwitchGlitch. Copied per image while the effect plays.")]
        [SerializeField] private Material? glitchMaterial;
        [SerializeField, Min(0.01f)] private float rampUpDuration   = 0.12f;
        [SerializeField, Min(0.01f)] private float rampDownDuration = 0.2f;
        [SerializeField, Min(1f)]    private float seedRate         = 24f;
        [Tooltip("How strong the distortion gets at its peak (1 = full, the inventory's look). Lower for a subtler refresh.")]
        [SerializeField, Range(0f, 1f)] private float peakAmount = 1f;
        [Tooltip("Whole-card alpha flicker at the peak, 0 = off.")]
        [SerializeField, Range(0f, 1f)] private float peakFlicker = 0.35f;

        private CanvasGroup? flickerGroup;
        private float        restAlpha = 1f;
        private Coroutine?   running;
        private Action?      pendingSwap;

        private Image[]    images    = Array.Empty<Image>();
        private Material?[] restMats = Array.Empty<Material?>();
        private Material?[] fxMats   = Array.Empty<Material?>();

        public bool IsPlaying => this.running != null;

        /// <summary>Plays the glitch and calls <paramref name="onPeak"/> once, at full distortion.</summary>
        public void Play(Action onPeak)
        {
            if (this.glitchMaterial == null || !isActiveAndEnabled)
            {
                onPeak();
                return;
            }

            // A second request mid-effect: land the first swap immediately, then restart cleanly.
            if (this.running != null)
            {
                StopCoroutine(this.running);
                this.running = null;
                Finish();
            }

            this.pendingSwap = onPeak;
            this.running     = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            Begin();

            float t = 0f;
            while (t < this.rampUpDuration)
            {
                t += Time.unscaledDeltaTime;
                Apply(Mathf.Clamp01(t / this.rampUpDuration));
                yield return null;
            }

            Apply(1f);
            var swap = this.pendingSwap;
            this.pendingSwap = null;
            swap?.Invoke();
            // The swap may have activated images that weren't being glitched (Bio panel): pick them up.
            Rebind();

            t = 0f;
            while (t < this.rampDownDuration)
            {
                t += Time.unscaledDeltaTime;
                Apply(1f - Mathf.Clamp01(t / this.rampDownDuration));
                yield return null;
            }

            this.running = null;
            Finish();
        }

        private void Begin()
        {
            this.flickerGroup = GetComponent<CanvasGroup>();
            // The card fade (MIA) lives on this same group: flicker around it, then put it back.
            this.restAlpha = this.flickerGroup != null ? this.flickerGroup.alpha : 1f;
            Rebind();
        }

        // (Re)collects every Image that uses the default UI material and gives it its own glitch copy.
        private void Rebind()
        {
            RestoreMaterials();

            var found = new List<Image>();
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                // Images with a custom material (ECG trace, panel glitch) keep their own look.
                if (img.material != null && img.material != Graphic.defaultGraphicMaterial) continue;
                found.Add(img);
            }

            this.images  = found.ToArray();
            this.restMats = new Material?[this.images.Length];
            this.fxMats   = new Material?[this.images.Length];

            for (int i = 0; i < this.images.Length; i++)
            {
                var img = this.images[i];
                this.restMats[i] = img.material;
                var mat = new Material(this.glitchMaterial!) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetVector(SpriteRectId, GetSpriteUvRect(img.sprite));
                img.material = mat;
                this.fxMats[i] = mat;
            }
        }

        private void Apply(float amount)
        {
            float seed = Mathf.Floor(Time.unscaledTime * this.seedRate);
            for (int i = 0; i < this.fxMats.Length; i++)
            {
                var mat = this.fxMats[i];
                if (mat == null) continue;
                mat.SetFloat(GlitchAmountId, amount * this.peakAmount);
                mat.SetFloat(GlitchSeedId, seed + i * 0.37f);
            }

            if (this.flickerGroup != null && this.peakFlicker > 0f)
            {
                float flicker = amount > 0.6f && ((int)seed & 1) == 0 ? this.peakFlicker * amount : 0f;
                this.flickerGroup.alpha = this.restAlpha * (1f - flicker);
            }
        }

        private void Finish()
        {
            RestoreMaterials();
            if (this.pendingSwap != null)
            {
                var swap = this.pendingSwap;
                this.pendingSwap = null;
                swap();
            }
        }

        private void RestoreMaterials()
        {
            for (int i = 0; i < this.images.Length; i++)
            {
                if (this.images[i] != null) this.images[i].material = this.restMats[i];
                if (this.fxMats[i] != null) Destroy(this.fxMats[i]);
            }
            this.images  = Array.Empty<Image>();
            this.restMats = Array.Empty<Material?>();
            this.fxMats   = Array.Empty<Material?>();

            if (this.flickerGroup != null) this.flickerGroup.alpha = this.restAlpha;
        }

        private void OnDisable()
        {
            if (this.running != null)
            {
                StopCoroutine(this.running);
                this.running = null;
            }
            Finish();
        }

        private static Vector4 GetSpriteUvRect(Sprite? sprite)
        {
            if (sprite == null || sprite.texture == null) return new Vector4(0f, 0f, 1f, 1f);

            var rect = sprite.textureRect;
            int w = sprite.texture.width, h = sprite.texture.height;
            if (w <= 0 || h <= 0) return new Vector4(0f, 0f, 1f, 1f);

            return new Vector4(rect.xMin / w, rect.yMin / h, rect.xMax / w, rect.yMax / h);
        }
    }
}
