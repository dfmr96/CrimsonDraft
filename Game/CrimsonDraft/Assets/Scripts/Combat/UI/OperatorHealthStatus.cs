#nullable enable

using UnityEngine;

namespace CrimsonDraft.Combat
{
    /// <summary>
    /// Text + color shown next to an operator's ECG (CDG_Status in the combat overview).
    /// Colors mirror the Inventory card's ECG band colors (EcgHeartbeatPulse in Card.prefab).
    /// </summary>
    public enum OperatorHealthStatus
    {
        Fine,
        Caution,
        Danger,
        Critical,
    }

    public static class OperatorHealthStatusStyle
    {
        public static string GetLabel(OperatorHealthStatus status) => status switch
        {
            OperatorHealthStatus.Fine    => "Fine",
            OperatorHealthStatus.Caution => "Caution",
            OperatorHealthStatus.Danger  => "Danger",
            _                            => "Critical",
        };

        public static Color GetColor(OperatorHealthStatus status) => status switch
        {
            OperatorHealthStatus.Fine    => new Color(0.09019608f, 0.45882353f, 0.19607843f),
            OperatorHealthStatus.Caution => new Color(0.6901961f, 0.7058824f, 0.29803923f),
            OperatorHealthStatus.Danger  => new Color(0.59607846f, 0.3529412f, 0.050980393f),
            _                            => new Color(1f, 0f, 0f),
        };
    }
}
