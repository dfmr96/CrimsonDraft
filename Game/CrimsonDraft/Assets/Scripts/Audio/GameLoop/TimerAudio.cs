#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TimerManager))]
    public sealed class TimerAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger defeatTrigger  = new();
        [SerializeField] private WwiseTrigger lowTime1Trigger = new();
        [SerializeField] private WwiseTrigger lowTime2Trigger = new();
        

        public void PlayDefeat()  => defeatTrigger.Fire(gameObject);
        public void PlayLowTime1() => lowTime1Trigger.Fire(gameObject);
        public void PlayLowTime2() => lowTime2Trigger.Fire(gameObject);


    }
}
