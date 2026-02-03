using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private AIWordGenerator wordGenerator;
    [SerializeField] private DrawingCanvas drawingCanvas;

    // === Game Settings ===
    [Header("Game Settings")]
    [SerializeField] private float drawingTime = 5f;
    [SerializeField] private float quizTime = 10f;
    [SerializeField] private int quizCount = 3;

    // === State ===
    private GameState currentState;
    public bool IsActive { get; private set; }

    // === Drawing Phase ===
    private Queue<string> wordQueue = new Queue<string>();
    private string currentWord;
    private float timeLeft;
    private Dictionary<string, Texture2D> drawnImages = new Dictionary<string, Texture2D>();

    // === Quiz Phase ===
    private List<KeyValuePair<string, Texture2D>> quizList = new List<KeyValuePair<string, Texture2D>>();
    private int currentQuizIndex;
    private string currentQuizAnswer;
    private Texture2D currentQuizImage;

    // === Score ===
    private int score;

    // === Properties ===
    public float TimeLeft => timeLeft;
    public string CurrentWord => currentWord;
    public int Score => score;
    public string CurrentQuizAnswer => currentQuizAnswer;
    public Texture2D CurrentQuizImage => currentQuizImage;

    void Start()
    {
        ChangeState(new WaitingState(this));
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
        StartCoroutine(LoadWords());
    }

    IEnumerator LoadWords()
    {
        yield return StartCoroutine(wordGenerator.GenerateWords());
        if (wordGenerator.generatedWords != null && wordGenerator.generatedWords.Length > 0)
        {
            wordQueue.Clear();
            drawnImages.Clear();
            foreach (string word in wordGenerator.generatedWords)
                wordQueue.Enqueue(word);
            Debug.Log($"[GameManager] Loaded {wordQueue.Count} words");
            ChangeState(new DrawingState(this));
        }
        else
        {
            Debug.LogError("[GameManager] Word loading failed!");
            ChangeState(new WaitingState(this));
        }
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
        IsActive = true;
        PrepareQuizList();
        currentQuizIndex = 0;
        LoadCurrentQuiz();
        timeLeft = quizTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);
    }

private void PrepareQuizList()
    {
        var all = new List<KeyValuePair<string, Texture2D>>(drawnImages);
        var rng = new System.Random();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        quizList.Clear();
        int count = Mathf.Min(quizCount, all.Count);
        for (int i = 0; i < count; i++) quizList.Add(all[i]);
        Debug.Log($"[GameManager] Quiz: {quizList.Count} questions");
    }

private void LoadCurrentQuiz()
    {
        if (currentQuizIndex >= quizList.Count) return;
        currentQuizAnswer = quizList[currentQuizIndex].Key;
        currentQuizImage = quizList[currentQuizIndex].Value;
        GameEventSystem.Publish("OnQuizLoaded", currentQuizImage);
        GameEventSystem.Publish("OnQuizProgress", $"{currentQuizIndex + 1}/{quizList.Count}");
        Debug.Log($"[GameManager] Quiz [{currentQuizIndex + 1}/{quizList.Count}]");
    }

    public void UpdateQuiz()
    {
        if (!IsActive) return;
        timeLeft -= Time.deltaTime;
        GameEventSystem.Publish("OnTimerUpdate", timeLeft);
        if (timeLeft <= 0) OnQuizWrong();
    }

    public void CheckAnswer(string playerAnswer)
    {
        if (!IsActive) return;
        if (playerAnswer.Trim().Equals(currentQuizAnswer.Trim(), System.StringComparison.OrdinalIgnoreCase))
            OnQuizCorrect();
        else
            GameEventSystem.Publish("OnQuizFeedback", "Wrong");
    }

    private void OnQuizCorrect()
    {
        IsActive = false;
        score += 10;
        GameEventSystem.Publish("OnScoreChanged", score);
        GameEventSystem.Publish("OnQuizFeedback", "Correct");
        Debug.Log($"[GameManager] Correct! Score: {score}");
        Invoke(nameof(NextQuiz), 0.8f);
    }

private void OnQuizWrong()
    {
        IsActive = false;
        GameEventSystem.Publish("OnQuizFeedback", "TimeOut:" + currentQuizAnswer);
        Debug.Log($"[GameManager] TimeOut! Answer: {currentQuizAnswer}");
        Invoke(nameof(NextQuiz), 1.5f);
    }

    private void NextQuiz()
    {
        currentQuizIndex++;
        if (currentQuizIndex < quizList.Count)
        {
            IsActive = true;
            LoadCurrentQuiz();
            timeLeft = quizTime;
            GameEventSystem.Publish("OnTimerUpdate", timeLeft);
            GameEventSystem.Publish("OnQuizFeedbackClear");
        }
        else
        {
            ChangeState(new EndState(this));
        }
    }

    // ===== End / Restart =====
    public void EndGame()
    {
        IsActive = false;
        Debug.Log($"[GameManager] Game End! Score: {score}");
        GameEventSystem.Publish("OnGameEnd", score);
    }

    public void RestartGame()
    {
        score = 0;
        timeLeft = 0;
        IsActive = false;
        wordQueue.Clear();
        drawnImages.Clear();
        quizList.Clear();
        currentQuizIndex = 0;
        currentWord = null;
        currentQuizAnswer = null;
        currentQuizImage = null;
        if (drawingCanvas != null) drawingCanvas.ClearCanvas();
        ChangeState(new WaitingState(this));
    }
}
