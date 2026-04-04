using UnityEngine;

namespace HorrorEngine
{
    public class CutsceneUtils : MonoBehaviour
    {
        public void PauseGame()
        {
            PauseController.Instance.Pause(this);
        }

        public void ResumeGame()
        {
            PauseController.Instance.Resume(this);
        }

        public void SetPlayerEnabled(bool enabled)
        {
            if (!enabled)
                GameManager.Instance.Player.Disable(this);
            else
                GameManager.Instance.Player.Enable(this);
        }

        public void SetPlayerVisible(bool visible)
        {
            GameManager.Instance.Player.SetVisible(visible);
        }

        public void SetPlayerAt(Transform point)
        {
            GameManager.Instance.Player.PlaceAt(point);
        }

        public void ImmediateFadeTo(float fadeVal)
        {
            UIFade fade = UIManager.Get<UIFade>();
            fade.Fade(fadeVal, fadeVal, 0f);
        }

        public void ImmediateLetterBox(bool visible)
        {
            UILetterBox letterBox = UIManager.Get<UILetterBox>();
            letterBox.SetVisible(visible, 0f);
        }
    }
}