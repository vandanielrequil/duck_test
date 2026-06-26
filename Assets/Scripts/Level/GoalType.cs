using System;

public enum GoalType
{
    ApproveObject,
    ApproveArchetype,
    MergePair,
    MergeToResult,
}

public enum InspectionOutcome
{
    Approve,
    Reject,
}

public enum LevelEndReason
{
    GoalsComplete,
    PipelineDrained,
    RageBarFull,
}

public enum LevelEndOutcome
{
    Success,
    Fail,
}

[Serializable]
public struct LevelGoalResultLine
{
    public string Description;
    public int Current;
    public int Required;
    public bool Complete;
}

[Serializable]
public struct LevelResultsSnapshot
{
    public string LevelName;
    public LevelEndOutcome Outcome;
    public LevelEndReason Reason;
    public LevelGoalResultLine[] GoalLines;
    public int ObjectsApproved;
    public int ActionsUsed;
    public int Rating;
}
