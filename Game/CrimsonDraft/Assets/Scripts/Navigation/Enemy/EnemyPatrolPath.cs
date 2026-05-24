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

        public void Advance() => index = (index + 1) % waypoints.Length;
    }
}
