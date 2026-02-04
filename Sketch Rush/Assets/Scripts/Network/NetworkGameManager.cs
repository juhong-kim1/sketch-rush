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
            photonView.RPC("RPC_StartQuizRound", RpcTarget.All, 0);
        }
    }

    [PunRPC]
    void RPC_StartQuizRound(int roundIndex)
    {
        currentTargetIndex = roundIndex % playerOrder.Count;
        int targetActorNumber = playerOrder[currentTargetIndex];
        
        Debug.Log($"[GameNetworkManager] Round {roundIndex + 1}: Target = {targetActorNumber}");
        
        // GameManager에 퀴즈 시작 알림
        bool isMyTurn = targetActorNumber == PhotonNetwork.LocalPlayer.ActorNumber;
        gameManager.StartQuizRound(roundIndex, targetActorNumber, isMyTurn);
        
        isWaitingForAnswer = true;
    }

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

        int targetActorNumber = playerOrder[currentTargetIndex];
        string correctAnswer = gameManager.CurrentQuizAnswer;

        bool isCorrect = answer.Trim().Equals(correctAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase);

        if (isCorrect)
        {
            // 정답!
            int points = (actorNumber == targetActorNumber) ? 3 : 1;
            playerDataDict[actorNumber].score += points;

            photonView.RPC("RPC_QuizResult", RpcTarget.All, 
                actorNumber, 
                targetActorNumber, 
                correctAnswer, 
                true, 
                points);

            isWaitingForAnswer = false;
            
            // 1.5초 후 다음 라운드 또는 종료
            Invoke(nameof(NextQuizRound), 1.5f);
        }
        else
        {
            // 틀림 (지목된 사람만)
            if (actorNumber == targetActorNumber)
            {
                photonView.RPC("RPC_TargetWrong", RpcTarget.All, targetActorNumber);
                // 10초 타이머 시작 (다른 사람들 경쟁)
                Invoke(nameof(QuizTimeOut), 10f);
            }
            else
            {
                // 지목 안 된 사람이 틀림 → 피드백만
                photonView.RPC("RPC_WrongFeedback", RpcTarget.All, actorNumber);
            }
        }
    }

    void QuizTimeOut()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!isWaitingForAnswer) return;

        // 타임아웃
        int targetActorNumber = playerOrder[currentTargetIndex];
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

    void NextQuizRound()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        currentTargetIndex++;

        // 3라운드 끝?
        if (currentTargetIndex >= 1)
        {
            photonView.RPC("RPC_EndGame", RpcTarget.All);
        }
        else
        {
            photonView.RPC("RPC_StartQuizRound", RpcTarget.All, currentTargetIndex);
        }
    }

    [PunRPC]
    void RPC_TargetWrong(int targetActorNumber)
    {
        Debug.Log($"[GameNetworkManager] Target {targetActorNumber} wrong! Others can answer now.");
        GameEventSystem.Publish("OnQuizFeedback", "TargetWrong");
        // 10초 타이머 UI 표시
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
        
        // 점수 갱신
        if (winnerActorNumber > 0 && playerDataDict.ContainsKey(winnerActorNumber))
        {
            playerDataDict[winnerActorNumber].score += points;
        }

        gameManager.ShowQuizResult(targetActorNumber, correctAnswer, wasCorrect, winnerActorNumber, points);
    }

    [PunRPC]
    void RPC_EndGame()
    {
        Debug.Log("[GameNetworkManager] Game End!");
        
        // 최종 점수 계산
        var sortedPlayers = playerDataDict.Values.OrderByDescending(p => p.score).ToList();
        
        gameManager.EndGame(sortedPlayers);
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
}

[System.Serializable]
public class PlayerData
{
    public int actorNumber;
    public string nickname;
    public int score;
    public Dictionary<string, byte[]> drawings = new Dictionary<string, byte[]>(); // 단어 -> PNG
}