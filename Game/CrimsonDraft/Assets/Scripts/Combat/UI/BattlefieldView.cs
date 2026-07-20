#nullable enable

using System;
using System.Collections.Generic;
using System.Collections;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using VContainer;
using CrimsonDraft.Operators;
using CrimsonDraft.Audio;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        private sealed class EnemyRuntimeState
        {
            public int CurrentHp;
            public int MaxHp;
            public bool IsDead;
            public int CurrentPoise;
            public int InitialPoise; // the roll this enemy resets to on a silent Poise reset
            public bool IsStaggered;
            public float StaggerEndsAt;
        }

        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;
        [SerializeField, Min(0.01f)] private float enemyDeathFadeDuration = 0.2f;
        [SerializeField] private Canvas? operatorDamageCanvas;
        [SerializeField] private GameObject? operatorDamageTextPrefab;
        [SerializeField] private Vector3 enemyTargetIndicatorOffset = new(0f, 0f, 0f);
        [SerializeField] private Vector3 operatorDamageOffset = new(0f, 0.9f, 0f);
        [SerializeField, Min(0.01f)] private float operatorDamageDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float enemyAttackShakeDuration = 0.2f;
        [SerializeField] private Vector3 enemyAttackShakeStrength = new(0.15f, 0.15f, 0f);

        private readonly List<GameObject> spawnedSprites = new();
        private readonly Dictionary<int, EnemyRuntimeState> enemyStateBySlot = new();
        private readonly Dictionary<int, GameObject> enemyGoBySlot = new();
        private readonly Dictionary<int, MeshRenderer> enemyRendererBySlot = new();
        private int[] occupiedEnemySlots = Array.Empty<int>();
        private EnemyData?[] currentEnemySlots = Array.Empty<EnemyData?>();
        private readonly Dictionary<int, Animator> operatorAnimatorBySlot = new();
        private static readonly int ShootHash = Animator.StringToHash("Shoot");
        private static readonly int AimHash = Animator.StringToHash("Aim");
        private readonly Dictionary<int, Animator> enemyAnimatorBySlot = new();
        private readonly Dictionary<int, bool> enemyHitToggleBySlot = new(); // false = Hit1 next, true = Hit2 next
        private static readonly int Hit1Hash = Animator.StringToHash("Hit1");
        private static readonly int Hit2Hash = Animator.StringToHash("Hit2");
        private static readonly int IsStaggeredHash = Animator.StringToHash("IsStaggered");
        private readonly IRandomSource poiseRandom = new UnityRandomSource();

        private IOperatorRoster? roster;

        [Inject]
        public void Construct(IOperatorRoster roster)
        {
            this.roster = roster;
        }

        private void Awake()
        {
            this.operatorIndicator.SetActive(false);
            this.enemyTargetIndicator.SetActive(false);
        }

        public void Populate(EncounterData encounter)
        {
            foreach (var go in this.spawnedSprites)
                Destroy(go);
            this.spawnedSprites.Clear();
            this.enemyStateBySlot.Clear();
            this.enemyGoBySlot.Clear();
            this.enemyRendererBySlot.Clear();
            this.operatorAnimatorBySlot.Clear();
            this.enemyAnimatorBySlot.Clear();
            this.enemyHitToggleBySlot.Clear();
            this.currentEnemySlots = encounter.EnemySlots;

            var occupied = new List<int>();
            for (int i = 0; i < encounter.EnemySlots.Length && i < this.enemySlotTransforms.Length; i++)
            {
                var enemy = encounter.EnemySlots[i];
                if (enemy == null) continue;

                occupied.Add(i);
                GameObject go;
                if (enemy.BattlefieldPrefab != null)
                {
                    go = Instantiate(enemy.BattlefieldPrefab, this.enemySlotTransforms[i], false);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(this.enemySlotTransforms[i], false);
                    go.GetComponent<MeshRenderer>().material.color = Color.red;
                }
                go.name = $"Enemy_{i}";
                var mr = go.GetComponentInChildren<MeshRenderer>();
                this.spawnedSprites.Add(go);
                this.enemyGoBySlot[i] = go;
                if (mr != null) this.enemyRendererBySlot[i] = mr;
                var enemyAnimator = go.GetComponentInChildren<Animator>();
                if (enemyAnimator != null) this.enemyAnimatorBySlot[i] = enemyAnimator;
                int rolledPoise = this.poiseRandom.NextInt(enemy.MinPoise, enemy.MaxPoise + 1);
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp     = Mathf.Max(1, enemy.MaxHp),
                    MaxHp         = Mathf.Max(1, enemy.MaxHp),
                    IsDead        = false,
                    CurrentPoise  = rolledPoise,
                    InitialPoise  = rolledPoise,
                    IsStaggered   = false,
                    StaggerEndsAt = 0f
                };
            }
            this.occupiedEnemySlots = occupied.ToArray();

            for (int i = 0; i < encounter.Operators.Length && i < this.playerSlotTransforms.Length; i++)
            {
                var op = encounter.Operators[i];
                if (op == null) continue;

                GameObject go;
                if (op.BattlefieldPrefab != null)
                {
                    go = Instantiate(op.BattlefieldPrefab, this.playerSlotTransforms[i], false);
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(this.playerSlotTransforms[i], false);
                    go.GetComponent<MeshRenderer>().material.color = Color.blue;
                }
                go.name = $"Operator_{i}";
                this.spawnedSprites.Add(go);

                var operatorAnimator = go.GetComponentInChildren<Animator>();
                if (operatorAnimator != null)
                    this.operatorAnimatorBySlot[i] = operatorAnimator;

                var operatorAudio = go.GetComponentInChildren<OperatorCombatAudio>();
                if (operatorAudio != null && this.roster != null)
                    operatorAudio.Bind(this.roster, i);
            }
        }

        public int[] GetOccupiedEnemySlots() => this.occupiedEnemySlots;

        public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.currentEnemySlots.Length)
                return null;

            return this.currentEnemySlots[slotIndex]?.HitMaskProfile;
        }

        public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int hpDamage, int poiseDamage)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return new EnemyDamageResult(slotIndex, 0, 0, false, false);

            if (state.IsDead)
                return new EnemyDamageResult(slotIndex, 0, 0, true, false);

            int appliedDamage = Mathf.Max(0, hpDamage);
            state.CurrentHp = Mathf.Max(0, state.CurrentHp - appliedDamage);
            bool isDead = state.CurrentHp <= 0;
            if (isDead)
            {
                state.IsDead = true;
                if (this.enemyGoBySlot.TryGetValue(slotIndex, out var go) && go != null)
                    StartCoroutine(this.FadeOutAndHideEnemy(go));

                var nextOccupied = new List<int>(this.occupiedEnemySlots.Length);
                foreach (int slot in this.occupiedEnemySlots)
                {
                    if (slot != slotIndex)
                        nextOccupied.Add(slot);
                }
                this.occupiedEnemySlots = nextOccupied.ToArray();

                return new EnemyDamageResult(slotIndex, appliedDamage, 0, true, false);
            }

            bool justStaggered = false;
            // Poise doesn't drain further while the enemy is already down — it only
            // matters again once it recovers (Update() below clears IsStaggered).
            if (!state.IsStaggered)
            {
                state.CurrentPoise -= Mathf.Max(0, poiseDamage);

                EnemyData? enemyData = slotIndex >= 0 && slotIndex < this.currentEnemySlots.Length
                    ? this.currentEnemySlots[slotIndex]
                    : null;

                if (enemyData != null && state.CurrentPoise <= 0)
                {
                    if (CombatMenuController.ShouldStagger(state.CurrentPoise, state.CurrentHp, state.MaxHp, enemyData.StaggerHpThresholdPct))
                    {
                        state.IsStaggered   = true;
                        state.StaggerEndsAt = Time.time + enemyData.StaggerDurationSec;
                        justStaggered       = true;
                        if (this.enemyAnimatorBySlot.TryGetValue(slotIndex, out var anim) && anim != null)
                            anim.SetBool(IsStaggeredHash, true);
                    }
                    else
                    {
                        state.CurrentPoise = state.InitialPoise; // silent reset — enemy too healthy to stagger yet
                    }
                }
            }

            return new EnemyDamageResult(slotIndex, appliedDamage, state.CurrentHp, false, justStaggered);
        }

        public bool IsEnemyStaggered(int slotIndex) =>
            this.enemyStateBySlot.TryGetValue(slotIndex, out var state) && state.IsStaggered;

        public bool HasAliveEnemies() => this.occupiedEnemySlots.Length > 0;

        private void Update()
        {
            float now = Time.time;
            foreach (var kvp in this.enemyStateBySlot)
            {
                var state = kvp.Value;
                if (!state.IsStaggered || now < state.StaggerEndsAt) continue;

                state.IsStaggered = false;
                if (this.enemyAnimatorBySlot.TryGetValue(kvp.Key, out var anim) && anim != null)
                    anim.SetBool(IsStaggeredHash, false);
            }
        }

        public async UniTask PlayOperatorShootBurstAsync(int operatorSlotIndex, int enemySlotIndex, ResolvedShot[] shots)
        {
            if (!this.operatorAnimatorBySlot.TryGetValue(operatorSlotIndex, out var animator) || animator == null)
                return;

            // The "Shoot" trigger only has an outgoing transition defined from "AimingIdlePistol"
            // (not the default "IdleUnarmed"), so Aim must be set and the transition into
            // AimingIdlePistol must actually complete before triggering Shoot has any effect.
            animator.SetBool(AimHash, true);
            while (!animator.GetCurrentAnimatorStateInfo(0).IsName("AimingIdlePistol"))
                await UniTask.NextFrame();

            int count = Mathf.Max(1, shots.Length);
            for (int i = 0; i < count; i++)
            {
                animator.SetTrigger(ShootHash);

                if (i < shots.Length && shots[i].Zone != ShotZone.Miss)
                    this.TriggerEnemyFlinch(enemySlotIndex);

                while (!animator.GetCurrentAnimatorStateInfo(0).IsName("ShootPistolFlexed2"))
                    await UniTask.NextFrame();

                var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                float duration = clipInfo.Length > 0 ? clipInfo[0].clip.length : 0f;
                if (duration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(duration));
            }

            animator.SetBool(AimHash, false);
        }

        private void TriggerEnemyFlinch(int enemySlotIndex)
        {
            if (enemySlotIndex < 0) return;
            if (!this.enemyAnimatorBySlot.TryGetValue(enemySlotIndex, out var animator) || animator == null) return;

            bool useHit2 = this.enemyHitToggleBySlot.TryGetValue(enemySlotIndex, out var toggle) && toggle;
            animator.SetTrigger(useHit2 ? Hit2Hash : Hit1Hash);
            this.enemyHitToggleBySlot[enemySlotIndex] = !useHit2;
        }

