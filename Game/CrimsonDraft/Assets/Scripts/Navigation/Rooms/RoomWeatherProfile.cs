#nullable enable

using UnityEngine;

namespace CrimsonDraft.Navigation.Rooms
{
    // Optional sibling of RoomController on any room GameObject with weather exposure.
    // Rooms without this component fall back to WeatherAmbienceController's silent default.
    public sealed class RoomWeatherProfile : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.State ambientState = new();
        [SerializeField] private float          insideStormForce;
        [SerializeField] private float          outsideStormForce;

        public AK.Wwise.State AmbientState      => this.ambientState;
        public float          InsideStormForce  => this.insideStormForce;
        public float          OutsideStormForce => this.outsideStormForce;
    }
}
