#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    [CreateAssetMenu(menuName = "CrimsonDraft/UI/Main Menu Sfx Data")]
    public sealed class MainMenuSfxData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.Event cursorEvent      = new();
        [SerializeField] private AK.Wwise.Event decideEvent      = new();
        [SerializeField] private AK.Wwise.Event cancelEvent      = new();
        [SerializeField] private AK.Wwise.Event knobTickEvent    = new();
        [SerializeField] private AK.Wwise.Event knobLimitEvent   = new();
        [SerializeField] private AK.Wwise.Event panelTravelEvent = new();
        [SerializeField] private AK.Wwise.Event startEvent       = new();
        [SerializeField] private AK.Wwise.Event filesChangeEvent = new();

        public void PlayCursor(GameObject postFrom)      => this.cursorEvent.Post(postFrom);
        public void PlayDecide(GameObject postFrom)      => this.decideEvent.Post(postFrom);
        public void PlayCancel(GameObject postFrom)      => this.cancelEvent.Post(postFrom);
        public void PlayKnobTick(GameObject postFrom)    => this.knobTickEvent.Post(postFrom);
        public void PlayKnobLimit(GameObject postFrom)   => this.knobLimitEvent.Post(postFrom);
        public void PlayPanelTravel(GameObject postFrom) => this.panelTravelEvent.Post(postFrom);
        public void PlayStart(GameObject postFrom)       => this.startEvent.Post(postFrom);
        public void PlayFilesChange(GameObject postFrom) => this.filesChangeEvent.Post(postFrom);
    }
}