#if UNITY_EDITOR || DEBUG_COMBAT
        public (int Current, int Max, bool IsDead, int Poise, bool IsStaggered) GetEnemyHpDebug(int slotIndex)
        {
            if (this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return (state.CurrentHp, state.MaxHp, state.IsDead, state.CurrentPoise, state.IsStaggered);
            return (0, 0, true, 0, false);
        }
#endif

        private IEnumerator FadeOutAndHideEnemy(GameObject enemyGo)
        {
            if (enemyGo == null)
                yield break;

            var mr = enemyGo.GetComponent<MeshRenderer>();
            if (mr == null)
            {
                enemyGo.SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, this.enemyDeathFadeDuration);
            var startColor = mr.material.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - elapsed / duration);
                mr.material.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }

            enemyGo.SetActive(false);
        }

        public void SetOperatorIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.playerSlotTransforms.Length) return;
            this.operatorIndicator.SetActive(true);
            this.operatorIndicator.transform.position = this.playerSlotTransforms[slotIndex].position;
            var mr = this.operatorIndicator.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = Color.white;
        }

        public void DimOperatorIndicator()
        {
            var mr = this.operatorIndicator.GetComponent<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }

        public void PlayEnemyAttackFeedback(int enemySlotIndex)
        {
            if (!this.enemyGoBySlot.TryGetValue(enemySlotIndex, out var enemyGo) || enemyGo == null)
                return;

            enemyGo.transform.DOKill();
            enemyGo.transform.DOShakePosition(
                this.enemyAttackShakeDuration,
                this.enemyAttackShakeStrength,
                vibrato: 20,
                randomness: 90f,
                fadeOut: true);
        }

        public void ShowOperatorDamage(int operatorSlotIndex, int damage)
        {
            if (operatorSlotIndex < 0 || operatorSlotIndex >= this.playerSlotTransforms.Length)
                return;

            if (this.operatorDamageTextPrefab == null)
            {
                Debug.LogWarning("[BattlefieldView] operatorDamageTextPrefab is not assigned.");
                return;
            }

            Canvas? targetCanvas = this.operatorDamageCanvas != null
                ? this.operatorDamageCanvas
                : GetComponentInParent<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogWarning("[BattlefieldView] Missing Canvas for operator damage text.");
                return;
            }

            Transform anchor = this.playerSlotTransforms[operatorSlotIndex];
            var textGo = Instantiate(
                this.operatorDamageTextPrefab,
                targetCanvas.transform);
            var tmp = textGo.GetComponentInChildren<TMP_Text>();
            if (tmp == null)
            {
                Destroy(textGo);
                return;
            }

            PositionDamageTextOnCanvas(textGo.transform, targetCanvas, anchor.position + this.operatorDamageOffset);

            tmp.text = $"-{Mathf.Max(0, damage)}";
            tmp.alpha = 1f;

            Vector3 moveTarget = textGo.transform.position + (Vector3.up * 0.4f);
            textGo.transform.DOMove(moveTarget, this.operatorDamageDuration);
            tmp.DOFade(0f, this.operatorDamageDuration).OnComplete(() =>
            {
                if (textGo != null)
                    Destroy(textGo);
            });
        }

        private static void PositionDamageTextOnCanvas(Transform textTransform, Canvas canvas, Vector3 worldPosition)
        {
            if (textTransform is not RectTransform textRt)
            {
                textTransform.position = worldPosition;
                return;
            }

            if (canvas.transform is not RectTransform canvasRt)
            {
                textTransform.position = worldPosition;
                return;
            }

            Camera? eventCamera = null;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, worldPosition);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screenPoint, eventCamera, out Vector2 localPoint))
                textRt.anchoredPosition = localPoint;
        }

        public void SetEnemyTargetIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.enemySlotTransforms.Length) return;
            this.enemyTargetIndicator.SetActive(true);
            this.enemyTargetIndicator.transform.position = this.enemySlotTransforms[slotIndex].position + this.enemyTargetIndicatorOffset;
        }

        public void HideEnemyTargetIndicator()
        {
            this.enemyTargetIndicator.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (this.enemySlotTransforms == null) return;

            foreach (var kvp in this.enemyStateBySlot)
            {
                int slot = kvp.Key;
                var state = kvp.Value;
                if (slot < 0 || slot >= this.enemySlotTransforms.Length) continue;
                if (this.enemySlotTransforms[slot] == null) continue;

                float hpRatio = state.MaxHp > 0 ? (float)state.CurrentHp / state.MaxHp : 0f;
                var labelPos = this.enemySlotTransforms[slot].position + new Vector3(0f, 0.9f, 0f);

                UnityEditor.Handles.color = state.IsDead
                    ? Color.gray
                    : Color.Lerp(Color.red, Color.green, hpRatio);

                string text = state.IsDead
                    ? $"Enemy {slot} - DEAD"
                    : $"Enemy {slot} - HP {state.CurrentHp}/{state.MaxHp}";
                UnityEditor.Handles.Label(labelPos, text);
            }
        }
#endif
    }
}
