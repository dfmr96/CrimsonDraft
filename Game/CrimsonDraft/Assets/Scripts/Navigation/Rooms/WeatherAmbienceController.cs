#nullable enable

using System;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using CrimsonDraft.Navigation;

namespace CrimsonDraft.Navigation.Rooms
{
    public sealed class WeatherAmbienceController : MonoBehaviour, IInitializable, IDisposable
    {
        [SerializeField] private AK.Wwise.Event weatherEvent          = new();
        [SerializeField] private AK.Wwise.RTPC  insideStormForceRtpc  = new();
        [SerializeField] private AK.Wwise.RTPC  outsideStormForceRtpc = new();
        [SerializeField] private AK.Wwise.State defaultAmbientState   = new();

        [Inject] private IRoomOrchestrator                  roomOrchestrator           = null!;
        [Inject] private ISubscriber<RoomTransitionedEvent> roomTransitionedSubscriber = null!;

        private IDisposable? subscription;

        void IInitializable.Initialize()
        {
            this.subscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);

            this.weatherEvent.Post(gameObject);
            ApplyRoom(this.roomOrchestrator.CurrentRoom);
        }

        private void OnRoomTransitioned(RoomTransitionedEvent e) => ApplyRoom(e.ActiveRoom);

        private void ApplyRoom(RoomController? room)
        {
            var profile = room != null ? room.GetComponent<RoomWeatherProfile>() : null;

            if (profile != null)
            {
                profile.AmbientState.SetValue();
                this.insideStormForceRtpc.SetGlobalValue(profile.InsideStormForce);
                this.outsideStormForceRtpc.SetGlobalValue(profile.OutsideStormForce);
            }
            else
            {
                this.defaultAmbientState.SetValue();
                this.insideStormForceRtpc.SetGlobalValue(0f);
                this.outsideStormForceRtpc.SetGlobalValue(0f);
            }
        }

        void IDisposable.Dispose() => this.subscription?.Dispose();
    }
}
