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
            var entryId = this.sceneEntryContext.Consume();

            if (entryId != null)
            {
                foreach (var sp in Object.FindObjectsOfType<SceneSpawnPoint>(true))
                {
                    if (sp.EntryPointId != entryId) continue;

                    this.player.transform.SetPositionAndRotation(
                        sp.transform.position, sp.transform.rotation);
                    sp.ActivateCamera(this.zoneService);
                    return sp.StartingRoom;
                }

                Debug.LogWarning($"[RoomOrchestrator] No SceneSpawnPoint with entry '{entryId}' — falling back.");
            }

            return this.context.StartingRoom;
        }

        public async UniTask TransitionToRoomAsync(RoomController destination, GameObject doorPrefab)
        {
            if (this.isTransitioning) return;
            this.isTransitioning = true;

            this.startedPublisher.Publish(new RoomTransitionStartedEvent(this.currentRoom!, destination));
            this.inputService.SwitchToDoorTransition();
            AudioListener.pause = true;

            var tcs = new UniTaskCompletionSource();
            this.context.Set(doorPrefab, this.inputService.DoorTransitionSkip, () => tcs.TrySetResult());

            await SceneManager.LoadSceneAsync(TransitionSceneName, LoadSceneMode.Additive).ToUniTask();

            this.currentRoom!.Deactivate();
            destination.Activate();

            var spawnPoint     = FindSpawnPoint(destination, this.currentRoom);
            var spawnTransform = spawnPoint != null ? spawnPoint.transform : destination.transform;
            this.player.transform.SetPositionAndRotation(spawnTransform.position, spawnTransform.rotation);
            spawnPoint?.ActivateCamera(this.zoneService);
            EnsureLiveCamera(destination, spawnTransform.position);

            await tcs.Task;

            await SceneManager.UnloadSceneAsync(TransitionSceneName).ToUniTask();

            AudioListener.pause = false;
            this.inputService.SwitchToGameplay();
            this.currentRoom = destination;

            this.endedPublisher.Publish(new RoomTransitionedEvent(this.currentRoom));
            this.isTransitioning = false;
        }

        public RoomController? CurrentRoom => this.currentRoom;

        public void ActivateRoomImmediate(string roomId)
        {
            var rooms = Object.FindObjectsOfType<RoomController>(true);
            RoomController? target = null;

            foreach (var room in rooms)
            {
                if (room.RoomId == roomId)
                {
                    target = room;
                    continue;
                }
                room.Deactivate();
            }

            if (target == null)
            {
                Debug.LogWarning($"[RoomOrchestrator] ActivateRoomImmediate: no room with id '{roomId}' found.");
                return;
            }

            target.Activate();
            EnsureLiveCamera(target, this.player.transform.position);
            this.currentRoom = target;
        }

        // Guarantees the room the player just landed in has a camera actually rendering.
        //
        // Most SpawnPoints have no camera assigned, so a room's shot normally comes from a
        // FixedCameraZoneTrigger reacting to the player via OnTriggerEnter. That's a
        // physics-timed callback on a teleported transform: it can land a frame or more late,
        // and never fires at all when the spawn position sits outside every zone volume. Until
        // it fires, the brain is still on the camera of the room we just deactivated -- which
        // renders the inside of a hidden room, i.e. a black screen.
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
                if (trigger.ZoneCamera == null) continue;

                var collider = trigger.GetComponent<Collider>();
                if (collider != null && Contains(collider, position))
                    return trigger.ZoneCamera;
            }
            return null;
        }

        private static bool Contains(Collider collider, Vector3 position)
        {
            // ClosestPoint is exact for the convex volumes zone triggers are built from, but it
            // throws on a non-convex MeshCollider -- those settle for the looser bounds test.
            if (collider is MeshCollider mesh && !mesh.convex)
                return collider.bounds.Contains(position);

            return (collider.ClosestPoint(position) - position).sqrMagnitude < 0.0001f;
        }

        private static SpawnPoint? FindSpawnPoint(RoomController destination, RoomController fromRoom)
        {
            foreach (var sp in destination.GetComponentsInChildren<SpawnPoint>(includeInactive: true))
            {
                if (sp.FromRoom == fromRoom)
                    return sp;
            }

            Debug.LogWarning($"[RoomOrchestrator] No SpawnPoint for '{fromRoom.name}' in '{destination.name}' — using room root.");
            return null;
        }
    }
}
