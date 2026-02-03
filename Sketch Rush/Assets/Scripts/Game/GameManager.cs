using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private AIWordGenerator wordGenerator;
    [SerializeField] private DrawingCanvas drawingCanvas;

    public bool IsPlaying { get; private set; }

    private GameState currentState;
    private Queue<string> wordQueue = new Queue<string>();
    private string currentWord;
    private float timeLeft;
    private int score;

    [Header("Game Settings")]
    [SerializeField] private float roundTime = 5f;  // 매 단어당 5초

    public float TimeLeft => timeLeft;
    public string CurrentWord => currentWord;
    public int Score => score;

    void Start()
    {
        ChangeState(new WaitingState(this));
    }

    void Update()
    {
        currentState?.Update();
    }

    // 상태 전환
    public void ChangeState(GameState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState.Enter();
    }

    // Loading 시작 → AI 단어 로딩
    public void StartWordLoading()
    {
        StartCoroutine(LoadWords());
    }

    IEnumerator LoadWords()
    {
        yield return StartCoroutine(wordGenerator.GenerateWords());

        if (wordGenerator.generatedWords != null && wordGenerator.generatedWords.Length > 0)
        {
            wordQueue.Clear();
            foreach (string word in wordGenerator.generatedWords)
            {
                wordQueue.Enqueue(word);
            }
            Debug.Log($"[GameManager] Loaded {wordQueue.Count} words");
            ChangeState(new PlayingState(this));
        }
        else
        {
            Debug.LogError("[GameManager] Word loading failed!");
            ChangeState(new WaitingState(this));
        }
    }

    // Playing 시작
    public void StartPlaying()
    {
        NextWord();              // 첫 단어
        timeLeft = roundTime;    // 5초
        IsPlaying = true;
        GameEventSystem.Publish("OnTimerStart", roundTime);
    }

    // Playing 업데이트 (매프레임)
    public void UpdatePlaying()
    {
        if (!IsPlaying) return;

        timeLeft -= Time.deltaTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);

        if (timeLeft <= 0)
        {
            if (wordQueue.Count > 0)
            {
                // 단어 남아있으면 다음 단어 + 타이머 리셋
                NextWord();
                timeLeft = roundTime;
            }
            else
            {
                // 단어 없으면 게임 종료
                timeLeft = 0;
                IsPlaying = false;
                GameEventSystem.Publish("OnTimerEnd");
                ChangeState(new EndState(this));
            }
        }
    }

    // 다음 단어
    public void NextWord()
    {
        if (wordQueue.Count > 0)
        {
            currentWord = wordQueue.Dequeue();
            GameEventSystem.Publish("OnWordChanged", currentWord);
            if (drawingCanvas != null)
                drawingCanvas.ClearCanvas();
            Debug.Log($"[GameManager] Current word: {currentWord}");
        }
        else
        {
            Debug.Log("[GameManager] No more words!");
            ChangeState(new EndState(this));
        }
    }

    // 점수 추가
    public void AddScore(int points)
    {
        score += points;
        GameEventSystem.Publish("OnScoreChanged", score);
    }

    // 게임 종료
    public void EndGame()
    {
        Debug.Log($"[GameManager] Game End! Score: {score}");
        GameEventSystem.Publish("OnGameEnd", score);
    }

    // 게임 재시작
    public void RestartGame()
    {
        score = 0;
        timeLeft = 0;
        IsPlaying = false;
        wordQueue.Clear();
        currentWord = null;
        if (drawingCanvas != null)
            drawingCanvas.ClearCanvas();
        ChangeState(new WaitingState(this));
    }
}