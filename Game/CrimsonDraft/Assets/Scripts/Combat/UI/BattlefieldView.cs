#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Combat
{
    public sealed class BattlefieldView : MonoBehaviour, IBattlefieldView
    {
        [SerializeField] private Transform[] enemySlotTransforms  = Array.Empty<Transform>();
        [SerializeField] private Transform[] playerSlotTransforms = Array.Empty<Transform>();
        [SerializeField] private GameObject  operatorIndicator    = null!;
        [SerializeField] private GameObject  enemyTargetIndicator = null!;

        private readonly List<GameObject> spawnedSprites = new();
        private int[]       occupiedEnemySlots = Array.Empty<int>();
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
