using CrimsonDraft.Audio;
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

    [Header("Tension1")]
    [SerializeField] private float lowTime1Threshold = 10f;
    [Header("Tension2")]
    [SerializeField] private float lowTime2Threshold = 10f;

    private float currentTime;
    private bool timerRunning;
    private bool gameFinished;
    private bool lowTime1Triggered;    
    private bool lowTime2Triggered;
    private TimerAudio audio;

    private void Awake()
    {
        Instance = this;
        TryGetComponent(out audio);
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

        if (!lowTime1Triggered && currentTime <= lowTime1Threshold)
        {
            audio.PlayLowTime1();
            lowTime1Triggered = true;
       
            
        }
        else if(!lowTime2Triggered && currentTime <= lowTime2Threshold)
        {
            audio.PlayLowTime2();
            lowTime2Triggered = true;
            
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

        if (audio != null) audio.PlayDefeat();

        SceneManager.LoadScene(defeatSceneName);
    }
}