#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Audio
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Audio/Audio Settings Data")]
    public sealed class AudioSettingsData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.RTPC masterVolumeRtpc = new();
        [SerializeField] private AK.Wwise.RTPC sfxVolumeRtpc    = new();
        // Drives the UI bus's own volume (it sits beside the SFX bus, not under it, so the SFX
        // slider never touches UI sounds).
        [SerializeField] private AK.Wwise.RTPC uiSfxVolumeRtpc  = new();
        [SerializeField] private AK.Wwise.RTPC musicVolumeRtpc  = new();

        // RTPC.SetGlobalValue() no-ops safely when the field is left unassigned (IsValid()
        // guard in the Wwise wrapper), so these can be wired to real Wwise Game Parameters
        // later without any code changes here. Assumes a 0-100 RTPC range (Wwise convention);
        // adjust the *100f scale if the authored parameter uses a different range.
        public void ApplyMasterVolume(float value01) => this.masterVolumeRtpc.SetGlobalValue(value01 * 100f);
        public void ApplySfxVolume(float value01)    => this.sfxVolumeRtpc.SetGlobalValue(value01 * 100f);
        public void ApplyUiSfxVolume(float value01)  => this.uiSfxVolumeRtpc.SetGlobalValue(value01 * 100f);
        public void ApplyMusicVolume(float value01)  => this.musicVolumeRtpc.SetGlobalValue(value01 * 100f);
    }
}
