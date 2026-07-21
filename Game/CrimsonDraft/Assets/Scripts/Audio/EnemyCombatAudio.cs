#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    public sealed class EnemyCombatAudio : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Event damageEvent = new();

        // Called by Animation Event on the Hit_1 / Hit_2 clips.
        public void PlayDamageSfx() => this.damageEvent.Post(gameObject);
    }
}
