using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 관리 (단일 책임 - UI만 담당)
/// Observer로 이벤트 구독 → 자동으로 UI 갱신
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject waitingPanel;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject playingPanel;
    [SerializeField] private GameObject endPanel;

    [Header("Playing UI")]
    [SerializeField] private TextMeshProUGUI wordText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;
    //[SerializeField] private Image timerBar;

    [Header("End UI")]
    [SerializeField] private TextMeshProUGUI endScoreText;

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button nextWordButton;
    [SerializeField] private Button restartButton;

    private GameManager gameManager;
    private float maxTime;

    void Awake()
    {
        gameManager = GetComponent<GameManager>();
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        GameEventSystem.Subscribe("OnStateChanged", OnStateChanged);
        GameEventSystem.Subscribe("OnWordChanged", OnWordChanged);
        GameEventSystem.Subscribe("OnTimerUpdate", OnTimerUpdate);
        GameEventSystem.Subscribe("OnScoreChanged", OnScoreChanged);
        GameEventSystem.Subscribe("OnGameEnd", OnGameEnd);
    }

    void OnDestroy()
    {
        GameEventSystem.Unsubscribe("OnStateChanged", OnStateChanged);
        GameEventSystem.Unsubscribe("OnWordChanged", OnWordChanged);
        GameEventSystem.Unsubscribe("OnTimerUpdate", OnTimerUpdate);
        GameEventSystem.Unsubscribe("OnScoreChanged", OnScoreChanged);
        GameEventSystem.Unsubscribe("OnGameEnd", OnGameEnd);
    }

    void Start()
    {
        startButton.onClick.AddListener(OnStartClick);
        nextWordButton.onClick.AddListener(OnNextWordClick);
        restartButton.onClick.AddListener(OnRestartClick);

        ShowPanel("Waiting");
    }

    private void ShowPanel(string state)
    {
        waitingPanel.SetActive(state == "Waiting");
        loadingPanel.SetActive(state == "Loading");
        playingPanel.SetActive(state == "Playing");
        endPanel.SetActive(state == "End");
    }

    private void OnStateChanged(object data)
    {
        string state = (string)data;
        ShowPanel(state);
        Debug.Log($"[UIManager] Panel: {state}");
    }

    private void OnWordChanged(object data)
    {
        wordText.text = (string)data;
    }

    private void OnTimerUpdate(object data)
    {
        float timeLeft = (float)data;
        timerText.text = timeLeft.ToString("F1") + "s";

        if (maxTime > 0)
            //timerBar.fillAmount = timeLeft / maxTime;

        timerText.color = timeLeft <= 10f ? Color.red : Color.white;
        //timerBar.color = timeLeft <= 10f ? Color.red : Color.green;
    }

    private void OnScoreChanged(object data)
    {
        int score = (int)data;
        scoreText.text = "Score: " + score;
    }

    private void OnGameEnd(object data)
    {
        int score = (int)data;
        endScoreText.text = "Final Score: " + score;
    }

    private void OnStartClick()
    {
        maxTime = 30f;
        gameManager.ChangeState(new LoadingState(gameManager));
    }

    private void OnNextWordClick()
    {
        gameManager.NextWord();
        gameManager.StartPlaying();
    }

    private void OnRestartClick()
    {
        gameManager.RestartGame();
    }
}