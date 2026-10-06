using UnityEngine;

namespace HorrorEngine
{
    [System.Serializable]
    public class SoundCue
    {
        [SerializeField] public AudioClip m_Clip;
        
        public float MinVolume = 1f;
        public float MaxVolume = 1f;
        public float MinPitch = 1f;
        public float MaxPitch = 1f;

        public bool IsValid()
        {
            return m_Clip != null;
        }

        public AudioClip GetClip(out float volume, out float pitch)
        {
            volume = Random.Range(MinVolume, MaxVolume);
            pitch = Random.Range(MinPitch, MaxPitch);
            return m_Clip;
        }
    }
}