using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [SerializeField] private TMP_Text keysText;
    [SerializeField] private string victorySceneName = "VictoryScene";

    private int collectedKeys;
    private int totalKeys;
    private bool gameFinished;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        totalKeys = FindObjectsByType<PickupPoints>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        ).Length;

        UpdateUI();
    }

    public void CollectKey()
    {
        if (gameFinished)
            return;

        collectedKeys++;
        UpdateUI();

        if (collectedKeys >= totalKeys)
        {
            Victory();
        }
    }

    private void UpdateUI()
    {
        if (keysText != null)
        {
            keysText.text =
                $"Llaves encontradas: {collectedKeys}/{totalKeys}";
        }
    }

    private void Victory()
    {
        gameFinished = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(victorySceneName);
    }
}
