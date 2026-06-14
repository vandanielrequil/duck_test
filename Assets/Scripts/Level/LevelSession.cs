using UnityEngine;

public class LevelSession : MonoBehaviour
{
    [SerializeField] private LevelDatabase _database;
    [SerializeField] private LevelManager _levelManager;
    [SerializeField] private InspectorController _inspector;
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private PipelineSpawner _spawner;
    [SerializeField] private GameStateManager _gameState;
    [SerializeField] private LevelResultsScreen _resultsScreen;
    [SerializeField] private LevelMenuController _menuController;

    [Header("Debug")]
    [SerializeField] private bool _autoStartLevelForDebug;
    [SerializeField] private bool _ignoreSaveForTesting;
    [SerializeField] private int _debugStartLevelIndex;

    private int _levelIndex;
    private bool _levelEnded;
    private LevelConfig _activeConfig;

    public LevelDatabase Database => _database;
    public int ActiveLevelIndex => _levelIndex;
    public bool IsLevelActive => _activeConfig != null && !_levelEnded;
    public int LevelCount => _database?.Levels?.Length ?? 0;
    public bool HasNextLevel =>
        _database?.Levels != null
        && _levelIndex + 1 < _database.Levels.Length;

    private void Awake()
    {
        if (_levelManager == null)
            _levelManager = FindAnyObjectByType<LevelManager>();
        if (_inspector == null)
            _inspector = FindAnyObjectByType<InspectorController>();
        if (_pipeline == null)
            _pipeline = FindAnyObjectByType<PipelineController>();
        if (_spawner == null)
            _spawner = FindAnyObjectByType<PipelineSpawner>();
        if (_gameState == null)
            _gameState = FindAnyObjectByType<GameStateManager>();
        if (_menuController == null)
            _menuController = FindAnyObjectByType<LevelMenuController>();

        if (_levelManager != null)
            _levelManager.OnAllGoalsComplete += HandleAllGoalsComplete;

        if (_inspector != null)
            _inspector.OnRageModeEntered += HandleRageModeEntered;

        if (_pipeline != null)
            _pipeline.OnPipelineDrained += HandlePipelineDrained;

        if (_autoStartLevelForDebug)
            StartLevel(
                _ignoreSaveForTesting
                    ? _debugStartLevelIndex
                    : PlayerProgress.CurrentLevelIndex
            );
        else
            ReturnToMenu();
    }

    private void OnDestroy()
    {
        if (_levelManager != null)
            _levelManager.OnAllGoalsComplete -= HandleAllGoalsComplete;

        if (_inspector != null)
            _inspector.OnRageModeEntered -= HandleRageModeEntered;

        if (_pipeline != null)
            _pipeline.OnPipelineDrained -= HandlePipelineDrained;
    }

    public bool IsLevelUnlocked(int index)
    {
        if (index < 0 || index >= LevelCount)
            return false;

        if (_ignoreSaveForTesting)
            return true;

        return index <= PlayerProgress.CurrentLevelIndex;
    }

    public void StartCurrentLevel()
    {
        int startIndex = _ignoreSaveForTesting
            ? _debugStartLevelIndex
            : PlayerProgress.CurrentLevelIndex;

        StartLevel(startIndex);
    }

    public void StartLevel(int index)
    {
        if (_database == null || _database.Levels == null
            || _database.Levels.Length == 0)
        {
            Debug.LogError("[LevelSession] Level database is empty.");
            return;
        }

        if (!IsLevelUnlocked(index))
        {
            Debug.LogWarning(
                $"[LevelSession] Level index {index} is locked or missing."
            );
            return;
        }

        BeginLevel(index);
    }

    public void RetryCurrentLevel()
    {
        BeginLevel(_levelIndex);
    }

    public void StartNextLevel()
    {
        if (!HasNextLevel)
        {
            Debug.Log("[LevelSession] Campaign complete.");
            if (_gameState != null)
                _gameState.SetState(PipeGameState.CampaignComplete);
            return;
        }

        StartLevel(_levelIndex + 1);
    }

    public void ReturnToMenu()
    {
        _levelEnded = true;
        _activeConfig = null;
        _resultsScreen?.HideImmediate();
        _inspector?.SetLevelEnded(true);
        _pipeline?.StopPipeline();

        if (_gameState != null)
            _gameState.SetState(PipeGameState.MainMenu);

        _menuController?.ShowMainMenu();
    }

    private void BeginLevel(int index)
    {
        if (_database == null || _database.Levels == null
            || _database.Levels.Length == 0)
        {
            Debug.LogError("[LevelSession] Level database is empty.");
            return;
        }

        _levelIndex = Mathf.Clamp(index, 0, _database.Levels.Length - 1);
        _activeConfig = _database.Levels[_levelIndex];
        _levelEnded = false;

        _resultsScreen?.HideImmediate();
        _menuController?.HideAll();

        _levelManager?.Load(_activeConfig);
        _inspector?.BindLevel(_activeConfig, _levelManager, false);
        _inspector?.SetLevelEnded(false);
        _spawner?.BindQueue(_activeConfig.SpawnQueue);
        _pipeline?.ResetForLevel(_activeConfig, _inspector, _spawner);

        if (_gameState != null)
            _gameState.SetState(PipeGameState.Playing);

        Debug.Log(
            $"[LevelSession] Started {_activeConfig.DisplayName} "
            + $"(index {_levelIndex})"
        );
    }

    private void HandleAllGoalsComplete()
    {
        if (_levelEnded || _activeConfig == null)
            return;

        if (!_activeConfig.EndRules.WinOnGoalsComplete)
            return;

        EndLevel(LevelEndOutcome.Success, LevelEndReason.GoalsComplete);
    }

    private void HandlePipelineDrained()
    {
        if (_levelEnded || _activeConfig == null)
            return;

        if (!_activeConfig.EndRules.WinOnPipelineDrained)
            return;

        EndLevel(LevelEndOutcome.Success, LevelEndReason.PipelineDrained);
    }

    private void HandleRageModeEntered(int count)
    {
        if (_levelEnded || _activeConfig == null)
            return;

        if (count < _activeConfig.EndRules.LoseOnRageModeCount)
            return;

        EndLevel(LevelEndOutcome.Fail, LevelEndReason.RageModeLimit);
    }

    private void EndLevel(LevelEndOutcome outcome, LevelEndReason reason)
    {
        if (_levelEnded)
            return;

        _levelEnded = true;
        _inspector?.SetLevelEnded(true);
        _pipeline?.StopPipeline();

        if (_gameState != null)
        {
            _gameState.SetState(
                outcome == LevelEndOutcome.Success
                    ? PipeGameState.LevelWon
                    : PipeGameState.LevelLost
            );
        }

        if (outcome == LevelEndOutcome.Success
            && _activeConfig != null
            && !_ignoreSaveForTesting)
        {
            PlayerProgress.CompleteLevel(
                _activeConfig.LevelId,
                _levelIndex + 1
            );
        }

        LevelResultsSnapshot snapshot =
            _levelManager.BuildResultsSnapshot(
                outcome,
                reason,
                _inspector != null ? _inspector.RageModeCount : 0
            );

        if (_resultsScreen != null)
        {
            _resultsScreen.Show(
                snapshot,
                HasNextLevel,
                StartNextLevel,
                RetryCurrentLevel,
                ReturnToMenu
            );
        }
        else
        {
            Debug.Log(
                "[LevelSession] Level ended without results screen."
            );
            ReturnToMenu();
        }
    }
}
