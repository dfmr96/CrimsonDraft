#nullable enable

using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        private sealed class EnemyRuntimeState
        {
            public int CurrentHp;
            public int MaxHp;
            public bool IsDead;
        }

        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;
        [SerializeField] private Material?   enemyDeathFadeMaterial;
        [SerializeField, Min(0.01f)] private float enemyDeathFadeDuration = 0.2f;
        [SerializeField] private Canvas? operatorDamageCanvas;
        [SerializeField] private GameObject? operatorDamageTextPrefab;
        [SerializeField] private Vector3 operatorDamageOffset = new(0f, 0.9f, 0f);
        [SerializeField, Min(0.01f)] private float operatorDamageDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float enemyAttackShakeDuration = 0.2f;
        [SerializeField] private Vector3 enemyAttackShakeStrength = new(0.15f, 0.15f, 0f);

        private readonly List<GameObject> spawnedSprites = new();
        private readonly Dictionary<int, EnemyRuntimeState> enemyStateBySlot = new();
        private readonly Dictionary<int, GameObject> enemyGoBySlot = new();
        private readonly Dictionary<int, SpriteRenderer> enemyRendererBySlot = new();
        private int[] occupiedEnemySlots = Array.Empty<int>();
        private EnemyData?[] currentEnemySlots = Array.Empty<EnemyData?>();
        private static readonly int FadePropId = Shader.PropertyToID("_Fade");

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
            this.currentEnemySlots = encounter.EnemySlots;

            var occupied = new List<int>();
            for (int i = 0; i < encounter.EnemySlots.Length && i < this.enemySlotTransforms.Length; i++)
            {
                var enemy = encounter.EnemySlots[i];
                if (enemy == null) continue;

                occupied.Add(i);
                var go = new GameObject($"Enemy_{i}");
                go.transform.SetParent(this.enemySlotTransforms[i], false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = enemy.Sprite;
                sr.sortingLayerName = "Combat";
                sr.sortingOrder = 0;
                if (this.enemyDeathFadeMaterial != null)
                {
                    var materialInstance = new Material(this.enemyDeathFadeMaterial);
                    if (materialInstance.HasProperty(FadePropId))
                        materialInstance.SetFloat(FadePropId, 0f);
                    sr.sharedMaterial = materialInstance;
                }
                this.spawnedSprites.Add(go);
                this.enemyGoBySlot[i] = go;
                this.enemyRendererBySlot[i] = sr;
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp = Mathf.Max(1, enemy.MaxHp),
                    MaxHp = Mathf.Max(1, enemy.MaxHp),
                    IsDead = false
                };
            }
            this.occupiedEnemySlots = occupied.ToArray();

            for (int i = 0; i < encounter.Operators.Length && i < this.playerSlotTransforms.Length; i++)
            {
                var op = encounter.Operators[i];
                if (op == null) continue;

                var go = new GameObject($"Operator_{i}");
                go.transform.SetParent(this.playerSlotTransforms[i], false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = op.Sprite;
                sr.sortingLayerName = "Combat";
                sr.sortingOrder = 0;
                this.spawnedSprites.Add(go);
            }
        }

        public int[] GetOccupiedEnemySlots() => this.occupiedEnemySlots;

        public AimHitMaskProfile? GetEnemyHitMaskProfile(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.currentEnemySlots.Length)
                return null;

            return this.currentEnemySlots[slotIndex]?.HitMaskProfile;
        }

        public EnemyDamageResult ApplyDamageToEnemy(int slotIndex, int damage)
        {
            if (!this.enemyStateBySlot.TryGetValue(slotIndex, out var state))
                return new EnemyDamageResult(slotIndex, 0, 0, false);

            if (state.IsDead)
                return new EnemyDamageResult(slotIndex, 0, 0, true);

            int appliedDamage = Mathf.Max(0, damage);
            state.CurrentHp = Mathf.Max(0, state.CurrentHp - appliedDamage);
            bool isDead = state.CurrentHp <= 0;
            if (!isDead)
                return new EnemyDamageResult(slotIndex, appliedDamage, state.CurrentHp, false);

            state.IsDead = true;
            if (this.enemyGoBySlot.TryGetValue(slotIndex, out var go) && go != null)
            {
                if (this.enemyRendererBySlot.TryGetValue(slotIndex, out var sr) && sr != null && sr.sharedMaterial != null &&
                    sr.sharedMaterial.HasProperty(FadePropId))
                    StartCoroutine(this.FadeOutAndHideEnemy(go, sr));
                else
                    go.SetActive(false);
            }

            var nextOccupied = new List<int>(this.occupiedEnemySlots.Length);
            foreach (int slot in this.occupiedEnemySlots)
            {
                if (slot != slotIndex)
                    nextOccupied.Add(slot);
            }
            this.occupiedEnemySlots = nextOccupied.ToArray();

            return new EnemyDamageResult(slotIndex, appliedDamage, 0, true);
        }

        public bool HasAliveEnemies() => this.occupiedEnemySlots.Length > 0;

        private IEnumerator FadeOutAndHideEnemy(GameObject enemyGo, SpriteRenderer sr)
        {
            if (enemyGo == null || sr == null || sr.sharedMaterial == null)
            {
                if (enemyGo != null) enemyGo.SetActive(false);
                yield break;
            }

            var mat = sr.sharedMaterial;
            if (!mat.HasProperty(FadePropId))
            {
                enemyGo.SetActive(false);
                yield break;
            }

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, this.enemyDeathFadeDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                mat.SetFloat(FadePropId, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            mat.SetFloat(FadePropId, 1f);
            enemyGo.SetActive(false);
        }

        public void SetOperatorIndicator(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= this.playerSlotTransforms.Length) return;
            this.operatorIndicator.SetActive(true);
            this.operatorIndicator.transform.position = this.playerSlotTransforms[slotIndex].position;
            var sr = this.operatorIndicator.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = Color.white;
        }

        public void DimOperatorIndicator()
        {
            var sr = this.operatorIndicator.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = new Color(0.4f, 0.4f, 0.4f, 1f);
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
            this.enemyTargetIndicator.transform.position = this.enemySlotTransforms[slotIndex].position;
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
