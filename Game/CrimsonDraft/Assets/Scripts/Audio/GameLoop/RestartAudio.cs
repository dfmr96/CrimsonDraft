#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(RestartGame))]
    public sealed class RestartAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger restartTrigger = new();

        public void PlayRestart() => restartTrigger.Fire(gameObject);
    }
}
