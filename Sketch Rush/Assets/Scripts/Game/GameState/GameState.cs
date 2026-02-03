using UnityEngine;

public abstract class GameState
{
    protected GameManager gameManager;

    public GameState(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }

    public abstract void Enter();
    public abstract void Update();
    public abstract void Exit();
}

// Waiting: 시작 버튼 표시
public class WaitingState : GameState
{
    public WaitingState(GameManager gameManager) : base(gameManager) { }

    public override void Enter()
    {
        Debug.Log("[GameState] Waiting");
        GameEventSystem.Publish("OnStateChanged", "Waiting");
    }

    public override void Update() { }

    public override void Exit() { }
}

// Loading: AI 단어 로딩 중
public class LoadingState : GameState
{
    public LoadingState(GameManager gameManager) : base(gameManager) { }

    public override void Enter()
    {
        Debug.Log("[GameState] Loading");
        GameEventSystem.Publish("OnStateChanged", "Loading");
        gameManager.StartWordLoading();
    }

    public override void Update() { }

    public override void Exit() { }
}

// Drawing: 그림 그리기 + 타이머
public class DrawingState : GameState
{
    public DrawingState(GameManager gameManager) : base(gameManager) { }

    public override void Enter()
    {
        Debug.Log("[GameState] Drawing");
        GameEventSystem.Publish("OnStateChanged", "Drawing");
        gameManager.StartDrawing();
    }

    public override void Update()
    {
        gameManager.UpdateDrawing();
    }

    public override void Exit() { }
}

// Quiz: 그림 맞추기
public class QuizState : GameState
{
    public QuizState(GameManager gameManager) : base(gameManager) { }

    public override void Enter()
    {
        Debug.Log("[GameState] Quiz");
        GameEventSystem.Publish("OnStateChanged", "Quiz");
        gameManager.StartQuiz();
    }

    public override void Update()
    {
        gameManager.UpdateQuiz();
    }

    public override void Exit() { }
}

// End: 결과 표시
public class EndState : GameState
{
    public EndState(GameManager gameManager) : base(gameManager) { }

    public override void Enter()
    {
        Debug.Log("[GameState] End");
        GameEventSystem.Publish("OnStateChanged", "End");
        gameManager.EndGame();
    }

    public override void Update() { }

    public override void Exit() { }
}