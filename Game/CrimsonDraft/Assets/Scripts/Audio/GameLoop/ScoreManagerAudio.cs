#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(ScoreManager))]
    public sealed class ScoreManagerAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger victoryTrigger = new();

        public void PlayVictory() => victoryTrigger.Fire(gameObject);
    }
}
