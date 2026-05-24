#nullable enable

using System;
using UnityEngine;

namespace CrimsonDraft.Navigation.Enemy
{
    public sealed class EnemyPatrolPath : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();

        private int index = 0;

        public bool HasWaypoints => waypoints.Length > 0;
        public Transform Current  => waypoints[index];

        public void Advance()
        {
            if (waypoints.Length == 0) return;
            index = (index + 1) % waypoints.Length;
        }

        private void OnValidate()
        {
            if (waypoints.Length == 0)
                Debug.LogWarning($"{gameObject.name}: EnemyPatrolPath has no waypoints!", this);

            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i] == null)
                    Debug.LogWarning($"{gameObject.name}: EnemyPatrolPath waypoint[{i}] is null!", this);
        }
    }
}
