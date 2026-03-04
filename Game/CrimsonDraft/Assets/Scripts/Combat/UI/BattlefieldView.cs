#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        private sealed class EnemyRuntimeState
        {
            public int CurrentHp;
            public bool IsDead;
        }

        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;

        private readonly List<GameObject> spawnedSprites = new();
        private readonly Dictionary<int, EnemyRuntimeState> enemyStateBySlot = new();
        private readonly Dictionary<int, GameObject> enemyGoBySlot = new();
        private int[] occupiedEnemySlots = Array.Empty<int>();
        private EnemyData?[] currentEnemySlots = Array.Empty<EnemyData?>();

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
                this.spawnedSprites.Add(go);
                this.enemyGoBySlot[i] = go;
                this.enemyStateBySlot[i] = new EnemyRuntimeState
                {
                    CurrentHp = Mathf.Max(1, enemy.MaxHp),
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
                go.SetActive(false);

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
    }
}
