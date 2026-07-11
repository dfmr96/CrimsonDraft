#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(ScoreManager))]
    public sealed class ScoreManagerAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event victoryEvent = new();

        public void PlayVictory() => victoryEvent?.Post(gameObject);
    }
}
