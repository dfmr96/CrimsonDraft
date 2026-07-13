#nullable enable

using System.Collections;
using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(TutorialUI))]
    public sealed class TutorialAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger openTrigger  = new();
        [SerializeField] private WwiseTrigger closeTrigger = new();

        [Header("Retry (SoundBank may still be loading on scene start)")]
        [SerializeField] private int   maxRetries    = 10;
        [SerializeField] private float retryInterval = 0.5f;

        public void PlayOpen()  => StartCoroutine(FireWithRetry(openTrigger));
        public void PlayClose() => StartCoroutine(FireWithRetry(closeTrigger));

        private IEnumerator FireWithRetry(WwiseTrigger trigger)
        {
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                if (trigger.Fire(gameObject))
                    yield break;

                yield return new WaitForSecondsRealtime(retryInterval);
            }
        }
    }
}
