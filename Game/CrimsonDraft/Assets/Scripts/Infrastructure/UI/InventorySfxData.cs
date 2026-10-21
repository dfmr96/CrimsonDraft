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
        [SerializeField] private AK.Wwise.Event pageTurnEvent        = new();

        // Voice-note cassette: the intro (the tape starting) plays once, and when it ends the loop
        // takes over and runs until the note closes -- the loop is posted from the intro's
        // end-of-event callback rather than by a Wwise sequence container. Rewinding pauses the
        // tape and plays the reversed loop in its place; fast-forwarding raises the
        // VoiceTapeSpeed game parameter (50 = normal speed, 100 = fastest).
        [Header("Voice note tape")]
        [SerializeField] private AK.Wwise.Event voiceTapeEvent        = new(); // the intro
        [SerializeField] private AK.Wwise.Event voiceTapeLoopEvent    = new();
        [SerializeField] private AK.Wwise.Event voiceTapePauseEvent   = new();
        [SerializeField] private AK.Wwise.Event voiceTapeResumeEvent  = new();
        [SerializeField] private AK.Wwise.Event voiceTapeRewindEvent  = new();
        [SerializeField] private AK.Wwise.RTPC  voiceTapeSpeedRtpc    = new();
        [SerializeField] private float          voiceTapeNormalSpeed  = 50f;
        [SerializeField] private float          voiceTapeFastSpeed    = 100f;

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
        public void PlayPageTurn(GameObject postFrom)         => this.pageTurnEvent.Post(postFrom);

        // Bumped on every start/stop so an intro that ends because it was stopped (which also
        // fires its end-of-event callback) can't start the loop of a tape that is already gone.
        private int tapeSession;

        public void StartVoiceTape(GameObject postFrom)
        {
            int session = ++this.tapeSession;
            this.voiceTapeSpeedRtpc.SetGlobalValue(this.voiceTapeNormalSpeed);
            this.voiceTapeEvent.Post(
                postFrom,
                (uint)AkCallbackType.AK_EndOfEvent,
                (cookie, type, info) =>
                {
                    if (session == this.tapeSession && postFrom != null)
                        this.voiceTapeLoopEvent.Post(postFrom);
                });
        }

        public void StopVoiceTape(GameObject postFrom)
        {
            this.tapeSession++;
            this.voiceTapeEvent.Stop(postFrom);
            this.voiceTapeLoopEvent.Stop(postFrom);
            this.voiceTapeRewindEvent.Stop(postFrom);
            this.voiceTapeSpeedRtpc.SetGlobalValue(this.voiceTapeNormalSpeed);
        }

        public void SetVoiceTapeFast(bool fast) =>
            this.voiceTapeSpeedRtpc.SetGlobalValue(fast ? this.voiceTapeFastSpeed : this.voiceTapeNormalSpeed);

        public void BeginVoiceTapeRewind(GameObject postFrom)
        {
            this.voiceTapePauseEvent.Post(postFrom);
            this.voiceTapeRewindEvent.Post(postFrom);
        }

        public void EndVoiceTapeRewind(GameObject postFrom)
        {
            this.voiceTapeRewindEvent.Stop(postFrom);
            this.voiceTapeResumeEvent.Post(postFrom);
        }
    }
}
