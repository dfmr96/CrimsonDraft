#nullable enable

using System;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CrimsonDraft.Infrastructure.DebugMenu;
using CrimsonDraft.Infrastructure.Scenes;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.Rooms;

namespace CrimsonDraft.Tests
{
    public sealed class DebugRoomTeleportTests
    {
        [Test]
        public void IsSameScene_sameName()
            => Assert.IsTrue(DebugRoomTeleport.IsSameScene("Deck_B_Development", "Deck_B_Development"));

        [Test]
        public void IsSameScene_otherDeck()
            => Assert.IsFalse(DebugRoomTeleport.IsSameScene("Deck_B_Development", "Deck_C_Development"));

        [Test]
        public void Teleport_toOtherDeck_whileARoomTransitionRuns_isRefused()
        {
            var catalog = ScriptableObject.CreateInstance<RoomCatalog>();
            catalog.EditorSetRooms(new[] { new RoomCatalog.Entry("Deck_X/ROOM", "Deck_X_Development") });
            var floors   = new FakeFloors();
            var teleport = new DebugRoomTeleport(new DebugMenuRegistry(), catalog,
                new FakeRooms { IsTransitioning = true }, floors, new FakeScenes());

            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("Deck_X/ROOM"));
                teleport.Teleport("Deck_X/ROOM");

                Assert.AreEqual(0, floors.Calls, "a deck swap mid door transition strands the DoorTransition scene");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        private sealed class FakeRooms : IRoomOrchestrator
        {
            public bool            IsTransitioning { get; set; }
            public RoomController? CurrentRoom     => null;
            public UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition) => UniTask.CompletedTask;
            public void ActivateRoomImmediate(string roomId, string? cameraPath = null) { }
            public void TeleportToRoom(RoomController room) { }
        }

        private sealed class FakeFloors : IFloorTransitionService
        {
            public int Calls { get; private set; }
            public UniTask TransitionToFloorAsync(string fromScene, string toScene, string entryPointId, GameObject doorPrefab) => UniTask.CompletedTask;
            public UniTask TeleportToFloorAsync(string fromScene, string toScene, string roomId)
            {
                Calls++;
                return UniTask.CompletedTask;
            }
        }

        private sealed class FakeScenes : ISceneTransitionService
        {
            public bool IsInCombat => false;
            public UniTask StartCombatAsync(string encounterId, ScriptableObject? encounterAsset = null, bool operatorsStartFull = false, Action? onScreenCovered = null)
                => UniTask.CompletedTask;
        }
    }
}
