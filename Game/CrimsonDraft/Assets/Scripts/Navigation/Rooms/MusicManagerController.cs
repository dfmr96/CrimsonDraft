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
        [SerializeField] private AK.Wwise.Switch doorsSector     = new(); // MarineraSector:Doors
        [SerializeField] private AK.Wwise.Switch saveRoomSector  = new(); // MarineraSector:SafeRoom
        [SerializeField] private AK.Wwise.Switch defaultSector   = new(); // MarineraSector fallback (e.g. DeckB)

        [Inject] private IRoomOrchestrator                        roomOrchestrator                = null!;
        [Inject] private ISubscriber<RoomTransitionStartedEvent>  roomTransitionStartedSubscriber  = null!;
        [Inject] private ISubscriber<RoomTransitionedEvent>       roomTransitionedSubscriber       = null!;

        private IDisposable? startedSubscription;
        private IDisposable? transitionedSubscription;
        private uint          lastSectorId = AK.Wwise.BaseType.InvalidId;

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
            // PlayerState stays Navigation for this whole feature — SafeRoom is now a
            // MarineraSector value instead of a separate PlayerState, so there's no state
            // transition to manage here (Combat/Dialogue/Menu/GameOver are still deferred).
            this.navigationState.SetValue();
            ApplySector(this.roomOrchestrator.CurrentRoom);
            this.mscEvent.Post(gameObject);
        }

        private void OnRoomTransitionStarted(RoomTransitionStartedEvent e)
        {
            this.doorsSector.SetValue(gameObject);
            this.lastSectorId = this.doorsSector.Id;
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e) => ApplySector(e.ActiveRoom);

        private void ApplySector(RoomController? room)
        {
            var isSaveRoom = room != null && room.GetComponent<SaveRoomMarker>() != null;
            AK.Wwise.Switch targetSwitch;

            if (isSaveRoom)
            {
                targetSwitch = this.saveRoomSector;
            }
            else
            {
                var sectorProfile = room != null ? room.GetComponent<RoomSectorProfile>() : null;
                targetSwitch = sectorProfile != null ? sectorProfile.MarineraSector : this.defaultSector;
            }

            // Wwise treats every SetSwitch call as a cue to re-evaluate the Music Switch
            // Container, restarting the current segment even when the value doesn't
            // actually change (e.g. moving between two rooms that both resolve to DeckB).
            // Only call SetValue when the resolved switch is genuinely different.
            if (targetSwitch.Id == this.lastSectorId)
                return;

            targetSwitch.SetValue(gameObject);
            this.lastSectorId = targetSwitch.Id;
        }

        void IDisposable.Dispose()
        {
            this.startedSubscription?.Dispose();
            this.transitionedSubscription?.Dispose();
        }
    }
}
