#nullable enable

using UnityEngine;
using VContainer.Unity;
using UnityEngine.Scripting;

namespace CrimsonDraft.Infrastructure.Audio
{
    public sealed class AudioSettingsService : IAudioSettingsService, IInitializable
    {
        private const string MasterVolumeKey = "Audio.MasterVolume";
        private const string SfxVolumeKey    = "Audio.SfxVolume";
        private const string MusicVolumeKey  = "Audio.MusicVolume";
        private const float  DefaultVolume   = 1f;

        private readonly AudioSettingsData data;

        public float MasterVolume { get; private set; }
        public float SfxVolume    { get; private set; }
        public float MusicVolume  { get; private set; }

        [Preserve]
        public AudioSettingsService(AudioSettingsData data)
        {
            this.data = data;
        }

        void IInitializable.Initialize()
        {
            this.MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, DefaultVolume);
            this.SfxVolume    = PlayerPrefs.GetFloat(SfxVolumeKey, DefaultVolume);
            this.MusicVolume  = PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);

            this.data.ApplyMasterVolume(this.MasterVolume);
            this.data.ApplySfxVolume(this.SfxVolume);
            this.data.ApplyMusicVolume(this.MusicVolume);
        }

        public void SetMasterVolume(float value01)
        {
            this.MasterVolume = value01;
            PlayerPrefs.SetFloat(MasterVolumeKey, value01);
            this.data.ApplyMasterVolume(value01);
        }

        public void SetSfxVolume(float value01)
        {
            this.SfxVolume = value01;
            PlayerPrefs.SetFloat(SfxVolumeKey, value01);
            this.data.ApplySfxVolume(value01);
        }

        public void SetMusicVolume(float value01)
        {
            this.MusicVolume = value01;
            PlayerPrefs.SetFloat(MusicVolumeKey, value01);
            this.data.ApplyMusicVolume(value01);
        }
    }
}
