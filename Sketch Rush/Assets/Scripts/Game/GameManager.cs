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

    [Header("Game Settings")]
    [SerializeField] private float drawingTime = 5f;
    [SerializeField] private float quizTime = 10f;
    [SerializeField] private int totalQuizRounds = 3;

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
    }

    void Start()
    {
        // 멀티플레이에서는 Waiting 상태 스킵 (로비에서 시작했으니)
        if (PhotonNetwork.IsMasterClient)
        {
            ChangeState(new LoadingState(this));
        }
        else
        {
            // 클라이언트는 대기
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
        
        Debug.Log($"[GameManager] Saved: {currentWord}");
    }

    // ===== Quiz =====
    public void StartQuiz()
    {
        // NetworkManager가 호출함
        Debug.Log("[GameManager] Quiz Phase Started");
    }

    public void StartQuizRound(int roundIndex, int targetActorNumber, bool myTurn)
    {
        currentQuizRound = roundIndex;
        currentTargetActorNumber = targetActorNumber;
        isMyTurn = myTurn;
        canAnswer = myTurn; // 처음엔 지목된 사람만
        IsActive = true;

        // 랜덤으로 단어 선택
        if (quizWordList.Count > 0)
        {
            int randomIndex = Random.Range(0, quizWordList.Count);
            currentQuizAnswer = quizWordList[randomIndex];
            quizWordList.RemoveAt(randomIndex); // 중복 방지
        }

        // 내 그림 표시
        if (drawnImages.ContainsKey(currentQuizAnswer))
        {
            GameEventSystem.Publish("OnQuizLoaded", drawnImages[currentQuizAnswer]);
        }

        // UI 갱신
        GameEventSystem.Publish("OnQuizProgress", $"Round {currentQuizRound + 1}/{totalQuizRounds}");
        
        if (isMyTurn)
        {
            GameEventSystem.Publish("OnQuizTurn", "Your Turn!");
        }
        else
        {
            string targetName = GetPlayerName(targetActorNumber);
            GameEventSystem.Publish("OnQuizTurn", $"{targetName}'s Turn");
        }

        timeLeft = quizTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);
    }

    public void UpdateQuiz()
    {
        if (!IsActive) return;
        
        timeLeft -= Time.deltaTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);
    }

    // 정답 제출
    public void CheckAnswer(string playerAnswer)
    {
        if (!IsActive) return;
        if (!canAnswer) return; // 답변 불가 상태면 무시

        networkManager.SubmitAnswer(playerAnswer);
    }

    // 지목된 사람이 틀림 → 다른 사람들 활성화
    public void OnTargetWrong()
    {
        if (!isMyTurn)
        {
            canAnswer = true;
            GameEventSystem.Publish("OnQuizTurn", "Answer Now!");
        }
    }

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

        // 타겟의 그림 공개 (TODO: PNG 전송)
        // 지금은 일단 자기 그림만 보임
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

    public void RestartGame()
    {
        // 로비로 돌아가기
        PhotonNetwork.LoadLevel("LobbyScene");
    }

    private string GetPlayerName(int actorNumber)
    {
        var playerData = networkManager.GetPlayerData(actorNumber);
        return playerData != null ? playerData.nickname : $"Player{actorNumber}";
    }
}