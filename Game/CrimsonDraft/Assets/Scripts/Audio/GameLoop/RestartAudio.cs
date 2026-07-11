#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [RequireComponent(typeof(RestartGame))]
    public sealed class RestartAudio : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private AK.Wwise.Event restartEvent = new();

        public void PlayRestart() => restartEvent?.Post(gameObject);
    }
}
