using UnityEngine;
using UnityEngine.Video;

namespace HorrorEngine
{
    public class CinematicPlayer : MonoBehaviour
    {
        public void Play(VideoClip clip)
        {
            UIManager.Get<UICinematicPlayer>().Show(clip);
        }
    }
}