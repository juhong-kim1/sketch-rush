using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    private int currentScore;
    private int highScore;

    public int CurrentScore => currentScore;
    public int HighScore => highScore;

    void Awake()
    {
        GameEventSystem.Subscribe("OnGameEnd", OnGameEnd);
    }

    void OnDestroy()
    {
        GameEventSystem.Unsubscribe("OnGameEnd", OnGameEnd);
    }

    // 점수 추가
    public void AddScore(int points)
    {
        currentScore += points;
        GameEventSystem.Publish("OnScoreChanged", currentScore);
        Debug.Log($"[ScoreManager] Score: {currentScore}");
    }

    // 점수 초기화
    public void ResetScore()
    {
        currentScore = 0;
        GameEventSystem.Publish("OnScoreChanged", currentScore);
    }

    // 하이스코어 갱신
    private void UpdateHighScore()
    {
        if (currentScore > highScore)
        {
            highScore = currentScore;
            Debug.Log($"[ScoreManager] New HighScore: {highScore}");
        }
    }

    private void OnGameEnd(object data)
    {
        UpdateHighScore();
        Debug.Log($"[ScoreManager] Game End - Score: {currentScore}, HighScore: {highScore}");
    }
}