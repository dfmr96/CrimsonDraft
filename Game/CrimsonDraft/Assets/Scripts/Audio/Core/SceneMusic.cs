#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    // Drop on any GameObject in a scene to stop everything else and play this
    // scene's music once, on load (e.g. Play_Victory in Victory_Scene,
    // Play_Defeat in Defeat_Scene).
    public sealed class SceneMusic : MonoBehaviour
    {
        [Header("Wwise")]
        [SerializeField] private WwiseTrigger stopAllTrigger = new();
        [SerializeField] private WwiseTrigger musicTrigger   = new();

        private void Start()
        {
            stopAllTrigger.Fire(gameObject);
            musicTrigger.Fire(gameObject);
        }
    }
}
