#nullable enable

namespace CrimsonDraft.Infrastructure.Audio
{
    public interface IAudioSettingsService
    {
        float MasterVolume { get; }
        float SfxVolume { get; }
        /// <summary>Volume of the UI bus (cursor, decide, denied...), independent of <see cref="SfxVolume"/>.</summary>
        float UiSfxVolume { get; }
        float MusicVolume { get; }

        void SetMasterVolume(float value01);
        void SetSfxVolume(float value01);
        void SetUiSfxVolume(float value01);
        void SetMusicVolume(float value01);
    }
}
