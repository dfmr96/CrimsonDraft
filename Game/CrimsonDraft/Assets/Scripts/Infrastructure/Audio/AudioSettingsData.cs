#nullable enable

using UnityEngine;

namespace CrimsonDraft.Infrastructure.Audio
{
    [CreateAssetMenu(menuName = "CrimsonDraft/Audio/Audio Settings Data")]
    public sealed class AudioSettingsData : ScriptableObject
    {
        [SerializeField] private AK.Wwise.RTPC masterVolumeRtpc = new();
        [SerializeField] private AK.Wwise.RTPC sfxVolumeRtpc    = new();
        [SerializeField] private AK.Wwise.RTPC musicVolumeRtpc  = new();

        // RTPC.SetGlobalValue() no-ops safely when the field is left unassigned (IsValid()
        // guard in the Wwise wrapper), so these can be wired to real Wwise Game Parameters
        // later without any code changes here. Assumes a 0-100 RTPC range (Wwise convention);
        // adjust the *100f scale if the authored parameter uses a different range.
        public void ApplyMasterVolume(float value01) => this.masterVolumeRtpc.SetGlobalValue(value01 * 100f);
        public void ApplySfxVolume(float value01)    => this.sfxVolumeRtpc.SetGlobalValue(value01 * 100f);
        public void ApplyMusicVolume(float value01)  => this.musicVolumeRtpc.SetGlobalValue(value01 * 100f);
    }
}
