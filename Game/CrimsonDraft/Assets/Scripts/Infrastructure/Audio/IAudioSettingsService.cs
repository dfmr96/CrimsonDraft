#nullable enable

namespace CrimsonDraft.Infrastructure.Audio
{
    public interface IAudioSettingsService
    {
        float MasterVolume { get; }
        float SfxVolume { get; }
        float MusicVolume { get; }

        void SetMasterVolume(float value01);
        void SetSfxVolume(float value01);
        void SetMusicVolume(float value01);
    }
}
