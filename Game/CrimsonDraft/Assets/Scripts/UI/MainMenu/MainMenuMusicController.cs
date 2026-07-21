#nullable enable

using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    public sealed class MainMenuMusicController : MonoBehaviour
    {
        [SerializeField] private AK.Wwise.Event mscEvent  = new(); // Play_MSC_Manager
        [SerializeField] private AK.Wwise.State menuState = new(); // PlayerState:Menu

        // Deferred to Start() — AkBank loaders run at DefaultExecutionOrder(-75), same
        // timing issue documented on WeatherAmbienceController/MusicManagerController:
        // posting before the bank finishes loading makes Wwise silently drop the event.
        private void Start()
        {
            this.menuState.SetValue();
            this.mscEvent.Post(gameObject);
        }
    }
}
