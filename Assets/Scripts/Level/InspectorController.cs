using System;
using System.Collections;
using UnityEngine;

public interface IPipelineControl
{
    bool IsPaused { get; set; }
}

public class InspectorController : MonoBehaviour
{
    [SerializeField] private PipelineController _pipeline;
    [SerializeField] private GameStateManager _gameState;
    [SerializeField] private LevelManager _levelManager;

    private readonly InspectorAngerState _anger = new();
    private LevelConfig _config;
    private Coroutine _inspectionRoutine;
    private bool _levelEnded;

    public bool IsBusy => _inspectionRoutine != null;
    public InspectorAngerState Anger => _anger;
    public int RageModeCount => _anger.RageModeCount;

    public event Action<PipeObject, InspectionOutcome, int> OnInspectionResolved;
    public event Action<int> OnRageModeEntered;

    private void OnEnable()
    {
        _anger.OnRageModeEntered += HandleRageModeEntered;
    }

    private void OnDisable()
    {
        _anger.OnRageModeEntered -= HandleRageModeEntered;

        if (_inspectionRoutine != null)
        {
            StopCoroutine(_inspectionRoutine);
            _inspectionRoutine = null;
        }
    }

    public void BindLevel(
        LevelConfig config,
        LevelManager levelManager,
        bool levelEnded = false
    )
    {
        _config = config;
        _levelManager = levelManager;
        _levelEnded = levelEnded;
        _anger.BindConfig(config);
        _anger.Reset();
    }

    public void SetLevelEnded(bool ended) => _levelEnded = ended;

    public void BeginInspection(
        PipeObject obj,
        Action onComplete
    )
    {
        if (obj == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_inspectionRoutine != null)
            StopCoroutine(_inspectionRoutine);

        _inspectionRoutine =
            StartCoroutine(InspectionRoutine(obj, onComplete));
    }

    private IEnumerator InspectionRoutine(
        PipeObject obj,
        Action onComplete
    )
    {
        if (_pipeline != null)
            _pipeline.IsPaused = true;

        obj.OnInspect();

        float duration = GetInspectionDuration();
        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        InspectionOutcome outcome = ResolveOutcome(obj);
        int duckiness = obj.Duckiness;
        int angerDelta = GetAngerDelta(duckiness, outcome);

        if (angerDelta > 0)
            _anger.AddAnger(angerDelta);

        _levelManager?.ReportInspection(obj, outcome, duckiness);
        OnInspectionResolved?.Invoke(obj, outcome, duckiness);

        Debug.Log(
            $"[Inspector] {obj.State?.ObjectData?.name}: duckiness={duckiness}/3 "
            + $"-> {outcome}, anger+={angerDelta}, tier={_anger.Tier}"
        );

        if (obj.CurrentSlot != null)
            obj.CurrentSlot.ClearOccupant();

        Destroy(obj.gameObject);

        if (_pipeline != null && !_levelEnded)
            _pipeline.IsPaused = false;

        _inspectionRoutine = null;
        onComplete?.Invoke();
    }

    private float GetInspectionDuration()
    {
        if (_config == null)
            return 5f;

        float multiplier = _anger.GetInspectionSpeedMultiplier();
        return _config.BaseInspectionDuration / Mathf.Max(0.1f, multiplier);
    }

    private InspectionOutcome ResolveOutcome(PipeObject obj)
    {
        int duckiness = obj.Duckiness;

        if (_anger.Tier >= 2 && duckiness < 3)
            return InspectionOutcome.Reject;

        return duckiness switch
        {
            3 => InspectionOutcome.Approve,
            2 => InspectionOutcome.Approve,
            _ => InspectionOutcome.Reject,
        };
    }

    private static int GetAngerDelta(
        int duckiness,
        InspectionOutcome outcome
    )
    {
        if (outcome == InspectionOutcome.Approve)
        {
            return duckiness switch
            {
                2 => 1,
                _ => 0,
            };
        }

        return duckiness switch
        {
            1 => 2,
            _ => 3,
        };
    }

    private void HandleRageModeEntered(int count)
    {
        Debug.Log($"[Inspector] Rage mode #{count}");
        OnRageModeEntered?.Invoke(count);
    }
}

public class InspectorAngerState
{
    public int Current { get; private set; }
    public int RageModeCount { get; private set; }

    private LevelConfig _config;

    public int Tier
    {
        get
        {
            if (_config == null)
                return 0;

            if (Current >= _config.AngerThresholdTier2)
                return 2;

            if (Current >= _config.AngerThresholdTier1)
                return 1;

            return 0;
        }
    }

    public event Action<int> OnAngerChanged;
    public event Action<int> OnRageModeEntered;

    public void BindConfig(LevelConfig config) =>
        _config = config;

    public void Reset()
    {
        Current = 0;
        RageModeCount = 0;
        OnAngerChanged?.Invoke(Current);
    }

    public void AddAnger(int delta)
    {
        if (delta <= 0 || _config == null)
            return;

        Current = Mathf.Clamp(
            Current + delta,
            0,
            _config.AngerMax
        );
        OnAngerChanged?.Invoke(Current);

        if (Current >= _config.AngerThresholdTier2)
            EnterRageMode();
    }

    private void EnterRageMode()
    {
        RageModeCount++;
        OnRageModeEntered?.Invoke(RageModeCount);
        Current = 0;
        OnAngerChanged?.Invoke(Current);
    }

    public float GetInspectionSpeedMultiplier()
    {
        if (_config == null)
            return 1f;

        return Tier switch
        {
            2 => _config.Tier2SpeedMultiplier,
            1 => _config.Tier1SpeedMultiplier,
            _ => 1f,
        };
    }
}
