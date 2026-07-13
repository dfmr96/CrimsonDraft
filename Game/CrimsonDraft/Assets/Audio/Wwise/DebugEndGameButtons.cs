using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class DebugEndGameButtons : MonoBehaviour
{
    [SerializeField] private string victorySceneName = "VictoryScene";
    [SerializeField] private string defeatSceneName = "DefeatScene";

    [SerializeField] private InputActionReference skipToVictoryAction = null!;
    [SerializeField] private InputActionReference skipToDefeatAction = null!;

    private void OnEnable()
    {
        skipToVictoryAction.action.performed += OnSkipToVictory;
        skipToDefeatAction.action.performed += OnSkipToDefeat;
    }

    private void OnDisable()
    {
        skipToVictoryAction.action.performed -= OnSkipToVictory;
        skipToDefeatAction.action.performed -= OnSkipToDefeat;
    }

    private void OnSkipToVictory(InputAction.CallbackContext context) => GoToVictory();
    private void OnSkipToDefeat(InputAction.CallbackContext context) => GoToDefeat();

    public void GoToVictory()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(victorySceneName);
    }

    public void GoToDefeat()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(defeatSceneName);
    }
}
