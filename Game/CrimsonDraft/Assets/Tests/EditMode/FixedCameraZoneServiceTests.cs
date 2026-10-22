#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using CrimsonDraft.Navigation.CamaraSystem;

namespace CrimsonDraft.Tests
{
    // The save records the shot the player returns to, not a temporary inspection close-up
    // (telegraph, generator...), so the service has to tell the two apart.
    public sealed class FixedCameraZoneServiceTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in this.spawned)
                if (go != null) Object.DestroyImmediate(go);
            this.spawned.Clear();
        }

        [Test]
        public void ActivateZone_isCurrentAndGameplayCamera()
        {
            var service = new FixedCameraZoneService();
            var room    = Camera("Room");

            service.ActivateZone(room);

            Assert.AreSame(room, service.CurrentZoneCamera);
            Assert.AreSame(room, service.GameplayZoneCamera);
        }

        [Test]
        public void InspectionShot_isCurrent_butGameplayCameraStays()
        {
            var service   = new FixedCameraZoneService();
            var room      = Camera("Room");
            var telegraph = Camera("Telegraph");

            service.ActivateZone(room);
            service.ActivateInspectionShot(telegraph);

            Assert.AreSame(telegraph, service.CurrentZoneCamera);
            Assert.AreSame(room, service.GameplayZoneCamera);
            Assert.IsTrue(telegraph.enabled);
            Assert.IsFalse(room.enabled, "only one zone camera may be live at a time");
        }

        [Test]
        public void ActivateZone_afterInspection_isGameplayAgain()
        {
            var service   = new FixedCameraZoneService();
            var room      = Camera("Room");
            var telegraph = Camera("Telegraph");

            service.ActivateZone(room);
            service.ActivateInspectionShot(telegraph);
            service.ActivateZone(room);

            Assert.AreSame(room, service.CurrentZoneCamera);
            Assert.AreSame(room, service.GameplayZoneCamera);
            Assert.IsFalse(telegraph.enabled);
        }

        private CinemachineCamera Camera(string name)
        {
            var go = new GameObject(name);
            this.spawned.Add(go);
            return go.AddComponent<CinemachineCamera>();
        }
    }
}
