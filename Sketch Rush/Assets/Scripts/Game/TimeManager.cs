using UnityEngine;

/// <summary>
/// 타이머 관리 (단일 책임 - 타이머만 담당)
/// GameEventSystem으로 타이머 상태를 전달
/// </summary>
public class TimerManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float defaultTime = 30f;

    private float timeLeft;
    private bool isRunning = false;

    public float TimeLeft => timeLeft;
    public bool IsRunning => isRunning;

    void Awake()
    {
        // Observer 구독
        GameEventSystem.Subscribe("OnTimerStart", OnTimerStart);
        GameEventSystem.Subscribe("OnTimerEnd", OnTimerEnd);
    }

    void OnDestroy()
    {
        // 구독 해제 (메모리 누수 방지)
        GameEventSystem.Unsubscribe("OnTimerStart", OnTimerStart);
        GameEventSystem.Unsubscribe("OnTimerEnd", OnTimerEnd);
    }

    void Update()
    {
        if (!isRunning) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0)
        {
            timeLeft = 0;
            isRunning = false;
            GameEventSystem.Publish("OnTimerEnd");
        }
    }

    private void OnTimerStart(object data)
    {
        timeLeft = data != null ? (float)data : defaultTime;
        isRunning = true;
        Debug.Log($"[TimerManager] Started: {timeLeft}s");
    }

    private void OnTimerEnd(object data)
    {
        isRunning = false;
        Debug.Log("[TimerManager] Ended");
    }

    public void StartTimer(float time)
    {
        timeLeft = time;
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer()
    {
        timeLeft = defaultTime;
        isRunning = false;
    }
}