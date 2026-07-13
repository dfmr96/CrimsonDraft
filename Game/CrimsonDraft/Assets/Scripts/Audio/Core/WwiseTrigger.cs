#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [System.Serializable]
    public sealed class WwiseTrigger
    {
        public enum Kind
        {
            Event,
            State,
            Switch,
            Rtpc,
        }

        [SerializeField] private Kind kind = Kind.Event;

        [SerializeField] private AK.Wwise.Event wwiseEvent = new();
        [SerializeField] private AK.Wwise.State  wwiseState  = new();
        [SerializeField] private AK.Wwise.Switch wwiseSwitch = new();
        [SerializeField] private AK.Wwise.RTPC   wwiseRtpc   = new();
        [SerializeField] private float           rtpcValue   = 1f;

        /// Returns false only when an Event kind fails to post (e.g. its SoundBank
        /// hasn't finished loading yet) — callers can use this to retry.
        public bool Fire(GameObject target)
        {
            var parentName = target.transform.parent != null ? target.transform.parent.name : "(no parent)";
            Debug.Log($"[WwiseTrigger] Fire on '{target.name}' — parent: '{parentName}'");

            switch (kind)
            {
                case Kind.Event:
                    Debug.Log($"[WwiseTrigger] Fire Event on '{target.name}' — valid: {wwiseEvent.IsValid()} (\"{wwiseEvent.Name}\")");
                    var playingId = wwiseEvent?.Post(target) ?? 0;
                    Debug.Log($"[WwiseTrigger] Event.Post result — playingId: {playingId} (0 = failed/invalid)");
                    return playingId != 0;
                case Kind.State:
                    Debug.Log($"[WwiseTrigger] Fire State — valid: {wwiseState.IsValid()} (\"{wwiseState.Name}\")");
                    wwiseState?.SetValue();
                    return true;
                case Kind.Switch:
                    Debug.Log($"[WwiseTrigger] Fire Switch on '{target.name}' — valid: {wwiseSwitch.IsValid()} (\"{wwiseSwitch.Name}\")");
                    wwiseSwitch?.SetValue(target);
                    return true;
                case Kind.Rtpc:
                    Debug.Log($"[WwiseTrigger] Fire RTPC on '{target.name}' — valid: {wwiseRtpc.IsValid()} (\"{wwiseRtpc.Name}\") value: {rtpcValue}");
                    wwiseRtpc?.SetValue(target, rtpcValue);
                    return true;
                default:
                    return true;
            }
        }
    }
}
