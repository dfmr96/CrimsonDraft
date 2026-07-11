using UnityEngine;
using UnityEngine.InputSystem;


public class TutorialUI : MonoBehaviour
{
    private void OnEnable()
    {
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CloseTutorial();
        }
    }

    private void CloseTutorial()
    {
        Time.timeScale = 1f;

        TimerManager.Instance.StartTimer();

        gameObject.SetActive(false);
    }
}