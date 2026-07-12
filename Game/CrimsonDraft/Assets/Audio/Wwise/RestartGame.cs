using CrimsonDraft.Audio;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RestartGame : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "GameScene";

    private RestartAudio audio;

    private void Awake()
    {
        TryGetComponent(out audio);
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Restart();
        }
    }

    private void Restart()
    {
        Time.timeScale = 1f;

        if (audio != null) audio.PlayRestart();

        SceneManager.LoadScene(gameSceneName);
    }
}