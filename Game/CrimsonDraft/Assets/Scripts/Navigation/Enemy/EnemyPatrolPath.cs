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

        public void ResetIndex() => index = 0;

        private void OnValidate()
        {
            if (waypoints.Length == 0)
                Debug.LogWarning($"{gameObject.name}: EnemyPatrolPath has no waypoints!", this);

            for (int i = 0; i < waypoints.Length; i++)
                if (waypoints[i] == null)
                    Debug.LogWarning($"{gameObject.name}: EnemyPatrolPath waypoint[{i}] is null!", this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!HasWaypoints) return;

            Gizmos.color = Color.cyan;
            foreach (var wp in waypoints)
            {
                if (wp == null) continue;
                Gizmos.DrawSphere(wp.position, 0.15f);
            }

            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            for (var i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] == null) continue;
                var next = waypoints[(i + 1) % waypoints.Length];
                if (next == null) continue;
                Gizmos.DrawLine(waypoints[i].position, next.position);
            }

            if (!Application.isPlaying) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(Current.position, 0.25f);
        }
#endif
    }
}
