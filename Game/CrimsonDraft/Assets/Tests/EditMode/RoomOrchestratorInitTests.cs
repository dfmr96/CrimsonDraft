#nullable enable

using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using MessagePipe;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;
using CrimsonDraft.Navigation.Rooms;
using Unity.Cinemachine;
using UnityEngine.TestTools;

namespace CrimsonDraft.Tests
{
    public sealed class RoomOrchestratorInitTests
    {
        [Test]
        public void Initialize_withStartingRoom_activatesOnlyThatRoom()
        {
            var goA    = new GameObject("RoomA"); goA.SetActive(true);
            var roomA  = goA.AddComponent<RoomController>();
            var goB    = new GameObject("RoomB"); goB.SetActive(true);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(goA.activeSelf,  "starting room must be active");
                Assert.IsFalse(goB.activeSelf, "other rooms must be deactivated");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void Initialize_withNoStartingRoom_activatesFirstFound()
        {
            var goA = new GameObject("RoomA"); goA.SetActive(false);
            goA.AddComponent<RoomController>();
            var goB = new GameObject("RoomB"); goB.SetActive(false);
            goB.AddComponent<RoomController>();

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                int activeCount = (goA.activeSelf ? 1 : 0) + (goB.activeSelf ? 1 : 0);
                Assert.AreEqual(1, activeCount, "exactly one room must be active when no starting room set");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void Initialize_withPendingEntry_startsInRoomOfEntryDoorAtItsArrival()
        {
            var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();
            entryContext.SetPendingEntry("entry-1");

            var roomA = new GameObject("RoomA").AddComponent<RoomController>();
            var roomB = new GameObject("RoomB").AddComponent<RoomController>();
            var door  = MakeEntryDoor(roomB, "entry-1");
            var doorArrival = door.GetComponent<DoorArrival>();
            doorArrival.PlaceAtDefault(1f, Vector3.zero);
            doorArrival.ArrivalPoint.position = new Vector3(5, 1, 3);
            context.SetStartingRoom(roomA);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(roomB.gameObject.activeSelf,  "room of the entry door must be active");
                Assert.IsFalse(roomA.gameObject.activeSelf, "fallback starting room must not be active");
                Assert.AreEqual(new Vector3(5, 1, 3), player.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
                Object.DestroyImmediate(roomB.gameObject);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(entryContext);
            }
        }

        [Test]
        public void Initialize_withPendingEntryButNoDoor_logsErrorAndUsesStartingRoom()
        {
            var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();
            entryContext.SetPendingEntry("entry-1");

            var roomA = new GameObject("RoomA").AddComponent<RoomController>();
            context.SetStartingRoom(roomA);
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
                LogAssert.Expect(LogType.Error, new Regex("entry-1"));
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(roomA.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(entryContext);
            }
        }

        [Test]
        public void Initialize_withEntryDoorWithoutArrival_logsErrorAndUsesStartingRoom()
        {
            var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();
            entryContext.SetPendingEntry("entry-1");

            var roomA = new GameObject("RoomA").AddComponent<RoomController>();
            var roomB = new GameObject("RoomB").AddComponent<RoomController>();
            var door  = MakeEntryDoor(roomB, "entry-1");
            var arrival = new SerializedObject(door.GetComponent<DoorArrival>());
            arrival.FindProperty("arrivalPoint").objectReferenceValue = null;
            arrival.ApplyModifiedPropertiesWithoutUndo();
            context.SetStartingRoom(roomA);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
                LogAssert.Expect(LogType.Error, new Regex("DoorArrival"));
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(roomA.gameObject.activeSelf,  "falls back to the starting room");
                Assert.IsFalse(roomB.gameObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
                Object.DestroyImmediate(roomB.gameObject);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(entryContext);
            }
        }

        private static SceneDoorInteractable MakeEntryDoor(RoomController room, string entryPointId)
        {
            var go = new GameObject("EntryDoor");
            go.transform.SetParent(room.transform, false);
            var door = go.AddComponent<SceneDoorInteractable>();
            var so = new SerializedObject(door);
            so.FindProperty("entryPointId").stringValue = entryPointId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        [Test]
        public void Initialize_whenSceneEntryContextIsEmpty_usesRoomTransitionContextStartingRoom()
        {
            var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();

            var roomA = new GameObject("RoomA").AddComponent<RoomController>();
            var roomB = new GameObject("RoomB").AddComponent<RoomController>();
            context.SetStartingRoom(roomA);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(roomA.gameObject.activeSelf,  "RoomTransitionContext starting room must be used");
                Assert.IsFalse(roomB.gameObject.activeSelf, "other room must not be active");
            }
            finally
            {
                Object.DestroyImmediate(roomA.gameObject);
                Object.DestroyImmediate(roomB.gameObject);
                Object.DestroyImmediate(playerGo.gameObject);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(entryContext);
            }
        }

        [Test]
        public void ActivateRoomImmediate_activatesMatchingRoom_deactivatesOthers()
        {
            var goA   = new GameObject("RoomA");
            var roomA = goA.AddComponent<RoomController>();
            var soA   = new SerializedObject(roomA);
            soA.FindProperty("roomId").stringValue = "room-a";
            soA.ApplyModifiedPropertiesWithoutUndo();

            var goB   = new GameObject("RoomB");
            var roomB = goB.AddComponent<RoomController>();
            var soB   = new SerializedObject(roomB);
            soB.FindProperty("roomId").stringValue = "room-b";
            soB.ApplyModifiedPropertiesWithoutUndo();

            goA.SetActive(true);
            goB.SetActive(true);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                orchestrator.ActivateRoomImmediate("room-b");

                Assert.IsFalse(goA.activeSelf, "room-a must be deactivated");
                Assert.IsTrue(goB.activeSelf, "room-b must be activated");
                Assert.AreEqual(roomB, orchestrator.CurrentRoom);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void ActivateRoomImmediate_logsWarning_whenRoomIdNotFound()
        {
            var goA   = new GameObject("RoomA");
            var roomA = goA.AddComponent<RoomController>();
            goA.SetActive(true);

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(".*"));
                orchestrator.ActivateRoomImmediate("does-not-exist");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        // A loaded save puts the player wherever they stood, often outside every zone trigger, so
        // the shot they had can't be re-derived from position: the save names it explicitly.
        [Test]
        public void ActivateRoomImmediate_withCameraPath_activatesThatCamera()
        {
            var (goA, roomA) = MakeRoom("RoomA", "room-a");
            var (goB, _)     = MakeRoom("RoomB", "room-b");
            AddCamera(goB, "Cameras/CamA");
            var camB = AddCamera(goB, "Cameras/CamB");

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);
            var zones    = new FakeCameraZoneService();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, zones: zones);
                ((IInitializable)orchestrator).Initialize();

                orchestrator.ActivateRoomImmediate("room-b", "Cameras/CamB");

                Assert.AreSame(camB, zones.CurrentZoneCamera);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void ActivateRoomImmediate_withUnknownCameraPath_fallsBackToRoomCamera()
        {
            var (goA, roomA) = MakeRoom("RoomA", "room-a");
            var (goB, _)     = MakeRoom("RoomB", "room-b");
            var camA = AddCamera(goB, "Cameras/CamA");
            AddCamera(goB, "Cameras/CamB");

            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var context  = ScriptableObject.CreateInstance<RoomTransitionContext>();
            context.SetStartingRoom(roomA);
            var zones    = new FakeCameraZoneService();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, zones: zones);
                ((IInitializable)orchestrator).Initialize();

                orchestrator.ActivateRoomImmediate("room-b", "Cameras/Renamed");

                Assert.AreSame(camA, zones.CurrentZoneCamera);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void TeleportToRoom_activatesRoom_placesPlayerAtFirstArrival_andPublishes()
        {
            var context = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var (goA, roomA) = MakeRoom("RoomA", "Deck_T/A");
            var (goB, roomB) = MakeRoom("RoomB", "Deck_T/B");
            AddDoorWithArrival(roomB, new Vector3(4, 1, 2));
            AddDoorWithArrival(roomB, new Vector3(9, 1, 9));
            context.SetStartingRoom(roomA);
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();
            var ended    = new FakePublisher<RoomTransitionedEvent>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, ended: ended);
                ((IInitializable)orchestrator).Initialize();

                orchestrator.TeleportToRoom(roomB);

                Assert.IsTrue(roomB.gameObject.activeSelf);
                Assert.IsFalse(roomA.gameObject.activeSelf);
                Assert.AreSame(roomB, orchestrator.CurrentRoom);
                Assert.AreEqual(new Vector3(4, 1, 2), player.transform.position);
                Assert.AreEqual(1, ended.Count);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void TeleportToRoom_currentRoom_replacesPlayerWithoutErrors()
        {
            var context = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var (goA, roomA) = MakeRoom("RoomA", "Deck_T/A");
            AddDoorWithArrival(roomA, new Vector3(3, 1, 3));
            context.SetStartingRoom(roomA);
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();
                player.transform.position = new Vector3(50, 0, 50);

                orchestrator.TeleportToRoom(roomA); // any LogError would fail the test

                Assert.IsTrue(roomA.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(3, 1, 3), player.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void TeleportToRoom_roomWithoutArrival_usesRoomOrigin_andLogsError()
        {
            var context = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var (goA, roomA) = MakeRoom("RoomA", "Deck_T/A");
            var (goB, roomB) = MakeRoom("RoomB", "Deck_T/B");
            goB.transform.position = new Vector3(7, 0, 7);
            context.SetStartingRoom(roomA);
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context);
                ((IInitializable)orchestrator).Initialize();

                LogAssert.Expect(LogType.Error, new Regex("Deck_T/B"));
                orchestrator.TeleportToRoom(roomB);

                Assert.IsTrue(roomB.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(7, 0, 7), player.transform.position);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void Initialize_withPendingRoom_startsInThatRoomAtFirstArrival()
        {
            var context      = ScriptableObject.CreateInstance<RoomTransitionContext>();
            var entryContext = ScriptableObject.CreateInstance<SceneEntryContext>();
            entryContext.SetPendingRoom("Deck_T/B");
            var (goA, roomA) = MakeRoom("RoomA", "Deck_T/A");
            var (goB, roomB) = MakeRoom("RoomB", "Deck_T/B");
            AddDoorWithArrival(roomB, new Vector3(6, 1, 1));
            context.SetStartingRoom(roomA);
            var playerGo = new GameObject("Player");
            var player   = playerGo.AddComponent<PlayerController>();

            try
            {
                var orchestrator = MakeOrchestrator(player, context, entry: entryContext);
                ((IInitializable)orchestrator).Initialize();

                Assert.IsTrue(roomB.gameObject.activeSelf);
                Assert.IsFalse(roomA.gameObject.activeSelf);
                Assert.AreEqual(new Vector3(6, 1, 1), player.transform.position);
                Assert.IsNull(entryContext.ConsumeRoom(), "the pending room is consumed");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
                Object.DestroyImmediate(playerGo);
                Object.DestroyImmediate(context);
                Object.DestroyImmediate(entryContext);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static (GameObject, RoomController) MakeRoom(string name, string roomId)
        {
            var go   = new GameObject(name);
            var room = go.AddComponent<RoomController>();
            var so   = new SerializedObject(room);
            so.FindProperty("roomId").stringValue = roomId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return (go, room);
        }

        private static CinemachineCamera AddCamera(GameObject room, string path)
        {
            var parent = room.transform;
            var names  = path.Split('/');
            for (int i = 0; i < names.Length - 1; i++)
            {
                var child = parent.Find(names[i]);
                if (child == null)
                {
                    child = new GameObject(names[i]).transform;
                    child.SetParent(parent, false);
                }
                parent = child;
            }
            var go = new GameObject(names[names.Length - 1]);
            go.transform.SetParent(parent, false);
            return go.AddComponent<CinemachineCamera>();
        }

        private static RoomOrchestrator MakeOrchestrator(
            PlayerController       player,
            RoomTransitionContext  context,
            SceneEntryContext?     entry = null,
            FakeCameraZoneService? zones = null,
            FakePublisher<RoomTransitionedEvent>? ended = null)
            => new RoomOrchestrator(
                new FakeInputService(),
                player,
                context,
                entry ?? ScriptableObject.CreateInstance<SceneEntryContext>(),
                zones ?? new FakeCameraZoneService(),
                new FakePublisher<RoomTransitionStartedEvent>(),
                ended ?? new FakePublisher<RoomTransitionedEvent>());

        private static DoorArrival AddDoorWithArrival(RoomController room, Vector3 arrivalPosition)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(room.transform, false);
            var arrival = go.AddComponent<RoomDoorInteractable>().GetComponent<DoorArrival>();
            arrival.PlaceAtDefault(1f, Vector3.zero);
            arrival.ArrivalPoint.position = arrivalPosition;
            return arrival;
        }

        private sealed class FakeInputService : IInputService
        {
            public InputAction Move                   => null!;
            public InputAction Interact               => null!;
            public InputAction OpenInventory          => null!;
            public InputAction OpenMap                => null!;
            public InputAction Aim                    => null!;
            public InputAction AimFire                => null!;
            public InputAction Pause                  => null!;
            public InputAction Sprint                 => null!;
            public InputAction CombatNavigate         => null!;
            public InputAction CombatConfirm          => null!;
            public InputAction CombatCancel           => null!;
            public InputAction CombatUseItem          => null!;
            public InputAction UINavigate             => null!;
            public InputAction UIConfirm              => null!;
            public InputAction UICancel               => null!;
            public InputAction UIBack                 => null!;
            public InputAction DialogueAdvanceLine    => null!;
            public InputAction DialogueCancelDialogue => null!;
            public InputAction DoorTransitionSkip     => null!;
            public InputAction PickupNavigate         => null!;
            public InputAction PickupConfirm          => null!;
            public InputAction InventoryNavigate      => null!;
            public InputAction InventoryConfirm       => null!;
            public InputAction InventoryPickup        => null!;
            public InputAction InventoryCancel        => null!;
            public InputAction InventoryNextTab       => null!;
            public InputAction InventoryPrevTab       => null!;
            public InputAction InventoryCloseMap      => null!;
            public InputAction InventoryClose         => null!;
            public void SwitchToGameplay()       { }
            public void SwitchToCombat()         { }
            public void SwitchToUI()             { }
            public void SwitchToDialogue()       { }
            public void SwitchToDoorTransition()  { }
            public void SwitchToPickupPrompt()   { }
            public void SwitchToInventory()      { }
            public void Dispose()                { }
        }

        private sealed class FakePublisher<T> : IPublisher<T>
        {
            public int Count { get; private set; }
            public void Publish(T message) => Count++;
        }

    }
}
