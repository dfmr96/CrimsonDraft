#nullable enable

using System;
using System.Collections.Generic;
using CrimsonDraft.Inventory;
using CrimsonDraft.Operators;
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
        // Child of silhouetteImage with the same rect -- draws the active overlay (vest) on top
        // of the silhouette so it shakes with it. Optional: without it overlays still apply,
        // they just aren't drawn.
        [SerializeField] private Image?        overlayImage;
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
        [SerializeField] private Color         armorBlockedFeedbackColor = new Color(0.2f, 0.88f, 1f, 1f); // #33E0FF

        // Each full "there and back" loop of a bar nudges its speed up by this much (1 + stage *
        // speedRampStep), capped at maxSpeedRampStages loops -- e.g. default 0.1/4 ramps
        // 1.0x -> 1.1x -> 1.2x -> 1.3x -> 1.4x, never a flat x2/x3/x4 jump.
        [Header("Loop Speed Ramp")]
        [SerializeField] private float speedRampStep      = 0.1f;
        [SerializeField] private int   maxSpeedRampStages = 4;

        // The whole panel pulses like a heartbeat instead of jittering with continuous noise --
        // a "lub" thump followed by a weaker "dub" a moment later, then quiet until the next
        // beat. There's always a faint pulse even at full HP; both how hard it hits
        // (minPulseAmplitude -> maxPulseAmplitude) and how often it beats
        // (calmBeatInterval -> panicBeatInterval) ramp up as HP drops, like a racing heart.
        [Header("HP Heartbeat Shake")]
        [SerializeField] private float calmBeatInterval    = 1.0f;  // seconds/beat at full HP (~60 BPM)
        [SerializeField] private float panicBeatInterval   = 0.45f; // seconds/beat at 0 HP (~130 BPM)
        [SerializeField] private float minPulseAmplitude   = 1.5f;  // px, always present
        [SerializeField] private float maxPulseAmplitude   = 6f;    // px, at 0 HP
        [SerializeField] private float pulseDecay          = 14f;   // higher = thump fades faster
        [SerializeField, Range(0f, 1f)] private float dubStrength       = 0.55f; // "dub" vs "lub" strength
        [SerializeField, Range(0f, 1f)] private float dubOffsetFraction = 0.16f; // "dub" timing within the beat

        // Randomizes where each selector's first leg starts along its rail instead of always the
        // near edge, scaling with HP. Null config keeps the original fixed-start behavior.
        [Header("Spawn Variability")]
        [SerializeField] private AimSpawnConfig? spawnConfig;

        // Shared Full/Yellow/Orange/Danger breakpoints -- also drives AimDebuffController so both
        // systems agree on what counts as "low HP" instead of each having its own thresholds.
        [Header("HP Tier")]
        [SerializeField] private HpTierConfig? hpTierConfig;
        [SerializeField] private AimDebuffController? debuffController;

        private IRandomSource random = new UnityRandomSource();
        private float? previousVerticalOffset;
        private float? previousHorizontalOffset;
        private float  spawnDanger01; // 0..1, stepped by HpTier -- see SetOperatorHpRatio

        private AimPhase phase;
        private Vector2  confirmedLocalPos;
        private int               shotCount              = 1;
        private int               activeDispersionRadius = 10;
        private int               activeBaseDamage       = CombatMenuController.BaseDamage;
        private Sprite?           activeDispersionSprite;
        private BurstPatternData? activeBurstPattern;
        private int               activePelletCount = 1;
        private IShotResolutionStrategy activeShotStrategy = new SingleShotStrategy();
        private ResolvedShot[] pendingResolvedShots = Array.Empty<ResolvedShot>();
        private bool isResolvingSequence;

        private Sprite?              activeZoneMaskSprite;
        private Sprite?              activeOverlayMaskSprite;
        private OverlayKind?         activeOverlayKind;
        private float                activeArmorDamageMultiplier = 1f;
        private bool                 warnedUnreadableOverlayMask;
        private ShotZoneDefinition[] activeZoneDefinitions = Array.Empty<ShotZoneDefinition>();
        private float                activeColorTolerance  = 0.1f;
        private bool                 warnedMissingMaskConfig;
        private readonly List<GameObject> activeFeedback = new List<GameObject>();

        private Tween? verticalTween;
        private Tween? horizontalTween;
        private int    verticalLoopLegs;
        private int    verticalRampStage;
        private int    horizontalLoopLegs;
        private int    horizontalRampStage;
        private float  shakeIntensity01; // 0 = full HP, 1 = 0 HP
        private float  shakeSeedV; // per-QTE seed for the heartbeat's per-beat punch direction
        private Vector3 basePanelLocalPos;
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

            // The visible (black & white) silhouette was previously a single static sprite set
            // only in the Inspector — SilhouetteSprite is the per-profile equivalent, separate
            // from ZoneMaskSprite (the color-coded sprite sampled for zone/hit detection, never
            // itself shown). Only overridden when the profile actually configured one, so
            // profiles that haven't been updated yet keep the old static sprite.
            if (this.silhouetteImage != null && profile.SilhouetteSprite != null)
                this.silhouetteImage.sprite = profile.SilhouetteSprite;
        }

        public void ConfigureOverlay(ActiveOverlay? overlay)
        {
            this.activeOverlayMaskSprite     = overlay?.MaskSprite;
            this.activeOverlayKind           = overlay?.Kind;
            this.warnedUnreadableOverlayMask = false;

            if (this.overlayImage == null) return;
            this.overlayImage.sprite  = overlay?.VisibleSprite;
            this.overlayImage.enabled = overlay.HasValue;
        }

        public void ConfigureWeapon(WeaponData? weaponData)
        {
            this.activeArmorDamageMultiplier = weaponData?.ArmorDamageMultiplier ?? 1f;
            this.activeDispersionRadius = weaponData?.DispersionRadius ?? 10;
            this.activeBaseDamage       = weaponData?.Damage ?? CombatMenuController.BaseDamage;
            this.activeDispersionSprite = weaponData?.DispersionCircleSprite;
            this.activeBurstPattern     = weaponData?.BurstPattern;
            this.activePelletCount      = Mathf.Max(1, weaponData?.PelletCount ?? 1);
            this.activeShotStrategy     = weaponData?.GunType is GunType.Shotgun or GunType.REShotgun
                ? new PelletSpreadStrategy()
                : new SingleShotStrategy();
        }

        public void ConfigureMeleeWeapon(MeleeWeaponData? meleeData)
        {
            this.activeArmorDamageMultiplier = meleeData?.ArmorDamageMultiplier ?? 1f;
            this.activeBaseDamage       = meleeData?.Damage ?? CombatMenuController.BaseDamage;
            this.activeDispersionSprite = null;
            this.activeBurstPattern     = meleeData?.SlashPattern;
            this.activePelletCount      = Mathf.Max(2, meleeData?.SlashPointCount ?? 5);
            float slashLength           = meleeData?.SlashLength ?? 40f;
            this.activeDispersionRadius = Mathf.Max(1, Mathf.RoundToInt(slashLength * 0.5f));
            this.activeShotStrategy     = new SlashStrategy(
                slashLength,
                meleeData?.SlashAngleDegrees ?? 45f,
                meleeData?.SlashAngleJitterDegrees ?? 15f);
        }

        public void SetShotCount(int shotCount)
        {
            this.shotCount = Mathf.Max(1, shotCount);
        }

        public void SetOperatorHpRatio(float hpRatio)
        {
            // Heartbeat shake stays continuous with raw HP% -- untouched by the tier system below.
            this.shakeIntensity01 = 1f - Mathf.Clamp01(hpRatio);

            HpTier tier = this.hpTierConfig != null
                ? HpTierCalculator.ComputeTier(hpRatio, this.hpTierConfig)
                : HpTier.Full;

            // Spawn radius now steps with the same tier the debuffs use, instead of following raw
            // HP% -- falls back to the old continuous behavior if no tier config is assigned.
            this.spawnDanger01 = this.hpTierConfig != null
                ? HpTierCalculator.TierToDanger01(tier)
                : this.shakeIntensity01;

            this.debuffController?.SetTier(tier);
        }

        public void Show()
        {
            this.gameObject.SetActive(true);
            this.basePanelLocalPos = this.transform.localPosition;
            this.shakeSeedV = UnityEngine.Random.Range(0f, 1000f);
            this.StartVerticalOscillation();
            this.phase = AimPhase.VerticalAiming;
            this.pendingResolvedShots = Array.Empty<ResolvedShot>();
            this.isResolvingSequence = false;
            this.debuffController?.BeginForShot();
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

                // Both axes are locked in -- the debuffs have done their job, let them fade out
                // instead of holding through the resolve/dismiss phases.
                this.debuffController?.EndForShot();
                return;
            }

            if (this.phase == AimPhase.WaitingResolve && !this.isResolvingSequence)
                this.ResolvePendingShotsAsync().Forget();
        }

        public void Hide()
        {
            // Guaranteed shutdown: whatever debuff effects are mid-fade get force-stopped here,
            // regardless of why the aim view is closing (normal dismiss, cancel, or death).
            this.debuffController?.EndAll();

            this.verticalSelector.DOKill();
            this.verticalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.verticalTween   = null;
            this.horizontalTween = null;
            this.transform.localPosition = this.basePanelLocalPos; // undo any in-progress shake before Show() recaptures it next time
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

            float startY = this.spawnConfig != null
                ? AimSpawnPlanner.ComputeSignedOffset(halfH, this.spawnDanger01, this.previousVerticalOffset, this.spawnConfig, this.random)
                : -halfH;
            this.previousVerticalOffset = startY;
            this.verticalSelector.rectTransform.localPosition = new Vector3(0f, startY, 0f);

            this.verticalLoopLegs  = 0;
            this.verticalRampStage = 0;

            // First leg heads to the top at the same px/sec rate as every normal leg (full range
            // 2*halfH covered in `speed` seconds) -- so a spawn closer to the top just takes
            // proportionally less time, exactly as if the oscillation had been running the whole
            // time and we started watching it partway through.
            float firstLegDuration = this.ComputeFirstLegDuration(halfH, startY);
            this.verticalTween = this.verticalSelector.rectTransform
                .DOLocalMoveY(halfH, firstLegDuration, snapping: true)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => this.BeginVerticalSteadyLoop(halfH));
        }

        private void BeginVerticalSteadyLoop(float halfH)
        {
            this.verticalLoopLegs = 1; // the randomized first leg (spawn -> top) already counts as leg 1
            this.verticalTween = this.verticalSelector.rectTransform
                .DOLocalMoveY(-halfH, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .OnStepComplete(HandleVerticalLoopLeg);
        }

        private void StartHorizontalOscillation()
        {
            float halfW = Mathf.Floor(this.horizontalSpace.rect.width / 2f);
            this.horizontalSelector.DOKill();
            this.horizontalSelector.rectTransform.DOKill();
            this.horizontalSelector.DOFade(1f, 0f);

            float startX = this.spawnConfig != null
                ? AimSpawnPlanner.ComputeSignedOffset(halfW, this.spawnDanger01, this.previousHorizontalOffset, this.spawnConfig, this.random)
                : -halfW;
            this.previousHorizontalOffset = startX;
            this.horizontalSelector.rectTransform.localPosition = new Vector3(startX, 0f, 0f);

            this.horizontalLoopLegs  = 0;
            this.horizontalRampStage = 0;

            // Same rate-matched first leg as the vertical bar -- see StartVerticalOscillation.
            float firstLegDuration = this.ComputeFirstLegDuration(halfW, startX);
            this.horizontalTween = this.horizontalSelector.rectTransform
                .DOLocalMoveX(halfW, firstLegDuration, snapping: true)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => this.BeginHorizontalSteadyLoop(halfW));
        }

        // Same px/sec rate as a normal full-range leg (2*halfExtent covered in `speed` seconds),
        // applied to the shorter/longer distance from a randomized spawn point to the extreme.
        private float ComputeFirstLegDuration(float halfExtent, float startOffset)
        {
            float fullRange = 2f * halfExtent;
            if (fullRange <= 0f)
                return this.speed;

            float distanceToExtreme = halfExtent - startOffset;
            return this.speed * (distanceToExtreme / fullRange);
        }

        private void BeginHorizontalSteadyLoop(float halfW)
        {
            this.horizontalLoopLegs = 1; // the randomized first leg (spawn -> right) already counts as leg 1
            this.horizontalTween = this.horizontalSelector.rectTransform
                .DOLocalMoveX(-halfW, this.speed, snapping: true)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .OnStepComplete(HandleHorizontalLoopLeg);
        }

        // OnStepComplete fires once per Yoyo leg (there, then back) -- a full "start, reach the
        // end, and return to start" loop is 2 legs, so only every 2nd firing counts as one lap
        // and nudges timeScale up a notch, capped at maxSpeedRampStages.
        private void HandleVerticalLoopLeg()
        {
            this.verticalLoopLegs++;
            if (this.verticalLoopLegs % 2 != 0) return;
            if (this.verticalRampStage >= this.maxSpeedRampStages) return;
            this.verticalRampStage++;
            if (this.verticalTween != null)
                this.verticalTween.timeScale = 1f + this.verticalRampStage * this.speedRampStep;
        }

        private void HandleHorizontalLoopLeg()
        {
            this.horizontalLoopLegs++;
            if (this.horizontalLoopLegs % 2 != 0) return;
            if (this.horizontalRampStage >= this.maxSpeedRampStages) return;
            this.horizontalRampStage++;
            if (this.horizontalTween != null)
                this.horizontalTween.timeScale = 1f + this.horizontalRampStage * this.speedRampStep;
        }

        // HP-heartbeat: pulses the whole QTE panel (bars, silhouette, everything --
        // AimViewController sits on the panel's own root RectTransform) on a "lub-dub" rhythm
        // instead of continuous noise, so it reads as a heartbeat rather than a twitch. Offset is
        // added on top of the panel's own configured resting position, captured once in Show(),
        // so it never drifts and always resolves back to the same anchor between beats.
        private void Update()
        {
            var rt = (RectTransform)this.transform;
            bool activePhase = this.phase == AimPhase.VerticalAiming || this.phase == AimPhase.HorizontalAiming;

            if (!activePhase)
            {
                rt.localPosition = this.basePanelLocalPos;
                return;
            }

            float beatInterval = Mathf.Max(0.05f, Mathf.Lerp(this.calmBeatInterval, this.panicBeatInterval, this.shakeIntensity01));
            int   beatIndex    = Mathf.FloorToInt(Time.time / beatInterval);
            float cycleT       = Time.time - beatIndex * beatInterval;

            float lub = PulseEnvelope(cycleT, this.pulseDecay);
            float dub = PulseEnvelope(cycleT - beatInterval * this.dubOffsetFraction, this.pulseDecay) * this.dubStrength;
            float envelope = Mathf.Max(lub, dub);

            // A new pseudo-random punch direction each beat (stable for the beat's whole
            // duration) keeps consecutive thumps from looking identical without ever being
            // incoherent noise mid-beat.
            float angle = Mathf.PerlinNoise(beatIndex * 0.37f + this.shakeSeedV, 0.5f) * Mathf.PI * 2f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            float amplitude = Mathf.Lerp(this.minPulseAmplitude, this.maxPulseAmplitude, this.shakeIntensity01);
            var offset = new Vector3(dir.x, dir.y, 0f) * (envelope * amplitude);

            rt.localPosition = this.basePanelLocalPos + offset;
        }

        private static float PulseEnvelope(float t, float decay) => t < 0f ? 0f : Mathf.Exp(-decay * t);

        private ResolvedShot[] BuildResolvedShots(Vector2 firstShotLocal, int count)
        {
            int clampedCount = Mathf.Max(1, count);
            var resolved     = new List<ResolvedShot>(clampedCount);
            int flatIndex    = 0;

            for (int bulletIndex = 0; bulletIndex < clampedCount; bulletIndex++)
            {
                Vector2[] pelletPositions = this.activeShotStrategy.GetPelletLocalPositions(
                    this.confirmedLocalPos, firstShotLocal, bulletIndex, this.perBulletYOffset, this.activeBurstPattern, this.activePelletCount);

                foreach (var shotLocal in pelletPositions)
                {
                    Vector2             normalized = this.NormalizeShotLocal(shotLocal);
                    ShotZoneDefinition? def        = this.SampleSilhouette(shotLocal);
                    ShotZone            zone       = def?.zone ?? ShotZone.Miss;
                    ShotPrecision       precision  = def?.precisionEntry.precision ?? ShotPrecision.Normal;
                    float               precMult   = def.HasValue ? (def.Value.precisionEntry.multiplier <= 0f ? 1f : def.Value.precisionEntry.multiplier) : 0f;
                    bool                armorBlocked = ArmorRules.IsBlocked(
                        zone, this.IsOverlayCovered(shotLocal), this.activeOverlayKind, this.activeArmorDamageMultiplier);
                    int                 damage     = CombatMenuController.ComputeShotDamage(
                        zone, precMult, this.activeBaseDamage, armorBlocked ? this.activeArmorDamageMultiplier : 1f);
                    resolved.Add(new ResolvedShot(flatIndex, bulletIndex, normalized, zone, precision, damage, armorBlocked));
                    flatIndex++;
                }
            }
            return resolved.ToArray();
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
                this.SpawnShotFeedbackVisual(shot.NormalizedPos, shot.Damage, shot.Zone == ShotZone.Miss, shot.ArmorBlocked);

                // Only pause between distinct bullets, not between pellets of the same shell -
                // a shotgun blast should read as one simultaneous spread, not a slow trickle.
                bool isLastShot     = i == this.pendingResolvedShots.Length - 1;
                bool advancesBullet = !isLastShot && this.pendingResolvedShots[i + 1].BulletIndex != shot.BulletIndex;
                if (advancesBullet && this.bulletSequenceDelay > 0f)
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

        private void SpawnShotFeedbackVisual(Vector2 normalizedPos, int damage, bool isMiss, bool armorBlocked = false)
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
            var baseColor = isMiss ? this.missFeedbackColor
                : armorBlocked ? this.armorBlockedFeedbackColor
                : this.hitFeedbackColor;
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

        // Number of distinct bullets fired represented in a resolved-shots array - 1 for a
        // normal weapon (1 pellet per bullet), or fewer than shots.Length for a shotgun where
        // several entries share the same BulletIndex. Relies on BulletIndex being
        // non-decreasing across the array, guaranteed by BuildResolvedShots's construction order.
        internal static int CountBullets(ResolvedShot[] shots) =>
            shots.Length == 0 ? 1 : shots[shots.Length - 1].BulletIndex + 1;

        // Shot position (aimSpace local) -> normalized UV inside the silhouette's rect. Shared by
        // the zone mask and the overlay mask, which are both laid out in that same rect.
        private bool TryGetSilhouetteUv(Vector2 shotLocal, out float u, out float v)
        {
            u = 0f;
            v = 0f;
            if (this.silhouetteImage == null) return false;

            var worldPos   = this.aimSpace.TransformPoint(new Vector3(shotLocal.x, shotLocal.y, 0f));
            var silRt      = this.silhouetteImage.rectTransform;
            var localInSil = silRt.InverseTransformPoint(worldPos);
            var rect       = silRt.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;

            u = Mathf.Clamp01((localInSil.x - rect.xMin) / rect.width);
            v = Mathf.Clamp01((localInSil.y - rect.yMin) / rect.height);
            return true;
        }

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

            if (!this.TryGetSilhouetteUv(shotLocal, out float u, out float v))
                return null;
            var silRt = this.silhouetteImage.rectTransform;
            var rect  = silRt.rect;

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

        private bool IsOverlayCovered(Vector2 shotLocal)
        {
            Sprite? mask = this.activeOverlayMaskSprite;
            if (mask == null) return false;

            if (!mask.texture.isReadable)
            {
                if (!this.warnedUnreadableOverlayMask)
                {
                    Debug.LogWarning($"[AimView] Overlay mask '{mask.name}' is not Read/Write enabled -- every shot is treated as uncovered.");
                    this.warnedUnreadableOverlayMask = true;
                }
                return false;
            }

            return this.TryGetSilhouetteUv(shotLocal, out float u, out float v)
                && OverlayCoverage.IsCovered(mask, u, v);
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
