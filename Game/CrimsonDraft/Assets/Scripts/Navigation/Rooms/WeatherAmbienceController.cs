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
            // Subscribing here (VContainer's Awake-phase) is safe — it's plain C# event
            // wiring with no Wwise dependency, and RoomTransitionedEvent can't fire before
            // the player acts, well after Start().
            this.subscription = this.roomTransitionedSubscriber.Subscribe(OnRoomTransitioned);
        }

        // Posting the ambience event has to wait for Start(): AkBank loaders run at
        // DefaultExecutionOrder(-75), which only guarantees they finish before other
        // default-order Start() calls — not before VContainer's Awake-phase Initialize().
        // Posting there raced the SoundBank load and Wwise silently dropped the event
        // ("Event ID not found") because Play_WeatherBC's bank wasn't loaded yet.
        private void Start()
        {
            // Apply the room's State/RTPC before posting — otherwise the Storm BlendContainer
            // starts playing against whatever mix was active a moment earlier (e.g. leftover
            // Wwise Authoring/Soundcaster state) instead of the intended silent default.
            ApplyRoom(this.roomOrchestrator.CurrentRoom);
            this.weatherEvent.Post(gameObject);
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
