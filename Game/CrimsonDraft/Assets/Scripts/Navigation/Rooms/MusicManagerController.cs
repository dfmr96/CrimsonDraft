#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class MusicManagerController : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private AK.Wwise.Event  mscEvent        = new(); // Play_MSC_Manager
        [SerializeField] private AK.Wwise.State  navigationState = new(); // PlayerState:Navigation
        [SerializeField] private AK.Wwise.State  safeRoomState   = new(); // PlayerState:SafeRoom
        [SerializeField] private AK.Wwise.Switch doorsSector     = new(); // MarineraSector:Doors
        [SerializeField] private AK.Wwise.Switch defaultSector   = new(); // MarineraSector fallback (e.g. DeckB)

        [Inject] private IRoomOrchestrator                        roomOrchestrator                = null!;
        [Inject] private ISubscriber<RoomTransitionStartedEvent>  roomTransitionStartedSubscriber  = null!;
        [Inject] private ISubscriber<RoomTransitionedEvent>       roomTransitionedSubscriber       = null!;

        private IDisposable? startedSubscription;
        private IDisposable? transitionedSubscription;

        void IInitializable.Initialize()
        {
            this.startedSubscription      = this.roomTransitionStartedSubscriber.Subscribe(OnRoomTransitionStarted);
            this.transitionedSubscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);
        }

        // Deferred to Start() (not here): posting Play_MSC_Manager during VContainer's
        // Awake-phase Initialize() races Wwise's SoundBank load, same issue fixed on
        // WeatherAmbienceController — Wwise silently drops the event if posted too early.
        private void Start()
        {
            ApplyRoom(this.roomOrchestrator.CurrentRoom);
            this.mscEvent.Post(gameObject);
        }

        private void OnRoomTransitionStarted(RoomTransitionStartedEvent e) => this.doorsSector.SetValue(gameObject);

        private void OnRoomTransitioned(RoomTransitionedEvent e) => ApplyRoom(e.ActiveRoom);

        private void ApplyRoom(RoomController? room)
        {
            var sectorProfile = room != null ? room.GetComponent<RoomSectorProfile>() : null;
            if (sectorProfile != null)
                sectorProfile.MarineraSector.SetValue(gameObject);
            else
                this.defaultSector.SetValue(gameObject);

            var isSaveRoom = room != null && room.GetComponent<SaveRoomMarker>() != null;
            if (isSaveRoom)
                this.safeRoomState.SetValue();
            else
                this.navigationState.SetValue();
        }

        void IDisposable.Dispose()
        {
            this.startedSubscription?.Dispose();
            this.transitionedSubscription?.Dispose();
        }
    }
}
