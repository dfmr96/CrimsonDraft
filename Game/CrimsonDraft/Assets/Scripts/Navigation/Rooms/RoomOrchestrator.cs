#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using VContainer.Unity;
using CrimsonDraft.Infrastructure.Input;
using CrimsonDraft.Navigation.CamaraSystem;
using CrimsonDraft.Navigation.Interactables;
using CrimsonDraft.Navigation.Player;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class RoomOrchestrator : IRoomOrchestrator, IInitializable
    {
        private const string TransitionSceneName = "DoorTransition";

        private readonly IInputService                          inputService;
        private readonly PlayerController                       player;
        private readonly RoomTransitionContext                  context;
        private readonly SceneEntryContext                      sceneEntryContext;
        private readonly IFixedCameraZoneService                zoneService;
        private readonly IPublisher<RoomTransitionStartedEvent> startedPublisher;
        private readonly IPublisher<RoomTransitionedEvent>      endedPublisher;

        private RoomController? currentRoom;
        private bool            isTransitioning;

        [Preserve]
        public RoomOrchestrator(
            IInputService                          inputService,
            PlayerController                       player,
            RoomTransitionContext                  context,
            SceneEntryContext                      sceneEntryContext,
            IFixedCameraZoneService                zoneService,
            IPublisher<RoomTransitionStartedEvent> startedPublisher,
            IPublisher<RoomTransitionedEvent>      endedPublisher)
        {
            this.inputService      = inputService;
            this.player            = player;
            this.context           = context;
            this.sceneEntryContext  = sceneEntryContext;
            this.zoneService       = zoneService;
            this.startedPublisher  = startedPublisher;
            this.endedPublisher    = endedPublisher;
        }

        void IInitializable.Initialize()
        {
            // Entering a Deck scene doesn't imply the Gameplay map is already active --
            // e.g. coming from the main menu leaves the UI map enabled for its own
            // navigation, and nothing else switches back on a fresh scene load.
            this.inputService.SwitchToGameplay();

            var rooms = Object.FindObjectsOfType<RoomController>(true);

            if (rooms.Length == 0)
            {
                Debug.LogError("[RoomOrchestrator] No RoomController found in scene.");
                return;
            }

            foreach (var room in rooms)
                room.Deactivate();

            var starting = ResolveStartingRoom();

            if (starting == null)
            {
                Debug.LogWarning("[RoomOrchestrator] No starting room resolved — using first found.");
                starting = rooms[0];
            }

            starting.Activate();
            EnsureLiveCamera(starting, this.player.transform.position);
            this.currentRoom = starting;
        }

        private RoomController? ResolveStartingRoom()
        {
            var roomId  = this.sceneEntryContext.ConsumeRoom();
            var entryId = this.sceneEntryContext.Consume();

            if (roomId != null)
            {
                var room = RoomLookup.FindLoaded(roomId);
                if (room != null)
                {
                    PlaceAtFirstArrival(room);
                    return room;
                }
                Debug.LogError($"[RoomOrchestrator] Pending room '{roomId}' is not in this scene — starting in the default room.");
            }

            if (entryId != null)
            {
                var door    = DoorArrivalLookup.FindEntryDoor(
                    Object.FindObjectsByType<SceneDoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None), entryId);
                var arrival = door != null ? door.GetComponent<DoorArrival>() : null;
                var room    = door != null ? door.GetComponentInParent<RoomController>(true) : null;

                if (door == null)
                    Debug.LogError($"[RoomOrchestrator] No SceneDoorInteractable with entry '{entryId}' — starting in the default room.");
                else if (arrival == null || arrival.ArrivalPoint == null)
                    Debug.LogError($"[RoomOrchestrator] Entry door '{door.name}' ('{entryId}') has no DoorArrival — starting in the default room.", door);
                else if (room == null)
                    Debug.LogError($"[RoomOrchestrator] Entry door '{door.name}' ('{entryId}') is not inside a RoomController — starting in the default room.", door);
                else
                {
                    this.player.transform.SetPositionAndRotation(arrival.ArrivalPoint.position, arrival.ArrivalPoint.rotation);
                    arrival.ActivateCamera(this.zoneService);
                    return room;
                }
            }

            return this.context.StartingRoom;
        }

        public async UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab, Vector3 departurePosition)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.startedPublisher.Publish(new RoomTransitionStartedEvent(this.currentRoom!, destination));
            this.inputService.SwitchToDoorTransition();
            AudioListener.pause = true;

            var tcs = new UniTaskCompletionSource();
            this.context.Set(doorPrefab, this.inputService.DoorTransitionSkip, () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();

            var origin = this.currentRoom!;
            origin.Deactivate();
            destination.Activate();

            var spawnTransform = ArriveIn(destination, origin, departurePosition) ?? destination.transform;
            this.player.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
            EnsureLiveCamera(destination, spawnTransform.position);

            await tcs.Task;

            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            AudioListener.pause = false;
            this.inputService.SwitchToGameplay();
            this.currentRoom = destination;

            this.endedPublisher.Publish(new RoomTransitionedEvent(this.currentRoom));
            this.isTransitioning = false;
        }

        public RoomController? CurrentRoom     => this.currentRoom;
        public bool            IsTransitioning => this.isTransitioning;

        public void TeleportToRoom(RoomController room)
        {
            if (this.isTransitioning)
            {
                Debug.LogWarning($"[RoomOrchestrator] Teleport to '{room.RoomId}' ignored: a room transition is running.");
                return;
            }

            foreach (var other in Object.FindObjectsByType<RoomController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other != room) other.Deactivate();
            room.Activate();

            PlaceAtFirstArrival(room);
            EnsureLiveCamera(room, this.player.transform.position);
            this.currentRoom = room;
            this.endedPublisher.Publish(new RoomTransitionedEvent(room));
        }

        // Debug teleport / pending room: the first door arrival of the room, else its origin.
        private void PlaceAtFirstArrival(RoomController room)
        {
            var arrival = DoorArrivalLookup.FindFirstArrival(room);
            if (arrival == null)
            {
                Debug.LogError($"[RoomOrchestrator] Room '{room.RoomId}' has no door arrival — placing the player at the room origin.", room);
                this.player.transform.SetPositionAndRotation(room.transform.position, room.transform.rotation);
                return;
            }

            this.player.transform.SetPositionAndRotation(arrival.ArrivalPoint!.position, arrival.ArrivalPoint.rotation);
            arrival.ActivateCamera(this.zoneService);
        }

        public void ActivateRoomImmediate(string roomId, string? cameraPath = null)
        {
            var rooms  = Object.FindObjectsOfType<RoomController>(true);
            var target = RoomLookup.Find(rooms, roomId);

            // e.g. a save from before the room id migration: keep a playable room instead of
            // switching every room off.
            if (target == null)
            {
                Debug.LogWarning($"[RoomOrchestrator] ActivateRoomImmediate: no room with id '{roomId}' found — using the starting room.");
                target = this.context.StartingRoom;
                if (target == null) return;
            }

            foreach (var room in rooms)
                if (room != target) room.Deactivate();

            target.Activate();
            if (!TryActivateSavedCamera(target, cameraPath))
                EnsureLiveCamera(target, this.player.transform.position);
            this.currentRoom = target;
        }

        // A loaded save names its shot outright: the player is often standing outside every zone
        // trigger (the shot came from the last one they crossed), so position alone can't recover it.
        private bool TryActivateSavedCamera(RoomController room, string? cameraPath)
        {
            if (string.IsNullOrEmpty(cameraPath)) return false;

            var found = room.transform.Find(cameraPath);
            if (found == null || !found.TryGetComponent<CinemachineCamera>(out var camera))
            {
                Debug.LogWarning($"[RoomOrchestrator] Saved camera '{cameraPath}' not found in '{room.name}' — picking one from the player position.", room);
                return false;
            }

            this.zoneService.ActivateZone(camera);
            return true;
        }

        // Guarantees the room the player just landed in has a camera actually rendering.
        //
        // Most SpawnPoints have no camera assigned, so a room's shot normally comes from a
        // FixedCameraZoneTrigger reacting to the player position. That check only runs on the
        // next frame after a teleport, and never fires at all when the spawn position sits
        // outside every zone polygon. Until it fires, the brain is still on the camera of the
        // room we just deactivated -- which renders the inside of a hidden room, i.e. a black
        // screen.
        //
        // So resolve the same answer the trigger would give, immediately and deterministically,
        // and fall back to any camera in the room so it can never be left with none. A trigger
        // firing afterwards is harmless: it's normally the very camera picked here.
        private void EnsureLiveCamera(RoomController room, Vector3 spawnPosition)
        {
            var current = this.zoneService.CurrentZoneCamera;
            if (current != null && current.enabled && current.transform.IsChildOf(room.transform))
                return;

            var zoneCamera = FindZoneCameraAt(room, spawnPosition);
            if (zoneCamera != null)
            {
                this.zoneService.ActivateZone(zoneCamera);
                return;
            }

            var cameras = room.GetComponentsInChildren<CinemachineCamera>(includeInactive: true);
            if (cameras.Length == 0)
            {
                Debug.LogWarning($"[RoomOrchestrator] Room '{room.name}' has no CinemachineCamera — screen will stay on the previous room's shot.", room);
                return;
            }

            // Prefer whatever the room was authored to start on, same seed rule as
            // FixedCameraZoneBootstrap uses when it normalizes a room's cameras.
            foreach (var camera in cameras)
            {
                if (!camera.enabled) continue;
                this.zoneService.ActivateZone(camera);
                return;
            }

            this.zoneService.ActivateZone(cameras[0]);
        }

        private static CinemachineCamera? FindZoneCameraAt(RoomController room, Vector3 position)
        {
            foreach (var trigger in room.GetComponentsInChildren<FixedCameraZoneTrigger>(includeInactive: true))
            {
                if (trigger.ZoneCamera == null || trigger.IsCompleted) continue;

                if (trigger.Contains(position))
                {
                    var camera = trigger.ZoneCamera;
                    if (camera != null) return camera;
                }
            }
            return null;
        }

        // Where the player lands in destination, with its camera activated; null = room root.
        private Transform? ArriveIn(RoomController destination, RoomController origin, Vector3 departurePosition)
        {
            var arrival = ResolveArrival(destination, origin, departurePosition, out var problem);
            if (arrival != null)
            {
                arrival.ActivateCamera(this.zoneService);
                return arrival.ArrivalPoint;
            }

            Debug.LogError($"[RoomOrchestrator] {problem} — placing the player at the room root.", destination);
            return null;
        }

        // The door of the destination that leads back to the room the player came from owns the
        // arrival point. A missing door or arrival is a data error, described in problem.
        internal static DoorArrival? ResolveArrival(RoomController destination, RoomController origin, Vector3 departurePosition, out string? problem)
        {
            problem = null;
            var door = DoorArrivalLookup.FindArrivalDoor(destination, origin, departurePosition);
            if (door == null)
            {
                problem = $"No door back to '{origin.name}' in '{destination.name}'";
                return null;
            }

            var arrival = door.GetComponent<DoorArrival>();
            if (arrival == null || arrival.ArrivalPoint == null)
            {
                problem = $"Door '{door.name}' in '{destination.name}' has no DoorArrival";
                return null;
            }
            return arrival;
        }
    }
}
