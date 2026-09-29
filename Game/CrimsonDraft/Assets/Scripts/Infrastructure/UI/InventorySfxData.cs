#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI
{
    [CreateAssetMenu(menuName = "CrimsonDraft/UI/Inventory Sfx Data")]
    public sealed class InventorySfxData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.Event cursorEvent          = new();
        [SerializeField] private AK.Wwise.Event decideEvent          = new();
        [SerializeField] private AK.Wwise.Event cancelEvent          = new();
        [SerializeField] private AK.Wwise.Event mapOpenEvent         = new();
        [SerializeField] private AK.Wwise.Event invalidActionEvent   = new();
        [SerializeField] private AK.Wwise.Event fileOpenEvent        = new();
        [SerializeField] private AK.Wwise.Event voiceTypewriterEvent = new();
        [SerializeField] private AK.Wwise.Event beeperMorseEvent     = new();
        [SerializeField] private AK.Wwise.Event voiceStartEvent      = new();

        public void PlayCursor(GameObject postFrom)          => this.cursorEvent.Post(postFrom);
        public void PlayDecide(GameObject postFrom)          => this.decideEvent.Post(postFrom);
        public void PlayCancel(GameObject postFrom)          => this.cancelEvent.Post(postFrom);
        public void PlayMapOpen(GameObject postFrom)         => this.mapOpenEvent.Post(postFrom);
        public void PlayInvalidAction(GameObject postFrom)   => this.invalidActionEvent.Post(postFrom);
        public void PlayFileOpen(GameObject postFrom)        => this.fileOpenEvent.Post(postFrom);
        public void PlayVoiceTypewriter(GameObject postFrom) => this.voiceTypewriterEvent.Post(postFrom);
        public void PlayBeeperMorseStart(GameObject postFrom) => this.beeperMorseEvent.Post(postFrom);
        public void StopBeeperMorse(GameObject postFrom)      => this.beeperMorseEvent.Stop(postFrom);
        public void PlayVoiceStart(GameObject postFrom)       => this.voiceStartEvent.Post(postFrom);
    }
}
