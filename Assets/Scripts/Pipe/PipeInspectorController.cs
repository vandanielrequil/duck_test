using UnityEngine;
using UnityEngine.Serialization;

public class PipeInspectorController : MonoBehaviour
{
    [FormerlySerializedAs("_pipeline")]
    [SerializeField] private MonoBehaviour _pipelineHost;
    [SerializeField] private GameStateManager _gameState;

    public void BeginInspection(PipeObject obj)
    {
        IPipelineControl pipeline = _pipelineHost as IPipelineControl;
        if (pipeline != null)
            pipeline.IsPaused = true;
        if (_gameState != null)
            _gameState.SetState(PipeGameState.Inspecting);

        obj?.OnInspect();

        if (_gameState != null)
            _gameState.SetState(PipeGameState.Playing);
        if (pipeline != null)
            pipeline.IsPaused = false;
    }
}
