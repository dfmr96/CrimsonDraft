#nullable enable

using DG.Tweening;
using UnityEngine;

namespace CrimsonDraft.UI.MainMenu
{
    /// <summary>
    /// What plays behind the menus: on the title screen only a soft rain loop, no music; in Options
    /// the rain fades out and the music fades in; in New Game and Load Game there is neither; back at
    /// the title everything fades out and the rain fades back in. Changes never cut: whatever was
    /// playing fades out first, and the next sound starts after a short gap with a fade in.
    /// <see cref="MainMenuPanelNavigator"/> calls <see cref="EnterMainMenu"/>, <see cref="EnterOptions"/>
    /// or <see cref="EnterSilentSubMenu"/>.
    /// </summary>
    public sealed class MainMenuMusicController : MonoBehaviour
    {
        private enum Mode { None, Rain, Music }

        [Tooltip("Evento de Wwise de la musica de Options (loop), por nombre.")]
        [SerializeField] private string settingsEventName = "Play_UI_MainMenu_Settings";
        [Tooltip("Evento de Wwise de la lluvia del titulo (loop), por nombre.")]
        [SerializeField] private string rainEventName = "Play_UI_MainMenu_Rain";

        [Header("Fades")]
        [SerializeField] private int   fadeOutMs = 800;
        [SerializeField] private int   fadeInMs  = 1500;
        [Tooltip("Silencio entre que termina de apagarse lo anterior y empieza lo nuevo.")]
        [SerializeField] private float gapSeconds = 0.15f;

        /// <summary>How long until whatever was playing is gone (fade out plus the gap).</summary>
        public float SilenceSeconds => this.fadeOutMs / 1000f + this.gapSeconds;

        private Mode  target = Mode.None;
        private uint  musicId;
        private uint  rainId;
        private Tween? pendingStart;

        // Deferred to Start() — AkBank loaders run at DefaultExecutionOrder(-75), same
        // timing issue documented on WeatherAmbienceController/MusicManagerController:
        // posting before the bank finishes loading makes Wwise silently drop the event.
        private void Start() => EnterMainMenu();

        /// <summary>Title screen: rain.</summary>
        public void EnterMainMenu() => SwitchTo(Mode.Rain);

        /// <summary>Options is open: music.</summary>
        public void EnterOptions() => SwitchTo(Mode.Music);

        /// <summary>New Game / Load Game: no rain and no music.</summary>
        public void EnterSilentSubMenu() => SwitchTo(Mode.None);

        private void SwitchTo(Mode next)
        {
            if (next == this.target) return;
            this.target = next;

            this.pendingStart?.Kill();
            this.pendingStart = null;

            bool fadingOut = false;
            if (next != Mode.Rain)  fadingOut |= Stop(ref this.rainId);
            if (next != Mode.Music) fadingOut |= Stop(ref this.musicId);
            if (next == Mode.None) return;

            float delay = fadingOut ? this.fadeOutMs / 1000f + this.gapSeconds : 0f;
            if (delay <= 0f)
            {
                StartMode(next);
                return;
            }

            this.pendingStart = DOVirtual.DelayedCall(delay, () =>
            {
                this.pendingStart = null;
                StartMode(next);
            }, ignoreTimeScale: true).SetTarget(this);
        }

        private void StartMode(Mode mode)
        {
            if (mode == Mode.Rain && this.rainId == 0 && !string.IsNullOrEmpty(this.rainEventName))
                this.rainId = FadeIn(AkSoundEngine.PostEvent(this.rainEventName, gameObject));
            else if (mode == Mode.Music && this.musicId == 0 && !string.IsNullOrEmpty(this.settingsEventName))
                this.musicId = FadeIn(AkSoundEngine.PostEvent(this.settingsEventName, gameObject));
        }

        // Posted and paused in the same breath, then resumed with a transition: Wwise fades the
        // resume in from silence.
        private uint FadeIn(uint playingId)
        {
            if (playingId == 0 || this.fadeInMs <= 0) return playingId;
            AkSoundEngine.ExecuteActionOnPlayingID(AkActionOnEventType.AkActionOnEventType_Pause, playingId, 0,
                AkCurveInterpolation.AkCurveInterpolation_Linear);
            AkSoundEngine.ExecuteActionOnPlayingID(AkActionOnEventType.AkActionOnEventType_Resume, playingId, this.fadeInMs,
                AkCurveInterpolation.AkCurveInterpolation_Linear);
            return playingId;
        }

        private bool Stop(ref uint playingId)
        {
            if (playingId == 0) return false;
            AkSoundEngine.StopPlayingID(playingId, this.fadeOutMs, AkCurveInterpolation.AkCurveInterpolation_Linear);
            playingId = 0;
            return true;
        }

        private void OnDestroy() => DOTween.Kill(this);
    }
}
