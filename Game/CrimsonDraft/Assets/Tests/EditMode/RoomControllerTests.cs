#nullable enable

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class RoomControllerTests
    {
        [Test]
        public void Activate_makesGameObjectActive()
        {
            var go   = new GameObject();
            go.SetActive(false);
            var room = go.AddComponent<RoomController>();

            room.Activate();

            Assert.IsTrue(go.activeSelf);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Deactivate_makesGameObjectInactive()
        {
            var go   = new GameObject();
            var room = go.AddComponent<RoomController>();

            room.Deactivate();

            Assert.IsFalse(go.activeSelf);
            Object.DestroyImmediate(go);
        }

    }
}
