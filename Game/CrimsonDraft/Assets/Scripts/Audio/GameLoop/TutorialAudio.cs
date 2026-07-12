#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TutorialUI))]
    public sealed class TutorialAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger openTrigger  = new();
        [SerializeField] private WwiseTrigger closeTrigger = new();

        public void PlayOpen()  => openTrigger.Fire(gameObject);
        public void PlayClose() => closeTrigger.Fire(gameObject);
    }
}
