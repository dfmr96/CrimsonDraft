#nullable enable

using System;
using System.Collections.Generic;
using CrimsonDraft.Inventory;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CrimsonDraft.Combat
{
    public sealed class AimViewController : MonoBehaviour, IAimView
    {
        #region Events

        public event Action<ResolvedShot[]>? OnShotsResolved;

        #endregion

        #region Fields

        private enum AimPhase { VerticalAiming, HorizontalAiming, WaitingResolve, ResolvingSequence, WaitingDismiss }

        [SerializeField] private RectTransform verticalSpace          = null!;
        [SerializeField] private Image         verticalSelector       = null!;
        [SerializeField] private RectTransform horizontalSpace        = null!;
        [SerializeField] private Image         horizontalSelector     = null!;
        [SerializeField] private RectTransform aimSpace               = null!;
        [SerializeField] private Image         silhouetteImage        = null!;
        [SerializeField] private GameObject    shotMarkerPrefab       = null!;
        [SerializeField] private GameObject    dispersionCirclePrefab = null!;
        [SerializeField] private RectTransform feedbackRoot           = null!;
        [SerializeField] private GameObject    feedbackTextPrefab     = null!;
        [SerializeField] private float         speed                  = 0.8f;
        [SerializeField] private float         dimmingAlpha           = 0.3f;
        [SerializeField] private Vector2       feedbackOffset         = new Vector2(0f, 24f);
        [SerializeField] private float         feedbackHoldDuration   = 0.25f;
        [SerializeField] private float         feedbackDuration       = 0.6f;
        [SerializeField] private float         bulletSequenceDelay    = 0.03f;
        [SerializeField] private float         perBulletYOffset       = 5f;
        [SerializeField] private int           maxConcurrentFeedback  = 3;
        [SerializeField] private Color         hitFeedbackColor       = Color.white;
        [SerializeField] private Color         missFeedbackColor      = new Color(0.8f, 0.8f, 0.8f, 1f);

        private AimPhase phase;
        private Vector2  confirmedLocalPos;
        private int      shotCount              = 1;
        private int      activeDispersionRadius = 10;
        private Sprite?  activeDispersionSprite;
        private ResolvedShot[] pendingResolvedShots = Array.Empty<ResolvedShot>();
        private bool isResolvingSequence;

        private Sprite?              activeZoneMaskSprite;
        private ShotZoneDefinition[] activeZoneDefinitions = Array.Empty<ShotZoneDefinition>();
        private float                activeColorTolerance  = 0.1f;
        private bool                 warnedMissingMaskConfig;
        private readonly List<GameObject> activeFeedback = new List<GameObject>();
#if UNITY_EDITOR
        private bool       hasLastSample;
        private Vector3    lastSampleWorldPos;
        private Vector2Int lastSamplePixel;
        private Color      lastSampleColor;
        private string     lastSampleHex = "#000000";
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

        public void ConfigureWeapon(WeaponData? weaponData)
        {
            this.activeDispersionRadius = weaponData?.DispersionRadius ?? 10;
            this.activeDispersionSprite = weaponData?.DispersionCircleSprite;
        }

        public void SetShotCount(int shotCount)
        {
            this.shotCount = Mathf.Max(1, shotCount);
        }

        public void Show()
        {
            this.gameObject.SetActive(true);
            this.StartVerticalOscillation();
            this.phase = AimPhase.VerticalAiming;
            this.pendingResolvedShots = Array.Empty<ResolvedShot>();
            this.isResolvingSequence = false;
        }

        public void ShowShotFeedback(Vector2 normalizedPos, int damage, bool isMiss) =>
            this.SpawnShotFeedbackVisual(normalizedPos, damage, isMiss);

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
                return;
            }

            if (this.phase == AimPhase.HorizontalAiming)
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

                var firstShotLocal = this.ComputeRandomShotLocal();
                this.pendingResolvedShots = this.BuildResolvedShots(firstShotLocal, this.shotCount);
                this.phase = AimPhase.WaitingResolve;
                return;
            }

            if (this.phase == AimPhase.WaitingResolve && !this.isResolvingSequence)
                this.ResolvePendingShotsAsync().Forget();
        }

        public void Hide()
        {
            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.DetachFeedbackFromAimView();

            foreach (Transform child in this.aimSpace)
                Destroy(child.gameObject);

            this.pendingResolvedShots = Array.Empty<ResolvedShot>();
            this.isResolvingSequence = false;
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

        private ResolvedShot[] BuildResolvedShots(Vector2 firstShotLocal, int count)
        {
            int clampedCount = Mathf.Max(1, count);
            var resolved     = new ResolvedShot[clampedCount];
            for (int i = 0; i < clampedCount; i++)
            {
                Vector2             shotLocal  = ComputeBulletLocalFromPrimary(firstShotLocal, i, this.perBulletYOffset);
                Vector2             normalized = this.NormalizeShotLocal(shotLocal);
                ShotZoneDefinition? def        = this.SampleSilhouette(shotLocal);
                ShotZone            zone       = def?.zone ?? ShotZone.Miss;
                ShotPrecision       precision  = def?.precisionEntry.precision ?? ShotPrecision.Normal;
                float               precMult   = def.HasValue ? (def.Value.precisionEntry.multiplier <= 0f ? 1f : def.Value.precisionEntry.multiplier) : 0f;
                int                 damage     = CombatMenuController.ComputeShotDamage(zone, precMult);
                resolved[i] = new ResolvedShot(i, normalized, zone, precision, damage);
            }
            return resolved;
        }

        private async UniTaskVoid ResolvePendingShotsAsync()
        {
            this.isResolvingSequence = true;
            this.phase = AimPhase.ResolvingSequence;

            if (this.pendingResolvedShots.Length == 0)
            {
                this.OnShotsResolved?.Invoke(Array.Empty<ResolvedShot>());
                this.isResolvingSequence = false;
                this.phase = AimPhase.WaitingDismiss;
                return;
            }

            for (int i = 0; i < this.pendingResolvedShots.Length; i++)
            {
                var shot = this.pendingResolvedShots[i];
                Vector2 local = this.DenormalizeShotLocal(shot.NormalizedPos);
                this.SpawnMarker(local);
                this.SpawnShotFeedbackVisual(shot.NormalizedPos, shot.Damage, shot.Zone == ShotZone.Miss);

                if (i < this.pendingResolvedShots.Length - 1 && this.bulletSequenceDelay > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(this.bulletSequenceDelay));
            }

            this.OnShotsResolved?.Invoke(this.pendingResolvedShots);
            this.isResolvingSequence = false;
            this.phase = AimPhase.WaitingDismiss;
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
            var img = circle.GetComponent<Image>();
            if (this.activeDispersionSprite != null)
                img.sprite = this.activeDispersionSprite;
            img.SetNativeSize();
        }

        private void SpawnShotFeedbackVisual(Vector2 normalizedPos, int damage, bool isMiss)
        {
            if (this.feedbackTextPrefab == null)
            {
                Debug.LogWarning("[AimView] Feedback text prefab is not assigned.");
                return;
            }

            this.PruneDeadFeedback();
            if (this.maxConcurrentFeedback > 0)
            {
                while (this.activeFeedback.Count >= this.maxConcurrentFeedback)
                {
                    var oldest = this.activeFeedback[0];
                    this.activeFeedback.RemoveAt(0);
                    if (oldest != null)
                        Destroy(oldest);
                }
            }

            var go = Instantiate(this.feedbackTextPrefab, this.aimSpace);
            this.activeFeedback.Add(go);

            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                Vector2 localShot = this.DenormalizeShotLocal(normalizedPos);
                rt.localPosition = new Vector3(
                    Mathf.Round(localShot.x + this.feedbackOffset.x),
                    Mathf.Round(localShot.y + this.feedbackOffset.y),
                    0f);
            }

            var text = go.GetComponent<TMP_Text>() ?? go.GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                Debug.LogWarning("[AimView] Feedback prefab must include TMP_Text.");
                this.activeFeedback.Remove(go);
                Destroy(go);
                return;
            }

            text.text = isMiss ? "MISS" : $"-{damage}";
            var baseColor = isMiss ? this.missFeedbackColor : this.hitFeedbackColor;
            baseColor.a = 1f;
            text.color = baseColor;

            float hold = Mathf.Max(0f, this.feedbackHoldDuration);
            float fade = Mathf.Max(0f, this.feedbackDuration);

            DOVirtual.DelayedCall(hold, () =>
            {
                if (text != null)
                    text.DOFade(0f, fade).SetEase(Ease.OutQuad);
            });

            DOVirtual.DelayedCall(hold + fade, () =>
            {
                this.activeFeedback.Remove(go);
                if (go != null)
                {
                    go.transform.DOKill();
                    text.DOKill();
                    Destroy(go);
                }
            });
        }

        private Vector2 ComputeRandomShotLocal()
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float r     = this.activeDispersionRadius * Mathf.Sqrt(UnityEngine.Random.value);
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

        private Vector2 DenormalizeShotLocal(Vector2 normalizedPos)
        {
            float halfW = this.aimSpace.rect.width / 2f;
            float halfH = this.aimSpace.rect.height / 2f;
            return new Vector2(
                Mathf.Lerp(-halfW, halfW, Mathf.Clamp01(normalizedPos.x)),
                Mathf.Lerp(-halfH, halfH, Mathf.Clamp01(normalizedPos.y)));
        }

        private void DetachFeedbackFromAimView()
        {
            var overlayRoot = this.feedbackRoot != null
                ? this.feedbackRoot
                : (this.transform.parent as RectTransform);
            if (overlayRoot == null)
                return;

            foreach (var feedback in this.activeFeedback)
            {
                if (feedback != null)
                    feedback.transform.SetParent(overlayRoot, worldPositionStays: true);
            }
        }

        private void PruneDeadFeedback()
        {
            for (int i = this.activeFeedback.Count - 1; i >= 0; i--)
            {
                if (this.activeFeedback[i] == null)
                    this.activeFeedback.RemoveAt(i);
            }
        }

        internal static Vector2 ComputeBulletLocalFromPrimary(Vector2 primaryLocal, int bulletIndex, float perBulletYOffset) =>
            new Vector2(
                Mathf.Round(primaryLocal.x),
                Mathf.Round(primaryLocal.y + Mathf.Max(0, bulletIndex) * perBulletYOffset));

        private ShotZoneDefinition? SampleSilhouette(Vector2 shotLocal)
        {
            if (this.silhouetteImage == null)
                return null;
            if (this.activeZoneMaskSprite == null)
            {
                if (!this.warnedMissingMaskConfig)
                {
                    Debug.LogWarning("[AimView] Missing hit mask profile. Returning null until ConfigureHitMask(...) is provided.");
                    this.warnedMissingMaskConfig = true;
                }
                return null;
            }

            var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
            var silRt      = this.silhouetteImage.rectTransform;
            var localInSil = silRt.InverseTransformPoint(worldPos);
            var rect       = silRt.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return null;

            float u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
            float v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);

            var sprite     = this.activeZoneMaskSprite;
            var tex        = sprite.texture;
            var pixelCoord = MapUvToTexturePixel(sprite, u, v);
            int px         = pixelCoord.x;
            int py         = pixelCoord.y;
            var texRect    = sprite.textureRect;
            var pixel      = tex.GetPixel(px, py);
            var def        = ResolveZone(pixel, this.activeZoneDefinitions, this.activeColorTolerance);
            string hex         = $"#{ColorUtility.ToHtmlStringRGB(pixel)}";
            string spriteName  = sprite.name;
            string textureName = tex.name;
            Debug.Log(
                $"[AimView] Sampled sprite='{spriteName}' texture='{textureName}' px=({px},{py}) color={hex} ({pixel}) -> Zone: {def?.zone} Precision: {def?.precisionEntry.precision}");
