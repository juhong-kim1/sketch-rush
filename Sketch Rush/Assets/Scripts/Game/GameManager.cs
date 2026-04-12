using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class GameManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private AIWordGenerator wordGenerator;
    [SerializeField] private DrawingCanvas drawingCanvas;
    [SerializeField] private GameNetworkManager networkManager;
    [SerializeField] private VoiceRecognizer voiceRecognizer;

    [Header("Game Settings")]
    private float drawingTime = 5f;
    [SerializeField] private float quizTime = 10f;

    // Phase System
    public enum QuizPhase
    {
        TargetTurn,  // Phase 1: 타겟의 턴
        OthersTurn   // Phase 2: 다른 사람들의 턴
    }
    private QuizPhase currentPhase = QuizPhase.TargetTurn;
    private float phaseStartTime = 0f;


    private GameState currentState;
    public bool IsActive { get; private set; }

    private Queue<string> wordQueue = new Queue<string>();
    private string currentWord;
    private float timeLeft;
    private Dictionary<string, Texture2D> drawnImages = new Dictionary<string, Texture2D>();

    private List<string> quizWordList = new List<string>(); // 20개 단어 중 랜덤 선택용
    private int currentQuizRound = 0;
    private int currentTargetActorNumber = -1;
    private string currentQuizAnswer;
    private bool isMyTurn = false;
    private bool canAnswer = false; // 지목된 사람 틀린 후 다른 사람들 활성화

    public float TimeLeft => timeLeft;
    public string CurrentWord => currentWord;
    public string CurrentQuizAnswer => currentQuizAnswer;

    void Awake()
    {
        if (networkManager == null)
            networkManager = FindAnyObjectByType<GameNetworkManager>();

        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnRecognized += OnVoiceRecognized;
            voiceRecognizer.OnError += OnVoiceError;
        }
    }

    private void OnDestroy()
    {
        if (voiceRecognizer != null)
        {
            voiceRecognizer.OnRecognized -= OnVoiceRecognized;
            voiceRecognizer.OnError -= OnVoiceError;
        }
    }

void Start()
    {
        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("DrawTime"))
        {
            drawingTime = (float)PhotonNetwork.CurrentRoom.CustomProperties["DrawTime"];
            Debug.Log($"[GameManager] DrawTime set to: {drawingTime}s");
        }

        if (PhotonNetwork.IsMasterClient)
        {
            ChangeState(new LoadingState(this));
        }
        else
        {
            GameEventSystem.Publish("OnStateChanged", "Loading");
        }
    }

    void Update()
    {
        currentState?.Update();
    }

    public void ChangeState(GameState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    // ===== Loading =====
    public void StartWordLoading()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // 호스트만 AI 호출
            networkManager.StartWordGeneration();
        }
    }

    // RPC로 받은 단어 처리
    public void ReceiveWords(string[] words)
    {
        wordQueue.Clear();
        drawnImages.Clear();
        quizWordList.Clear();

        foreach (string word in words)
        {
            wordQueue.Enqueue(word);
            quizWordList.Add(word);
        }

        Debug.Log($"[GameManager] Received {wordQueue.Count} words");
        
        // Drawing 상태로 전환
        if (PhotonNetwork.IsMasterClient)
        {
            networkManager.SyncStateChange("Drawing");
        }
        
        ChangeState(new DrawingState(this));
    }

    // ===== Drawing =====
    public void StartDrawing()
    {
        IsActive = true;
        NextDrawingWord();
        timeLeft = drawingTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StartTickTock();
        }
    }

    public void UpdateDrawing()
    {
        if (!IsActive) return;
        timeLeft -= Time.deltaTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);
        
        if (timeLeft <= 0)
        {
            SaveCurrentDrawing();
            
            if (wordQueue.Count > 0)
            {
                NextDrawingWord();
                timeLeft = drawingTime;
            }
            else
            {
                IsActive = false;
                
                // Drawing 완료 → Quiz로
                if (PhotonNetwork.IsMasterClient)
                {
                    networkManager.SyncStateChange("Quiz");
                    networkManager.StartQuiz();
                }
                
                ChangeState(new QuizState(this));
            }
        }
    }

    private void NextDrawingWord()
    {
        if (wordQueue.Count > 0)
        {
            currentWord = wordQueue.Dequeue();
            GameEventSystem.Publish("OnWordChanged", currentWord);
            if (drawingCanvas != null) drawingCanvas.ClearCanvas();

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayNextDrawingWord();
            }

            Debug.Log($"[GameManager] Drawing: {currentWord}");
        }
    }

    private void SaveCurrentDrawing()
    {
        if (drawingCanvas == null || currentWord == null) return;

        Texture2D original = drawingCanvas.GetTexture();
        Texture2D copy = new Texture2D(original.width, original.height);
        copy.SetPixels(original.GetPixels());
        copy.Apply();
        drawnImages[currentWord] = copy;

        byte[] pngData = drawingCanvas.GetPNG();
        networkManager.SavePlayerDrawing(currentWord, pngData);

        Debug.Log($"[GameManager] Saved: {currentWord}");
    }

    // ===== Quiz =====
    public void StartQuiz()
    {
        // NetworkManager가 호출함
        Debug.Log("[GameManager] Quiz Phase Started");
    }

