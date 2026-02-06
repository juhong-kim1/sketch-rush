using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

public class GameNetworkManager : MonoBehaviourPunCallbacks
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private AIWordGenerator wordGenerator;

    // === 플레이어 데이터 ===
    private Dictionary<int, PlayerData> playerDataDict = new Dictionary<int, PlayerData>();
    
    // === Quiz 관련 ===
    private List<int> playerOrder = new List<int>(); // 플레이어 순서
    private int currentTargetIndex = 0; // 현재 지목된 플레이어 인덱스
    private List<string> quizWordPool = new List<string>(); // 전체 단어 풀
    private bool isWaitingForAnswer = false;

    void Awake()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();
        if (wordGenerator == null)
            wordGenerator = FindAnyObjectByType<AIWordGenerator>();
    }

    void Start()
    {
        // 플레이어 데이터 초기화
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            playerDataDict[player.ActorNumber] = new PlayerData
            {
                actorNumber = player.ActorNumber,
                nickname = player.NickName,
                score = 0
            };
        }

        // 플레이어 순서 설정 (ActorNumber 순)
        playerOrder = PhotonNetwork.PlayerList
            .OrderBy(p => p.ActorNumber)
            .Select(p => p.ActorNumber)
            .ToList();

        Debug.Log($"[GameNetworkManager] Players: {string.Join(", ", playerOrder)}");
    }

    // ===== AI 단어 생성 (호스트만) =====
    public void StartWordGeneration()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[GameNetworkManager] Master: Starting word generation...");
            StartCoroutine(GenerateAndSyncWords());
        }
    }

    System.Collections.IEnumerator GenerateAndSyncWords()
    {
        // AI 단어 생성
        yield return StartCoroutine(wordGenerator.GenerateWords());

        if (wordGenerator.generatedWords != null && wordGenerator.generatedWords.Length > 0)
        {
            // 단어를 ,로 연결해서 RPC로 전송
            string wordsString = string.Join(",", wordGenerator.generatedWords);
            photonView.RPC("RPC_ReceiveWords", RpcTarget.All, wordsString);
            Debug.Log($"[GameNetworkManager] Sent words: {wordGenerator.generatedWords.Length}");
        }
        else
        {
            Debug.LogError("[GameNetworkManager] Word generation failed!");
        }
    }

    [PunRPC]
    void RPC_ReceiveWords(string wordsString)
    {
        string[] words = wordsString.Split(',');
        Debug.Log($"[GameNetworkManager] Received {words.Length} words");
        
        // 단어 풀 저장
        quizWordPool.Clear();
        quizWordPool.AddRange(words);
        
        // GameManager에 단어 전달
        gameManager.ReceiveWords(words);
    }

    // ===== 상태 동기화 =====
    public void SyncStateChange(string stateName)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            photonView.RPC("RPC_ChangeState", RpcTarget.All, stateName);
        }
    }

    [PunRPC]
    void RPC_ChangeState(string stateName)
    {
        Debug.Log($"[GameNetworkManager] State: {stateName}");
        GameEventSystem.Publish("OnStateChanged", stateName);
    }

    // ===== Quiz 시작 =====
public void StartQuiz()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            currentTargetIndex = 0;
            
            // 단어 선택
            string selectedWord = SelectRandomWord();
            photonView.RPC("RPC_StartQuizRound", RpcTarget.All, 0, selectedWord);
        }
    }

private string SelectRandomWord()
    {
        if (quizWordPool.Count == 0)
        {
            Debug.LogError("[GameNetworkManager] Word pool is empty!");
            return "ERROR";
        }
        
        int randomIndex = Random.Range(0, quizWordPool.Count);
        string selectedWord = quizWordPool[randomIndex];
        quizWordPool.RemoveAt(randomIndex); // 중복 방지
        
        return selectedWord;
    }

