#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TimerManager))]
    public sealed class TimerAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event defeatEvent  = new();
        [SerializeField] private AK.Wwise.Event lowTimeEvent = new();

        public void PlayDefeat()  => defeatEvent?.Post(gameObject);
        public void PlayLowTime() => lowTimeEvent?.Post(gameObject);
    }
}
