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

        public void Fire(GameObject target)
        {
            switch (kind)
            {
                case Kind.Event:
                    wwiseEvent?.Post(target);
                    break;
                case Kind.State:
                    wwiseState?.SetValue();
                    break;
                case Kind.Switch:
                    wwiseSwitch?.SetValue(target);
                    break;
                case Kind.Rtpc:
                    wwiseRtpc?.SetValue(target, rtpcValue);
                    break;
            }
        }
    }
}