[PunRPC]
    void RPC_StartQuizRound(int roundIndex, string selectedWord)
    {
        // roundIndex를 그대로 사용 (플레이어 수와 무관하게)
        currentTargetIndex = roundIndex;
        int targetIndex = currentTargetIndex % playerOrder.Count;
        int targetActorNumber = playerOrder[targetIndex];
        
        Debug.Log($"[GameNetworkManager] Round {roundIndex + 1}: Target = {targetActorNumber} (index={currentTargetIndex}), Word = {selectedWord}");
        
        // GameManager에 퀴즈 시작 알림 (단어 포함)
        bool isMyTurn = targetActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        gameManager.StartQuizRound(roundIndex, targetActorNumber, isMyTurn, selectedWord);
        
        isWaitingForAnswer = true;
        
        // Phase 1 타임아웃 설정 (10초)
        if (PhotonNetwork.IsMasterClient)
        {
            CancelInvoke(nameof(QuizTimeOut));
            Invoke(nameof(QuizTimeOut), 10f);
        }
    }



    //[PunRPC]


    // ===== 정답 제출 =====
    public void SubmitAnswer(string answer)
    {
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        photonView.RPC("RPC_CheckAnswer", RpcTarget.MasterClient, myActorNumber, answer);
    }

    [PunRPC]
    void RPC_CheckAnswer(int actorNumber, string answer)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!isWaitingForAnswer) return;

        int targetIndex = currentTargetIndex % playerOrder.Count;
        int targetActorNumber = playerOrder[targetIndex];
        string correctAnswer = gameManager.CurrentQuizAnswer;

        bool isCorrect = answer.Trim().Equals(correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            isWaitingForAnswer = false;
            CancelInvoke(nameof(QuizTimeOut));

            int points = (actorNumber == targetActorNumber) ? 3 : 1;
            playerDataDict[actorNumber].score += points;

            photonView.RPC("RPC_UpdateScore", RpcTarget.All, actorNumber, playerDataDict[actorNumber].score);

            photonView.RPC("RPC_QuizResult", RpcTarget.All,
                actorNumber,
                targetActorNumber,
                correctAnswer,
                true,
                points);

            Invoke(nameof(NextQuizRound), 1.5f);
        }
        else
        {
            if (actorNumber == targetActorNumber)
            {
                photonView.RPC("RPC_TargetWrong", RpcTarget.All, targetActorNumber);
                CancelInvoke(nameof(QuizTimeOut));
                Invoke(nameof(QuizTimeOut), 10f);
            }
            else
            {
                photonView.RPC("RPC_WrongFeedback", RpcTarget.All, actorNumber);
            }
        }
    }

    [PunRPC]
    void RPC_UpdateScore(int actorNumber, int newScore)
    {
        // 모든 클라이언트의 playerDataDict 업데이트
        if (playerDataDict.ContainsKey(actorNumber))
        {
            playerDataDict[actorNumber].score = newScore;
        }

        // 로컬 플레이어의 점수라면 UI 업데이트
        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            GameEventSystem.Publish("OnScoreChanged", newScore);
        }

        Debug.Log($"[NetworkGameManager] Score updated: Player {actorNumber} = {newScore}");
    }

    void QuizTimeOut()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!isWaitingForAnswer) return;

        Debug.Log("[GameNetworkManager] QuizTimeOut called");
        
        // Phase 체크: GameManager에서 확인
        var currentPhase = gameManager.GetCurrentPhase();
        
        // 타겟 인덱스 계산 (플레이어 수로 모듈로)
        int targetIndex = currentTargetIndex % playerOrder.Count;
        int targetActorNumber = playerOrder[targetIndex];
        
        if (currentPhase == GameManager.QuizPhase.TargetTurn)
        {
            // Phase 1 타임아웃 -> Phase 2로 전환
            Debug.Log("[GameNetworkManager] Phase 1 timeout -> Starting Phase 2");
            photonView.RPC("RPC_TargetWrong", RpcTarget.All, targetActorNumber);
            
            // Phase 2 타임아웃 설정 (10초)
            CancelInvoke(nameof(QuizTimeOut));
            Invoke(nameof(QuizTimeOut), 10f);
        }
        else
        {
            // Phase 2 타임아웃 -> 다음 라운드
            Debug.Log("[GameNetworkManager] Phase 2 timeout -> Next round");
            string correctAnswer = gameManager.CurrentQuizAnswer;

            photonView.RPC("RPC_QuizResult", RpcTarget.All, 
                -1, 
                targetActorNumber, 
                correctAnswer, 
                false, 
                0);

            isWaitingForAnswer = false;
            Invoke(nameof(NextQuizRound), 1.5f);
        }
    }

    void NextQuizRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        currentTargetIndex++;

        // 총 라운드 = 플레이어 수 × 3
        int totalRounds = playerOrder.Count * 3;
        
        Debug.Log($"[GameNetworkManager] NextQuizRound: currentTargetIndex={currentTargetIndex}, totalRounds={totalRounds}");
        
        if (currentTargetIndex >= totalRounds)
        {
            Debug.Log("[GameNetworkManager] Game Over! Ending game.");
            photonView.RPC("RPC_EndGame", RpcTarget.All);
        }
        else
        {
            // 다음 라운드 단어 선택
            string selectedWord = SelectRandomWord();
            Debug.Log($"[GameNetworkManager] Starting round {currentTargetIndex + 1} with word: {selectedWord}");
            photonView.RPC("RPC_StartQuizRound", RpcTarget.All, currentTargetIndex, selectedWord);
        }
    }

    [PunRPC]
    void RPC_TargetWrong(int targetActorNumber)
    {
        Debug.Log($"[GameNetworkManager] Target {targetActorNumber} wrong! Starting Phase 2.");
        
        // GameManager에 Phase 2 시작 요청
        gameManager.StartPhase2();
    }

    [PunRPC]
    void RPC_WrongFeedback(int actorNumber)
    {
        if (actorNumber == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            GameEventSystem.Publish("OnQuizFeedback", "Wrong");
        }
    }

    [PunRPC]
    void RPC_QuizResult(int winnerActorNumber, int targetActorNumber, string correctAnswer, bool wasCorrect, int points)
    {
        Debug.Log($"[GameNetworkManager] Result: winner={winnerActorNumber}, target={targetActorNumber}, correct={wasCorrect}");

        gameManager.ShowQuizResult(targetActorNumber, correctAnswer, wasCorrect, winnerActorNumber, points);

        ShowRoundResult(winnerActorNumber, targetActorNumber, wasCorrect);
    }

    void ShowRoundResult(int winnerActorNumber, int targetActorNumber, bool wasCorrect)
    {
        RoundResultData resultData = new RoundResultData();

        // 타겟의 그림 가져오기
        if (playerDataDict.ContainsKey(targetActorNumber))
        {
            var targetData = playerDataDict[targetActorNumber];
            string currentWord = gameManager.CurrentQuizAnswer;

            if (targetData.drawings.ContainsKey(currentWord))
            {
                resultData.targetDrawing = targetData.drawings[currentWord];
            }
        }

        // 결과 메시지 및 Winner 그림
        if (wasCorrect && winnerActorNumber == targetActorNumber)
        {
            // 케이스 1: 타겟이 맞춤
            string targetName = GetPlayerName(targetActorNumber);
            resultData.message = $"{targetName}님이 맞췄습니다!";
            resultData.winnerDrawing = null; // 타겟 그림만 표시
        }
        else if (wasCorrect && winnerActorNumber != targetActorNumber)
        {
            // 케이스 2: 다른 사람이 맞춤
            string targetName = GetPlayerName(targetActorNumber);
            string winnerName = GetPlayerName(winnerActorNumber);
            resultData.message = $"{winnerName}님이 맞췄습니다!\n{targetName}님은 틀렸습니다.";

            // 맞춘 사람의 그림도 가져오기
            if (playerDataDict.ContainsKey(winnerActorNumber))
            {
                var winnerData = playerDataDict[winnerActorNumber];
                string currentWord = gameManager.CurrentQuizAnswer;

                if (winnerData.drawings.ContainsKey(currentWord))
                {
                    resultData.winnerDrawing = winnerData.drawings[currentWord];
                }
            }
        }
        else
        {
            // 케이스 3: 아무도 못 맞춤
            string targetName = GetPlayerName(targetActorNumber);
            resultData.message = $"시간 초과!\n정답: {gameManager.CurrentQuizAnswer}";
            resultData.winnerDrawing = null;
        }

        GameEventSystem.Publish("OnRoundResult", resultData);
    }

    string GetPlayerName(int actorNumber)
    {
        if (playerDataDict.ContainsKey(actorNumber))
        {
            return playerDataDict[actorNumber].nickname;
        }
        return $"Player{actorNumber}";
    }

    [PunRPC]
    void RPC_EndGame()
    {
        Debug.Log("[NetworkGameManager] Game End!");

        var sortedPlayers = playerDataDict.Values.OrderByDescending(p => p.score).ToList();
        gameManager.EndGame(sortedPlayers);

        if (PhotonNetwork.IsMasterClient)
        {
            Invoke(nameof(AutoReturnToLobby), 10f);
        }
    }

    public void AutoReturnToLobby()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("[NetworkGameManager] Auto returning to lobby...");
            PhotonNetwork.LoadLevel("LobbyScene");
        }
    }

    // ===== 플레이어 데이터 접근 =====
    public Dictionary<int, PlayerData> GetAllPlayerData()
    {
        return playerDataDict;
    }

    public PlayerData GetPlayerData(int actorNumber)
    {
        return playerDataDict.ContainsKey(actorNumber) ? playerDataDict[actorNumber] : null;
    }

    public void SavePlayerDrawing(string word, byte[] pngData)
    {
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        if (playerDataDict.ContainsKey(myActorNumber))
        {
            playerDataDict[myActorNumber].drawings[word] = pngData;
            Debug.Log($"[NetworkGameManager] Saved drawing for word: {word}, size: {pngData.Length} bytes");
        }
    }
}

[System.Serializable]
public class PlayerData
{
    public int actorNumber;
    public string nickname;
    public int score;
    public Dictionary<string, byte[]> drawings = new Dictionary<string, byte[]>(); // 단어 -> PNG
}