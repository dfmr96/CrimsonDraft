#nullable enable

using System;
using UnityEngine;
using NaughtyAttributes;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomController : MonoBehaviour
    {
        [Serializable]
        private sealed class EnemySpawnEntry
        {
            public EnemyNavAgent enemy = null!;
            public Transform     spawnPoint = null!;
        }

        [SerializeField] private string roomId = "";
        [SerializeField] private EnemySpawnEntry[] enemySpawns = Array.Empty<EnemySpawnEntry>();

        public string RoomId => this.roomId;

        public void Activate()
        {
            gameObject.SetActive(true);

            for (int i = 0; i < this.enemySpawns.Length; i++)
            {
                var entry = this.enemySpawns[i];
                if (entry?.enemy == null || entry.spawnPoint == null) continue;
                if (!entry.enemy.gameObject.activeSelf) continue; // defeated or otherwise hidden
                entry.enemy.ResetToSpawn(entry.spawnPoint.position, entry.spawnPoint.rotation);
            }
        }

        public void Deactivate() => gameObject.SetActive(false);

#if UNITY_EDITOR
        [Button("Cache Room Enemies")]
        private void CacheRoomEnemies()
        {
            for (int i = 0; i < this.enemySpawns.Length; i++)
            {
                if (this.enemySpawns[i]?.spawnPoint != null)
                    DestroyImmediate(this.enemySpawns[i].spawnPoint.gameObject);
            }

            var enemies = GetComponentsInChildren<EnemyNavAgent>(includeInactive: true);
            this.enemySpawns = new EnemySpawnEntry[enemies.Length];

            for (int i = 0; i < enemies.Length; i++)
            {
                var spawnGo = new GameObject($"EnemySpawn_{enemies[i].name}");
                spawnGo.transform.SetParent(transform);
                spawnGo.transform.SetPositionAndRotation(
                    enemies[i].transform.position, enemies[i].transform.rotation);

                this.enemySpawns[i] = new EnemySpawnEntry
                {
                    enemy      = enemies[i],
                    spawnPoint = spawnGo.transform,
                };
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
