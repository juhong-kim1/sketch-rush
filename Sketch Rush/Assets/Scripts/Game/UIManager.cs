using UnityEngine;
using System.Collections;

using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI 관리 (단일 책임 - UI만 담당)
/// Observer로 이벤트 구독 → 자동으로 UI 갱신
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private GameObject drawingPanel;
    [SerializeField] private GameObject quizPanel;
    [SerializeField] private GameObject endPanel;

    [Header("Drawing UI")]
    [SerializeField] private TextMeshProUGUI wordText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;

    [Header("Quiz UI")]
    [SerializeField] private RawImage quizImage;
    [SerializeField] private TMP_InputField quizInput;
    [SerializeField] private TextMeshProUGUI quizTimerText;
    [SerializeField] private TextMeshProUGUI quizFeedbackText;
    [SerializeField] private TextMeshProUGUI quizProgressText;
    [SerializeField] private Button submitButton;
    [SerializeField] private TextMeshProUGUI quizTurnText;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private TextMeshProUGUI feedbackText;


    [Header("End UI")]
    [SerializeField] private TextMeshProUGUI endScoreText;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;

    private GameManager gameManager;

    void Awake()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        GameEventSystem.Subscribe("OnStateChanged", OnStateChanged);
        GameEventSystem.Subscribe("OnWordChanged", OnWordChanged);
        GameEventSystem.Subscribe("OnTimerUpdate", OnTimerUpdate);
        GameEventSystem.Subscribe("OnScoreChanged", OnScoreChanged);
        GameEventSystem.Subscribe("OnGameEnd", OnGameEnd);
        GameEventSystem.Subscribe("OnQuizLoaded", OnQuizLoaded);
        GameEventSystem.Subscribe("OnQuizFeedback", OnQuizFeedback);
        GameEventSystem.Subscribe("OnQuizFeedbackClear", OnQuizFeedbackClear);
        GameEventSystem.Subscribe("OnQuizProgress", OnQuizProgress);
        GameEventSystem.Subscribe("OnQuizTurn", OnQuizTurn);
        GameEventSystem.Subscribe("OnQuizResult", OnQuizResult);
    }

    void OnDestroy()
    {
        GameEventSystem.Unsubscribe("OnStateChanged", OnStateChanged);
        GameEventSystem.Unsubscribe("OnWordChanged", OnWordChanged);
        GameEventSystem.Unsubscribe("OnTimerUpdate", OnTimerUpdate);
        GameEventSystem.Unsubscribe("OnScoreChanged", OnScoreChanged);
        GameEventSystem.Unsubscribe("OnGameEnd", OnGameEnd);
        GameEventSystem.Unsubscribe("OnQuizLoaded", OnQuizLoaded);
        GameEventSystem.Unsubscribe("OnQuizFeedback", OnQuizFeedback);
        GameEventSystem.Unsubscribe("OnQuizFeedbackClear", OnQuizFeedbackClear);
        GameEventSystem.Unsubscribe("OnQuizProgress", OnQuizProgress);
        GameEventSystem.Unsubscribe("OnQuizTurn", OnQuizTurn);
        GameEventSystem.Unsubscribe("OnQuizResult", OnQuizResult);
    }

void Start()
    {
        submitButton.onClick.AddListener(OnSubmitClick);
        restartButton.onClick.AddListener(OnReturnToRoomClick);
    }

    // ===== Panel 전환 =====
private void ShowPanel(string state)
    {
        loadingPanel.SetActive(state == "Loading");
        drawingPanel.SetActive(state == "Drawing");
        quizPanel.SetActive(state == "Quiz");
        endPanel.SetActive(state == "End");
    }

    // ===== Observer 핸들러 =====
    private void OnStateChanged(object data)
    {
        string state = (string)data;
        ShowPanel(state);

        if (state == "Quiz" && quizInput != null)
        {
            quizInput.text = "";
            quizInput.ActivateInputField();
        }

        Debug.Log($"[UIManager] Panel: {state}");
    }

    private void OnWordChanged(object data)
    {
        wordText.text = (string)data;
    }

    private void OnTimerUpdate(object data)
    {
        float timeLeft = (float)data;

        if (drawingPanel.activeSelf)
        {
            timerText.text = timeLeft.ToString("F1") + "s";
            timerText.color = timeLeft <= 3f ? Color.red : Color.white;
        }
        else if (quizPanel.activeSelf)
        {
            quizTimerText.text = timeLeft.ToString("F1") + "s";
            quizTimerText.color = timeLeft <= 3f ? Color.red : Color.white;
        }
    }

    private void OnQuizTurn(object data)
    {
        string turnText = (string)data;
        quizTurnText.text = turnText;
        Debug.Log($"[UIManager] Turn: {turnText}");
    }

    private void OnQuizResult(object data)
    {
        string resultText = (string)data;
        quizFeedbackText.text = resultText;
        quizFeedbackText.color = Color.cyan;
        Debug.Log($"[UIManager] Result: {resultText}");
    }

    private void OnScoreChanged(object data)
    {
        scoreText.text = "Score: " + (int)data;
    }

    private void OnGameEnd(object data)
    {
        string leaderboard = (string)data;
        endScoreText.text = leaderboard;
    }

    // ===== Quiz 이벤트 =====
    private void OnQuizLoaded(object data)
    {
        Texture2D tex = (Texture2D)data;
        quizImage.texture = tex;
        quizInput.text = "";
        quizInput.ActivateInputField();
        quizFeedbackText.text = "";
    }

private void OnQuizFeedback(object data)
    {
        string feedback = (string)data;
        if (feedback == "Correct")
        {
            quizFeedbackText.text = "\u2713 Correct!";
            quizFeedbackText.color = Color.green;
            quizInput.DeactivateInputField();
        }
        else if (feedback == "Wrong")
        {
            quizFeedbackText.text = "\u2717 Wrong!";
            quizFeedbackText.color = Color.red;
        }
        else if (feedback.StartsWith("TimeOut:"))
        {
            string answer = feedback.Substring(8); // "TimeOut:" 이후
            quizFeedbackText.text = "\u23f0 Time Out! \n정답: " + answer;
            quizFeedbackText.color = Color.yellow;
            quizInput.DeactivateInputField();
        }
    }

    private void OnQuizFeedbackClear(object data)
    {
        quizFeedbackText.text = "";
        quizInput.text = "";
        quizInput.ActivateInputField();
    }

    private void OnQuizProgress(object data)
    {
        quizProgressText.text = (string)data;
    }

    // ===== 버튼 클릭 =====


    private void OnSubmitClick()
    {
        if (quizInput.text.Length > 0)
            gameManager.CheckAnswer(quizInput.text);
    }

    private void OnReturnToRoomClick()
    {
        gameManager.ReturnToRoom();
    }


public void ShowPhaseInfo(string phaseTitle, string phaseDescription)
    {
        if (phaseText != null)
        {
            phaseText.text = $"{phaseTitle}\n{phaseDescription}";
        }
        Debug.Log($"[UIManager] ShowPhaseInfo: {phaseTitle} - {phaseDescription}");
    }


public void ShowFeedback(string message, bool isCorrect)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = isCorrect ? Color.green : Color.red;
            StartCoroutine(HideFeedbackAfterDelay(2f));
        }
        Debug.Log($"[UIManager] ShowFeedback: {message} (Correct: {isCorrect})");
    }

    private IEnumerator HideFeedbackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (feedbackText != null)
        {
            feedbackText.text = "";
        }
    }
}
