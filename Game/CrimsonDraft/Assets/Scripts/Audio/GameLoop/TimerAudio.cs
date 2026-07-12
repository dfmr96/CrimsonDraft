#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TimerManager))]
    public sealed class TimerAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger defeatTrigger  = new();
        [SerializeField] private WwiseTrigger lowTimeTrigger = new();

        public void PlayDefeat()  => defeatTrigger.Fire(gameObject);
        public void PlayLowTime() => lowTimeTrigger.Fire(gameObject);
    }
}
