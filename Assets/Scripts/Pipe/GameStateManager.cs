using UnityEngine;

public enum PipeGameState
{
    Playing,
    Inspecting,
    GameOver,
    Paused,
}

public class GameStateManager : MonoBehaviour
{
    [SerializeField] private PipeGameState _state = PipeGameState.Playing;

    public PipeGameState Current => _state;

    public void SetState(PipeGameState state)
    {
        _state = state;
    }
}
