#nullable enable

using Cysharp.Threading.Tasks;
using MessagePipe;
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
            this.currentRoom = target;
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
