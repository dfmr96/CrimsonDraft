using UnityEngine;

namespace HorrorEngine
{
    [RequireComponent(typeof(AudioStack))]
    public class AmbientManager : SingletonBehaviour<AmbientManager>, IAudioManager
    {
        private AudioStack m_Stack;
        
        protected override void Awake()
        {
            base.Awake();
            
            m_Stack = GetComponent<AudioStack>();
        }


        public AudioStack GetStack()
        {
            return m_Stack;
        }
    }
}