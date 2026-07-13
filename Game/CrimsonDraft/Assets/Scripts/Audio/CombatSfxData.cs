#nullable enable

using UnityEngine;

namespace CrimsonDraft.Audio
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Audio/Combat Sfx Data")]
    public sealed class CombatSfxData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.Event cursorEvent = new();
        [SerializeField] private AK.Wwise.Event decideEvent = new();
        [SerializeField] private AK.Wwise.Event cancelEvent = new();

        public void PlayCursor(GameObject postFrom) => this.cursorEvent.Post(postFrom);
        public void PlayDecide(GameObject postFrom) => this.decideEvent.Post(postFrom);
        public void PlayCancel(GameObject postFrom) => this.cancelEvent.Post(postFrom);
    }
}
