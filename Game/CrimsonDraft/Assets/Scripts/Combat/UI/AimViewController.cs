#nullable enable

using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class AimViewController : MonoBehaviour, IAimView
    {
        #region Events

        public event Action<Vector2, ShotZone>? OnShotFired;

        #endregion

        #region Fields

        private enum AimPhase { VerticalAiming, HorizontalAiming, WaitingDismiss }

        [SerializeField] private RectTransform verticalSpace          = null!;
        [SerializeField] private Image         verticalSelector       = null!;
        [SerializeField] private RectTransform horizontalSpace        = null!;
        [SerializeField] private Image         horizontalSelector     = null!;
        [SerializeField] private RectTransform aimSpace               = null!;
        [SerializeField] private Image         silhouetteImage        = null!;
        [SerializeField] private GameObject    shotMarkerPrefab       = null!;
        [SerializeField] private GameObject    dispersionCirclePrefab = null!;
        [SerializeField] private float         speed                  = 0.8f;
        [SerializeField] private float         dimmingAlpha           = 0.3f;
        [SerializeField] private int           dispersionRadius       = 10;

        private AimPhase phase;
        private Vector2  confirmedLocalPos;
        private Vector2  pendingNormalizedShot;
        private ShotZone pendingZone;
        private Sprite?               activeZoneMaskSprite;
        private ShotZoneDefinition[]  activeZoneDefinitions = Array.Empty<ShotZoneDefinition>();
        private float                 activeColorTolerance  = 0.1f;
        private bool                  warnedMissingMaskConfig;
#if UNITY_EDITOR
        private bool     hasLastSample;
        private Vector3  lastSampleWorldPos;
        private Vector2Int lastSamplePixel;
        private Color    lastSampleColor;
        private string   lastSampleHex = "#000000";
#endif

        #endregion

        #region IAimView

        public void ConfigureHitMask(AimHitMaskProfile? profile)
        {
            this.warnedMissingMaskConfig = false;

            if (profile == null || profile.ZoneMaskSprite == null || profile.ZoneDefinitions == null || profile.ZoneDefinitions.Length == 0)
            {
                this.activeZoneMaskSprite  = null;
                this.activeZoneDefinitions = Array.Empty<ShotZoneDefinition>();
                this.activeColorTolerance  = 0.1f;
                return;
            }

            this.activeZoneMaskSprite  = profile.ZoneMaskSprite;
            this.activeZoneDefinitions = profile.ZoneDefinitions;
            this.activeColorTolerance  = profile.ColorTolerance;
        }

        public void Show()
        {
            this.gameObject.SetActive(true);
            this.StartVerticalOscillation();
            this.phase = AimPhase.VerticalAiming;
        }

        public void Confirm()
        {
            if (this.phase == AimPhase.VerticalAiming)
            {
                this.verticalSelector.rectTransform.DOKill();
                var vLocal = this.verticalSelector.rectTransform.localPosition;
                vLocal.y   = Mathf.Round(vLocal.y);
                this.verticalSelector.rectTransform.localPosition = vLocal;

                this.verticalSelector.DOFade(this.dimmingAlpha, 0.15f);
                this.StartHorizontalOscillation();
                this.phase = AimPhase.HorizontalAiming;
            }
            else if (this.phase == AimPhase.HorizontalAiming)
            {
                this.horizontalSelector.rectTransform.DOKill();
                var hLocal = this.horizontalSelector.rectTransform.localPosition;
                hLocal.x   = Mathf.Round(hLocal.x);
                this.horizontalSelector.rectTransform.localPosition = hLocal;

                this.horizontalSelector.DOFade(this.dimmingAlpha, 0.15f);

                var worldIntersection = new Vector3(
                    this.horizontalSelector.rectTransform.position.x,
                    this.verticalSelector.rectTransform.position.y,
                    this.aimSpace.position.z);
                var raw = this.aimSpace.InverseTransformPoint(worldIntersection);
                this.confirmedLocalPos = new Vector2(Mathf.Round(raw.x), Mathf.Round(raw.y));

                this.SpawnDispersionCircle(this.confirmedLocalPos);
                var shotLocal = this.ComputeRandomShotLocal();
                this.SpawnMarker(shotLocal);
                this.pendingNormalizedShot = this.NormalizeShotLocal(shotLocal);
                this.pendingZone           = this.SampleSilhouette(shotLocal);

                this.phase = AimPhase.WaitingDismiss;
            }
            else if (this.phase == AimPhase.WaitingDismiss)
            {
                this.OnShotFired?.Invoke(this.pendingNormalizedShot, this.pendingZone);
            }
        }

        public void Hide()
        {
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();

            foreach (Transform child in this.aimSpace)
                Destroy(child.gameObject);

            this.gameObject.SetActive(false);
        }

        #endregion

        #region Private

        private void StartVerticalOscillation()
        {
            float halfH = Mathf.Floor(this.verticalSpace.rect.height / 2f);
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.verticalSelector.DOFade(1f, 0f);
            this.verticalSelector.rectTransform.localPosition = new Vector3(0f, -halfH, 0f);
            this.verticalSelector.rectTransform
                .DOLocalMoveY(halfH, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void StartHorizontalOscillation()
        {
            float halfW = Mathf.Floor(this.horizontalSpace.rect.width / 2f);
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOFade(1f, 0f);
            this.horizontalSelector.rectTransform.localPosition = new Vector3(-halfW, 0f, 0f);
            this.horizontalSelector.rectTransform
                .DOLocalMoveX(halfW, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void SpawnMarker(Vector2 localPos)
        {
            var marker = Instantiate(this.shotMarkerPrefab, this.aimSpace);
            ((RectTransform)marker.transform).localPosition = new Vector3(
                Mathf.Round(localPos.x),
                Mathf.Round(localPos.y),
                0f);
        }

        private void SpawnDispersionCircle(Vector2 localPos)
        {
            var circle = Instantiate(this.dispersionCirclePrefab, this.aimSpace);
            var rt     = (RectTransform)circle.transform;
            rt.localPosition = new Vector3(Mathf.Round(localPos.x), Mathf.Round(localPos.y), 0f);
            circle.GetComponent<Image>().SetNativeSize();
        }

        private Vector2 ComputeRandomShotLocal()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r     = this.dispersionRadius * Mathf.Sqrt(UnityEngine.Random.value);
            return new Vector2(
                Mathf.Round(this.confirmedLocalPos.x + r * Mathf.Cos(angle)),
                Mathf.Round(this.confirmedLocalPos.y + r * Mathf.Sin(angle)));
        }

        private Vector2 NormalizeShotLocal(Vector2 localPos)
        {
            float halfW = this.aimSpace.rect.width  / 2f;
            float halfH = this.aimSpace.rect.height / 2f;
            return new Vector2(
                Mathf.Clamp01((localPos.x + halfW) / (halfW * 2f)),
                Mathf.Clamp01((localPos.y + halfH) / (halfH * 2f)));
        }

        private ShotZone SampleSilhouette(Vector2 shotLocal)
        {
            if (this.silhouetteImage == null)
                return ShotZone.Miss;
            if (this.activeZoneMaskSprite == null)
            {
                if (!this.warnedMissingMaskConfig)
                {
                    Debug.LogWarning("[AimView] Missing hit mask profile. Returning ShotZone.Miss until ConfigureHitMask(...) is provided.");
                    this.warnedMissingMaskConfig = true;
                }
                return ShotZone.Miss;
            }

            var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
            var silRt      = this.silhouetteImage.rectTransform;
            var localInSil = silRt.InverseTransformPoint(worldPos);
            var rect       = silRt.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return ShotZone.Miss;

            float u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
            float v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);

            var sprite = this.activeZoneMaskSprite;
            var tex    = sprite.texture;
            var pixelCoord = MapUvToTexturePixel(sprite, u, v);
            int px = pixelCoord.x;
            int py = pixelCoord.y;
            var texRect = sprite.textureRect;
            var pixel = tex.GetPixel(px, py);
            var zone = ResolveZone(pixel, this.activeZoneDefinitions, this.activeColorTolerance);
            string hex = $"#{ColorUtility.ToHtmlStringRGB(pixel)}";
            string spriteName = sprite.name;
            string textureName = tex.name;
            Debug.Log(
                $"[AimView] Sampled sprite='{spriteName}' texture='{textureName}' px=({px},{py}) color={hex} ({pixel}) -> Zone: {zone}");
#if UNITY_EDITOR
            float uCenter = Mathf.Clamp01(((px - texRect.xMin) + 0.5f) / texRect.width);
            float vCenter = Mathf.Clamp01(((py - texRect.yMin) + 0.5f) / texRect.height);
            float sampleX = Mathf.Lerp(rect.xMin, rect.xMax, uCenter);
            float sampleY = Mathf.Lerp(rect.yMin, rect.yMax, vCenter);
            this.hasLastSample    = true;
            this.lastSampleWorldPos = silRt.TransformPoint(new Vector3(sampleX, sampleY, 0f));
            this.lastSamplePixel  = new Vector2Int(px, py);
            this.lastSampleColor  = pixel;
            this.lastSampleHex    = hex;
#endif
            return zone;
        }

        internal static Vector2Int MapUvToTexturePixel(Sprite sprite, float u, float v)
        {
            var tex     = sprite.texture;
            var texRect = sprite.textureRect;
            int px = Mathf.Clamp(
                Mathf.RoundToInt(texRect.xMin + Mathf.Clamp01(u) * (texRect.width - 1f)),
                0,
                tex.width - 1);
            int py = Mathf.Clamp(
                Mathf.RoundToInt(texRect.yMin + Mathf.Clamp01(v) * (texRect.height - 1f)),
                0,
                tex.height - 1);
            return new Vector2Int(px, py);
        }

        internal static ShotZone ResolveZone(Color pixel, ShotZoneDefinition[] definitions, float tolerance)
        {
            if (definitions == null || definitions.Length == 0)
                return ShotZone.Miss;

            float bestDistSq = float.MaxValue;
            ShotZone bestZone = ShotZone.Miss;
            bool found = false;

            foreach (var def in definitions)
            {
                float dr = pixel.r - def.color.r;
                float dg = pixel.g - def.color.g;
                float db = pixel.b - def.color.b;
                float distSq = dr * dr + dg * dg + db * db;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestZone   = def.zone;
                    found      = true;
                }
            }

            return (found && bestDistSq <= tolerance * tolerance) ? bestZone : ShotZone.Miss;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (this.aimSpace == null) return;

            float radius = this.dispersionRadius * this.aimSpace.lossyScale.x;
            var   center = this.aimSpace.TransformPoint(this.confirmedLocalPos);
            UnityEditor.Handles.color = new Color(0f, 0.9f, 1f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, radius);

            if (this.hasLastSample)
            {
                float markerRadius = Mathf.Max(2f * this.aimSpace.lossyScale.x, 0.01f);
                UnityEditor.Handles.color = this.lastSampleColor;
                UnityEditor.Handles.DrawSolidDisc(this.lastSampleWorldPos, Vector3.forward, markerRadius);
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawWireDisc(this.lastSampleWorldPos, Vector3.forward, markerRadius * 1.8f);
                UnityEditor.Handles.Label(
                    this.lastSampleWorldPos + new Vector3(0.1f, 0.1f, 0f),
                    $"px:{this.lastSamplePixel.x},{this.lastSamplePixel.y} color:{this.lastSampleHex}");
            }
        }
#endif

        #endregion
    }
}