public void StartQuizRound(int roundIndex, int targetActorNumber, bool myTurn, string quizWord)
    {
        currentQuizRound = roundIndex;
        currentTargetActorNumber = targetActorNumber;
        isMyTurn = myTurn;
        canAnswer = myTurn;
        IsActive = true;
        
        currentPhase = QuizPhase.TargetTurn;
        phaseStartTime = Time.time;

        currentQuizAnswer = quizWord;

        if (drawnImages.ContainsKey(currentQuizAnswer))
        {
            GameEventSystem.Publish("OnQuizLoaded", drawnImages[currentQuizAnswer]);
        }

        int totalRounds = PhotonNetwork.CurrentRoom.PlayerCount * 3;
        GameEventSystem.Publish("OnQuizProgress", $"{totalRounds} 라운드 중 {currentQuizRound + 1} 라운드");
        
        if (isMyTurn)
        {
            GameEventSystem.Publish("OnQuizTurn", "내 차례!");
        }
        else
        {
            string targetName = GetPlayerName(targetActorNumber);
            GameEventSystem.Publish("OnQuizTurn", $"{targetName}이 정답 맞추는 중..");
        }

        timeLeft = quizTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StartTickTock();
        }
    }

public void UpdateQuiz()
    {
        if (!IsActive) return;

        timeLeft -= Time.deltaTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);

        if (timeLeft <= 0)
        {
            VoiceRecognizer voiceRecognizer = FindAnyObjectByType<VoiceRecognizer>();
            if (voiceRecognizer != null && voiceRecognizer.IsProcessing)
            {
                Debug.Log("[GameManager] Waiting for voice recognition...");
                timeLeft = 0;
                GameEventSystem.Publish("OnTimerUpdate", 0);
                return;
            }

            timeLeft = 0;
            GameEventSystem.Publish("OnTimerUpdate", 0);
        }
    }

public void StartPhase2(int targetActorNumber)
    {
        Debug.Log("[GameManager] Starting Phase 2 - Others' Turn");
        currentPhase = QuizPhase.OthersTurn;
        currentTargetActorNumber = targetActorNumber;
        phaseStartTime = Time.time;
        timeLeft = quizTime; // 10초 리셋
        IsActive = true; // Phase 2 재활성화
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);

        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        bool isTarget = myActorNumber == currentTargetActorNumber;

        if (isTarget)
        {
            // 타겟: 입력 불가
            canAnswer = false;
            GameEventSystem.Publish("OnQuizTurn", "오답! 다른 사람들이 입력 중입니다.");
        }
        else
        {
            // 다른 사람: 입력 가능
            string targetName = GetPlayerName(targetActorNumber);
            canAnswer = true;
            GameEventSystem.Publish("OnQuizTurn", $"{targetName} 오답! 먼저 입력한 사람이 점수를 얻습니다.");
        }
    }

