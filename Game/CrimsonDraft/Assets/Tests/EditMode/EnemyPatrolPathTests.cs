#nullable enable

using NUnit.Framework;
using UnityEngine;
using CrimsonDraft.Navigation.Enemy;

namespace CrimsonDraft.Tests
{
    public sealed class EnemyPatrolPathTests
    {
        private static EnemyPatrolPath BuildPathWithWaypoints(int count)
        {
            var go   = new GameObject();
            var path = go.AddComponent<EnemyPatrolPath>();

            var waypoints = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var wpGo = new GameObject($"Waypoint{i}");
                wpGo.transform.position = new Vector3(i, 0f, 0f);
                waypoints[i] = wpGo.transform;
            }

            var so = new UnityEditor.SerializedObject(path);
            var arrayProp = so.FindProperty("waypoints");
            arrayProp.arraySize = count;
            for (int i = 0; i < count; i++)
                arrayProp.GetArrayElementAtIndex(i).objectReferenceValue = waypoints[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return path;
        }

        [Test]
        public void ResetIndex_afterAdvancing_returnsCurrentToFirstWaypoint()
        {
            var path = BuildPathWithWaypoints(3);
            path.Advance();
            path.Advance();
            Assert.AreEqual(2f, path.Current.position.x); // sanity: advanced to waypoint 2

            path.ResetIndex();

            Assert.AreEqual(0f, path.Current.position.x);

            Object.DestroyImmediate(path.gameObject);
        }
    }
}
