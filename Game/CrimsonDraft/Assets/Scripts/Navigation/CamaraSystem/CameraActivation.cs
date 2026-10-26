#nullable enable

using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;

namespace CrimsonDraft.Navigation.CamaraSystem
{
    public enum CameraActivationState
    {
        /// <summary>No zone activates the camera.</summary>
        None,
        /// <summary>Exactly one zone: the camera's activation polygon.</summary>
        Ok,
        /// <summary>Several zones: to be reduced to one by hand.</summary>
        Legacy,
    }

    public readonly struct CameraActivationStatus
    {
        public readonly CameraActivationState State;
        public readonly IReadOnlyList<FixedCameraZoneTrigger> Triggers;

        public CameraActivationStatus(CameraActivationState state, IReadOnlyList<FixedCameraZoneTrigger> triggers)
        {
            this.State = state;
            this.Triggers = triggers;
        }
    }

    /// <summary>One camera, one activation polygon: tells how far a camera is from that.</summary>
    public static class CameraActivation
    {
        public static CameraActivationStatus For(CinemachineCamera camera, IEnumerable<FixedCameraZoneTrigger> roomTriggers)
        {
            var triggers = roomTriggers.Where(t => t != null && t.ReferencedCameras.Contains(camera)).ToArray();
            var state = triggers.Length == 0 ? CameraActivationState.None
                : triggers.Length == 1 ? CameraActivationState.Ok
                : CameraActivationState.Legacy;
            return new CameraActivationStatus(state, triggers);
        }
    }
}