public QuizPhase GetCurrentPhase()
    {
        return currentPhase;
    }



    // 정답 제출
public void CheckAnswer(string playerAnswer)
    {
        if (!IsActive) return;
        if (!canAnswer) return;

        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        bool isTarget = myActorNumber == currentTargetActorNumber;

        // Phase에 따른 제출 권한 체크
        if (currentPhase == QuizPhase.TargetTurn && !isTarget)
        {
            Debug.Log("[GameManager] Cannot submit - Phase 1 is target's turn only");
            return;
        }

        if (currentPhase == QuizPhase.OthersTurn && isTarget)
        {
            Debug.Log("[GameManager] Cannot submit - Phase 2 is others' turn only");
            return;
        }

        // 제출
        networkManager.SubmitAnswer(playerAnswer);
    }

    // 지목된 사람이 틀림 → 다른 사람들 활성화


    // 퀴즈 결과 표시
    public void ShowQuizResult(int targetActorNumber, string correctAnswer, bool wasCorrect, int winnerActorNumber, int points)
    {
        IsActive = false;
        canAnswer = false;

        // 결과 UI 표시
        string resultText = "";
        
        if (wasCorrect)
        {
            if (winnerActorNumber == targetActorNumber)
            {
                resultText = $"{GetPlayerName(targetActorNumber)} got it! +{points}pt";
            }
            else
            {
                resultText = $"{GetPlayerName(winnerActorNumber)} stole it! +{points}pt";
            }
        }
        else
        {
            resultText = $"Time Out! Answer: {correctAnswer}";
        }

        GameEventSystem.Publish("OnQuizResult", resultText);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopTickTock();
        }
    }

    // ===== End =====
    public void EndGame(List<PlayerData> sortedPlayers)
    {
        IsActive = false;
        ChangeState(new EndState(this));
        
        // 최종 점수 UI 표시
        string leaderboard = "Final Scores:\n";
        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            leaderboard += $"{i + 1}. {sortedPlayers[i].nickname}: {sortedPlayers[i].score}pt\n";
        }
        
        GameEventSystem.Publish("OnGameEnd", leaderboard);
        Debug.Log($"[GameManager] {leaderboard}");
    }

    public void ReturnToRoom()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            networkManager.CancelInvoke(nameof(networkManager.AutoReturnToLobby));

            PhotonNetwork.LoadLevel("LobbyScene");
        }

        StartCoroutine(CountdownToLobby());
    }

    private string GetPlayerName(int actorNumber)
    {
        var playerData = networkManager.GetPlayerData(actorNumber);
        return playerData != null ? playerData.nickname : $"Player{actorNumber}";
    }

    IEnumerator CountdownToLobby()
    {
        for (int i = 10; i > 0; i--)
        {
            // TODO: UI에 카운트다운 텍스트 표시
            // countdownText.text = $"Returning in {i}...";
            yield return new WaitForSeconds(1f);
        }
    }

    void OnVoiceRecognized(string recognizedText)
    {
        Debug.Log($"[GameManager] Voice recognized: {recognizedText}");

        if (currentState is QuizState)
        {
            CheckAnswer(recognizedText);
        }
    }

    void OnVoiceError(string errorMessage)
    {
        Debug.LogWarning($"[GameManager] Voice error: {errorMessage}");
        GameEventSystem.Publish("OnVoiceError", errorMessage);
    }
}

[System.Serializable]
public class RoundResultData
{
    public string message;
    public string targetName;
    public string winnerName;
    public byte[] targetDrawing;
    public byte[] winnerDrawing;
}