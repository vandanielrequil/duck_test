using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    private readonly List<LevelGoalRuntime> _goals = new();

    private LevelConfig _config;
    private int _objectsApproved;
    private int _actionsUsed;
    private int _rageAccumulated;
    private int _additionalCompleted;

    public LevelConfig CurrentConfig => _config;
    public IReadOnlyList<LevelGoalRuntime> Goals => _goals;
    public int ObjectsApproved => _objectsApproved;
    public int ActionsUsed => _actionsUsed;
    public int RageAccumulated => _rageAccumulated;
    public int AdditionalCompleted => _additionalCompleted;

    public event Action<LevelGoalRuntime> OnGoalProgress;
    public event Action OnAllGoalsComplete;

    public void Load(LevelConfig config)
    {
        _config = config;
        _objectsApproved = 0;
        _actionsUsed = 0;
        _rageAccumulated = 0;
        _additionalCompleted = 0;
        _goals.Clear();

        if (config?.Goals == null)
            return;

        foreach (LevelGoalData goal in config.Goals)
            _goals.Add(new LevelGoalRuntime(goal));
    }

    public void RegisterAction()
    {
        if (_config == null)
            return;

        _actionsUsed++;
    }

    public void RegisterRage(int delta)
    {
        if (_config == null || delta <= 0)
            return;

        _rageAccumulated += delta;
    }

    public bool AllGoalsComplete
    {
        get
        {
            bool hasRequired = false;
            foreach (LevelGoalRuntime goal in _goals)
            {
                if (goal.Config.IsAdditional)
                    continue;

                hasRequired = true;
                if (!goal.IsComplete)
                    return false;
            }

            return hasRequired;
        }
    }

    public void ReportInspection(
        PipeObject obj,
        InspectionOutcome outcome,
        int duckiness
    )
    {
        if (_config == null || obj == null)
            return;

        ReportMergeGoals(obj);

        if (outcome != InspectionOutcome.Approve)
            return;

        _objectsApproved++;

        foreach (LevelGoalRuntime goal in _goals)
        {
            if (goal.IsComplete)
                continue;

            if (!goal.MatchesApprove(obj, duckiness))
                continue;

            goal.AddProgress();
            OnGoalProgress?.Invoke(goal);

            if (goal.Config.IsAdditional && goal.IsComplete)
                _additionalCompleted++;
        }

        if (AllGoalsComplete)
            OnAllGoalsComplete?.Invoke();
    }

    private void ReportMergeGoals(PipeObject obj)
    {
        bool progress = false;

        foreach (LevelGoalRuntime goal in _goals)
        {
            if (goal.IsComplete)
                continue;

            bool matched = false;

            if (goal.MatchesMergePair(
                    obj.State?.MergeInputA,
                    obj.State?.MergeInputB
                ))
            {
                goal.AddProgress();
                OnGoalProgress?.Invoke(goal);
                matched = true;
                progress = true;
            }
            else if (goal.MatchesMergeResult(obj.State?.ObjectData))
            {
                goal.AddProgress();
                OnGoalProgress?.Invoke(goal);
                matched = true;
                progress = true;
            }

            if (matched && goal.Config.IsAdditional && goal.IsComplete)
                _additionalCompleted++;
        }

        if (progress && AllGoalsComplete)
            OnAllGoalsComplete?.Invoke();
    }

    public LevelResultsSnapshot BuildResultsSnapshot(
        LevelEndOutcome outcome,
        LevelEndReason reason
    )
    {
        var lines = new LevelGoalResultLine[_goals.Count];

        for (int i = 0; i < _goals.Count; i++)
        {
            LevelGoalRuntime goal = _goals[i];
            lines[i] = new LevelGoalResultLine
            {
                Description = goal.GetDisplayDescription(),
                Current = goal.Current,
                Required = goal.Config.RequiredAmount,
                Complete = goal.IsComplete,
                IsAdditional = goal.Config.IsAdditional,
            };
        }

        return new LevelResultsSnapshot
        {
            LevelName = _config != null
                ? _config.DisplayName
                : string.Empty,
            Outcome = outcome,
            Reason = reason,
            GoalLines = lines,
            ObjectsApproved = _objectsApproved,
            ActionsUsed = _actionsUsed,
            RageAccumulated = _rageAccumulated,
            AdditionalCompleted = _additionalCompleted,
            Rating = outcome == LevelEndOutcome.Success && _config != null
                ? _config.ComputeRating(_actionsUsed, _rageAccumulated, _additionalCompleted)
                : 0,
        };
    }
}
