#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class OperatorCorpseSpawnerTests
    {
        [Test]
        public void Spawn_instantiatesPrefabAsChildOfRoom_atGivenTransform()
        {
            var prefabSource = new GameObject("DummyCorpseModel");
            var settings     = ScriptableObject.CreateInstance<OperatorCorpseSettings>();
            var so = new SerializedObject(settings);
            so.FindProperty("corpsePrefab").objectReferenceValue = prefabSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            var pos = new Vector3(1f, 2f, 3f);
            var rot = Quaternion.Euler(0f, 90f, 0f);

            try
            {
                var spawner = new OperatorCorpseSpawner(settings, new CorpseProximityTracker());
                spawner.Spawn(0, room, pos, rot);

                Assert.AreEqual(1, room.transform.childCount);
                var spawned = room.transform.GetChild(0);
                Assert.AreEqual(pos, spawned.position);
                Assert.Less(Quaternion.Angle(rot, spawned.rotation), 0.01f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(prefabSource);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    
        [Test]
        public void Spawn_initializesOperatorCorpseWithSlot()
        {
            var prefabSource = new GameObject("CorpseWithComponent", typeof(BoxCollider));
            prefabSource.AddComponent<OperatorCorpse>();
            var settings = ScriptableObject.CreateInstance<OperatorCorpseSettings>();
            var so = new SerializedObject(settings);
            so.FindProperty("corpsePrefab").objectReferenceValue = prefabSource;
            so.ApplyModifiedPropertiesWithoutUndo();
            var roomGo = new GameObject("Room");
            var room   = roomGo.AddComponent<RoomController>();

            try
            {
                new OperatorCorpseSpawner(settings, new CorpseProximityTracker()).Spawn(2, room, Vector3.zero, Quaternion.identity);
                Assert.AreEqual(2, room.transform.GetChild(0).GetComponent<OperatorCorpse>().Slot);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomGo);
                UnityEngine.Object.DestroyImmediate(prefabSource);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }
    }
}
