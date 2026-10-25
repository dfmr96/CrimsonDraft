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
        [Tooltip("Trueno de la transicion a New Game / Load Game (por nombre de evento). Vacio = sin sonido.")]
        [SerializeField] private string thunderEventName = "Play_UI_MainMenu_Thunder";
        [Tooltip("Sonido del fundido largo al empezar la partida (Start en New Game / confirmar una carga), por nombre de evento. Vacio = sin sonido.")]
        [SerializeField] private string gameStartEventName = "Play_UI_MainMenu_StormStart";

        public void PlayCursor(GameObject postFrom)      => this.cursorEvent.Post(postFrom);
        public void PlayDecide(GameObject postFrom)      => this.decideEvent.Post(postFrom);
        public void PlayCancel(GameObject postFrom)      => this.cancelEvent.Post(postFrom);
        public void PlayKnobTick(GameObject postFrom)    => this.knobTickEvent.Post(postFrom);
        public void PlayKnobLimit(GameObject postFrom)   => this.knobLimitEvent.Post(postFrom);
        public void PlayPanelTravel(GameObject postFrom) => this.panelTravelEvent.Post(postFrom);
        public void PlayStart(GameObject postFrom)       => this.startEvent.Post(postFrom);
        public void PlayFilesChange(GameObject postFrom) => this.filesChangeEvent.Post(postFrom);

        public void PlayGameStart(GameObject postFrom)
        {
            if (!string.IsNullOrEmpty(this.gameStartEventName)) AkSoundEngine.PostEvent(this.gameStartEventName, postFrom);
        }

        public void PlayThunder(GameObject postFrom)
        {
            if (!string.IsNullOrEmpty(this.thunderEventName)) AkSoundEngine.PostEvent(this.thunderEventName, postFrom);
        }
    }
}
