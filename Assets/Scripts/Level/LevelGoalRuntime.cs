using System;
using UnityEngine;

public class LevelGoalRuntime
{
    public readonly LevelGoalData Config;
    public int Current { get; private set; }

    public bool IsComplete => Current >= Config.RequiredAmount;

    public float ProgressRatio =>
        Config.RequiredAmount > 0
            ? (float)Current / Config.RequiredAmount
            : 1f;

    public LevelGoalRuntime(LevelGoalData config)
    {
        Config = config;
    }

    public void AddProgress(int delta = 1)
    {
        if (delta <= 0 || IsComplete)
            return;

        Current = Mathf.Min(
            Current + delta,
            Config.RequiredAmount
        );
    }

    public bool MatchesApprove(PipeObject obj, int duckiness)
    {
        if (obj?.State?.ObjectData == null)
            return false;

        if (duckiness < Config.MinDuckiness)
            return false;

        switch (Config.Type)
        {
            case GoalType.ApproveObject:
                return Config.ApproveTarget != null
                    && obj.State.ObjectData == Config.ApproveTarget;

            case GoalType.ApproveArchetype:
                if (Config.TargetArchetype != default
                    && obj.State.ObjectData.Archetype != Config.TargetArchetype)
                    return false;

                if (Config.RequiredTags != PipeObjectTag.None
                    && (obj.State.ObjectData.Tags & Config.RequiredTags)
                        != Config.RequiredTags)
                    return false;

                return true;

            default:
                return false;
        }
    }

    public bool MatchesMergePair(
        PipeObjectData inputA,
        PipeObjectData inputB,
        PipeObjectData inputC = null
    )
    {
        if (Config.Type != GoalType.MergePair)
            return false;

        if (inputA == null || inputB == null)
            return false;

        if (Config.MergeInputC != null)
        {
            if (inputC == null)
                return false;

            return MatchesMergeTriple(
                inputA,
                inputB,
                inputC,
                Config.MergeInputA,
                Config.MergeInputB,
                Config.MergeInputC
            );
        }

        return (inputA == Config.MergeInputA
                && inputB == Config.MergeInputB)
            || (inputA == Config.MergeInputB
                && inputB == Config.MergeInputA);
    }

    private static bool MatchesMergeTriple(
        PipeObjectData inputA,
        PipeObjectData inputB,
        PipeObjectData inputC,
        PipeObjectData cfgA,
        PipeObjectData cfgB,
        PipeObjectData cfgC
    )
    {
        return (inputA == cfgA && inputB == cfgB && inputC == cfgC)
            || (inputA == cfgA && inputB == cfgC && inputC == cfgB)
            || (inputA == cfgB && inputB == cfgA && inputC == cfgC)
            || (inputA == cfgB && inputB == cfgC && inputC == cfgA)
            || (inputA == cfgC && inputB == cfgA && inputC == cfgB)
            || (inputA == cfgC && inputB == cfgB && inputC == cfgA);
    }

    public bool MatchesMergeResult(PipeObjectData result)
    {
        if (Config.Type != GoalType.MergeToResult)
            return false;

        return result != null && result == Config.MergeResult;
    }

    public string GetDisplayDescription()
    {
        if (!string.IsNullOrEmpty(Config.Description))
            return Config.Description;

        return Config.Type switch
        {
            GoalType.ApproveObject =>
                $"Approve {Config.ApproveTarget?.name} x{Config.RequiredAmount}",
            GoalType.ApproveArchetype =>
                $"Approve {Config.TargetArchetype} x{Config.RequiredAmount}",
            GoalType.MergePair =>
                Config.MergeInputC != null
                    ? $"Merge {Config.MergeInputA?.name} + {Config.MergeInputB?.name} "
                      + $"+ {Config.MergeInputC?.name} x{Config.RequiredAmount}"
                    : $"Merge {Config.MergeInputA?.name} + {Config.MergeInputB?.name} "
                      + $"x{Config.RequiredAmount}",
            GoalType.MergeToResult =>
                $"Create {Config.MergeResult?.name} x{Config.RequiredAmount}",
            _ => Config.Type.ToString(),
        };
    }
}
