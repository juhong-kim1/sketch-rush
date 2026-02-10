using Photon.Pun;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Button voiceMicButton;
    [SerializeField] private TextMeshProUGUI quizTurnText;
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("Result UI")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TextMeshProUGUI resultText;
    [SerializeField] private RawImage targetDrawingImage;
    [SerializeField] private RawImage winnerDrawingImage;

    [Header("End UI")]
    [SerializeField] private TextMeshProUGUI endScoreText;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;

    private GameManager gameManager;
    private VoiceRecognizer voiceRecognizer;
    private bool isVoiceRecording = false;

    void Awake()
    {
        gameManager = FindAnyObjectByType<GameManager>();
        voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();

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
        GameEventSystem.Subscribe("OnRoundResult", OnRoundResult);
        GameEventSystem.Subscribe("OnVoiceError", OnVoiceError);
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
        GameEventSystem.Unsubscribe("OnRoundResult", OnRoundResult);
        GameEventSystem.Unsubscribe("OnVoiceError", OnVoiceError);
    }

void Start()
    {
        submitButton.onClick.AddListener(OnSubmitClick);
        restartButton.onClick.AddListener(OnReturnToRoomClick);

        var trigger = voiceMicButton.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        // 누를 때
        var pointerDown = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerDown.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
        pointerDown.callback.AddListener((data) => { OnVoiceButtonDown(); });
        trigger.triggers.Add(pointerDown);

        // 뗄 때
        var pointerUp = new UnityEngine.EventSystems.EventTrigger.Entry();
        pointerUp.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
        pointerUp.callback.AddListener((data) => { OnVoiceButtonUp(); });
        trigger.triggers.Add(pointerUp);
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
        wordText.text = $"{(string)data} 을(를) 그려주세요!";
    }

    private void OnTimerUpdate(object data)
    {
        float time = Convert.ToSingle(data);

        int seconds = Mathf.CeilToInt(time);
        if (timerText != null && timerText.gameObject.activeInHierarchy)
        {
            timerText.text = $"{seconds} 초 뒤 다음단어";
        }
        if (quizTimerText != null && quizTimerText.gameObject.activeInHierarchy)
        {
            quizTimerText.text = $"Time: {seconds}s";
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

        // 호스트만 버튼 활성화
        if (restartButton != null)
        {
            restartButton.interactable = PhotonNetwork.IsMasterClient;

            var buttonText = restartButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = PhotonNetwork.IsMasterClient ?
                    "방으로" :
                    "방으로";
            }
        }

        // 카운트다운 시작
        StartCoroutine(CountdownToLobby());
    }

    IEnumerator CountdownToLobby()
    {
        for (int i = 10; i > 0; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = $"{i}초 후 방으로...";

                // 3초 이하면 빨간색
                if (i <= 3)
                    countdownText.color = Color.red;
                else
                    countdownText.color = Color.white;
            }

            yield return new WaitForSeconds(1f);
        }

        // 카운트다운 끝
        if (countdownText != null)
        {
            countdownText.text = "방으로...";
        }
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

    private void OnRoundResult(object data)
    {
        RoundResultData resultData = (RoundResultData)data;

        // Result Panel 활성화
        resultPanel.SetActive(true);

        // 결과 텍스트
        resultText.text = resultData.message;

        // 타겟 그림 표시
        if (resultData.targetDrawing != null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(resultData.targetDrawing);
            targetDrawingImage.texture = tex;
            targetDrawingImage.gameObject.SetActive(true);
        }

        if (resultData.winnerDrawing != null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(resultData.winnerDrawing);
            winnerDrawingImage.texture = tex;
            winnerDrawingImage.gameObject.SetActive(true);
        }
        else
        {
            winnerDrawingImage.gameObject.SetActive(false);
        }

        StartCoroutine(HideResultPanelAfterDelay(5f));
    }

    IEnumerator HideResultPanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        resultPanel.SetActive(false);
    }

    void OnVoiceButtonDown()
    {
        if (voiceRecognizer != null && !isVoiceRecording)
        {
            isVoiceRecording = true;
            voiceRecognizer.StartRecording();

            // UI 피드백
            if (voiceMicButton != null)
            {
                voiceMicButton.GetComponent<UnityEngine.UI.Image>().color = Color.red;
            }

            Debug.Log("[UIManager] Voice recording started");
        }
    }

    void OnVoiceButtonUp()
    {
        if (voiceRecognizer != null && isVoiceRecording)
        {
            isVoiceRecording = false;
            voiceRecognizer.StopRecordingAndRecognize();

            // UI 피드백
            if (voiceMicButton != null)
            {
                voiceMicButton.GetComponent<UnityEngine.UI.Image>().color = Color.white;
            }

            Debug.Log("[UIManager] Voice recording stopped");
        }
    }

    void OnVoiceError(object data)
    {
        string errorMessage = (string)data;

        if (quizFeedbackText != null)
        {
            quizFeedbackText.text = errorMessage;
            quizFeedbackText.color = Color.yellow;
        }
    }
}
