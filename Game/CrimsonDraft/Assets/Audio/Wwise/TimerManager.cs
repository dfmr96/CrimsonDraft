using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TimerManager : MonoBehaviour
{
    public static TimerManager Instance;

    [Header("Timer")]
    [SerializeField] private float startTime = 120f;
    [SerializeField] private TMP_Text timerText;

    [Header("Scenes")]
    [SerializeField] private string defeatSceneName = "DefeatScene";

    private float currentTime;
    private bool timerRunning;
    private bool gameFinished;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentTime = startTime;
        timerRunning = false;
        UpdateTimerUI();
    }

    private void Update()
    {
        if (!timerRunning || gameFinished)
            return;

        currentTime -= Time.deltaTime;

        if (currentTime <= 0f)
        {
            currentTime = 0f;
            UpdateTimerUI();
            Defeat();
            return;
        }

        UpdateTimerUI();
    }

    public void StartTimer()
    {
        timerRunning = true;
    }

    private void UpdateTimerUI()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);

        if (timerText != null)
        {
            timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void Defeat()
    {
        gameFinished = true;
        timerRunning = false;
        Time.timeScale = 1f;

        SceneManager.LoadScene(defeatSceneName);
    }
}