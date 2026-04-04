using UnityEngine;

namespace HorrorEngine
{
    public interface IAudioManager
    {
        public AudioStack GetStack();
    }
    
    [RequireComponent(typeof(AudioStack))]
    public class MusicManager : SingletonBehaviour<MusicManager>, IAudioManager
    {
        private AudioStack Stack;
        
        protected override void Awake()
        {
            base.Awake();
            
            Stack = GetComponent<AudioStack>();
        }

        public AudioStack GetStack()
        {
            return Stack;
        }
    }
}