#if UNITY_EDITOR
            float uCenter = Mathf.Clamp01(((px - texRect.xMin) + 0.5f) / texRect.width);
            float vCenter = Mathf.Clamp01(((py - texRect.yMin) + 0.5f) / texRect.height);
            float sampleX = Mathf.Lerp(rect.xMin, rect.xMax, uCenter);
            float sampleY = Mathf.Lerp(rect.yMin, rect.yMax, vCenter);
            this.hasLastSample      = true;
            this.lastSampleWorldPos = silRt.TransformPoint(new Vector3(sampleX, sampleY, 0f));
            this.lastSamplePixel    = new Vector2Int(px, py);
            this.lastSampleColor    = pixel;
            this.lastSampleHex      = hex;
#endif
            return def;
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

        internal static ShotZoneDefinition? ResolveZone(Color pixel, ShotZoneDefinition[] definitions, float tolerance)
        {
            if (definitions == null || definitions.Length == 0)
                return null;

            float bestDistSq = float.MaxValue;
            int   bestIdx    = -1;

            for (int i = 0; i < definitions.Length; i++)
            {
                float dr     = pixel.r - definitions[i].color.r;
                float dg     = pixel.g - definitions[i].color.g;
                float db     = pixel.b - definitions[i].color.b;
                float distSq = dr * dr + dg * dg + db * db;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIdx    = i;
                }
            }

            return (bestIdx >= 0 && bestDistSq <= tolerance * tolerance)
                ? definitions[bestIdx]
                : null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (this.aimSpace == null) return;

            float radius = this.activeDispersionRadius * this.aimSpace.lossyScale.x;
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
