using CrimsonDraft.Audio;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialUI : MonoBehaviour
{
    private TutorialAudio audio;

    private void Awake()
    {
        TryGetComponent(out audio);
    }

    private void OnEnable()
    {
        Time.timeScale = 0f;
        if (audio != null) audio.PlayOpen();
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

        if (audio != null) audio.PlayClose();

        TimerManager.Instance.StartTimer();

        gameObject.SetActive(false);
    }
}