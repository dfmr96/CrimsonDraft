using System.Collections;
using UnityEngine;

namespace HorrorEngine
{
    public class DoorAnimation : MonoBehaviour
    {
        public AudioClip Sound;

        private bool m_Finished;

        // --------------------------------------------------------------------

        public IEnumerator Play()
        {
            m_Finished = false;

            while (!m_Finished)
                yield return null;
        }

        // --------------------------------------------------------------------

        public void AnimEvent_Finish()
        {
            m_Finished = true;
        }
    }
}