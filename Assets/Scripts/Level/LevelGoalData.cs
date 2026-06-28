using System;
using UnityEngine;

[Serializable]
public class LevelGoalData
{
    public GoalType Type;

    [Header("Approve")]
    public PipeObjectData ApproveTarget;
    public PipeArchetype TargetArchetype;
    public PipeObjectTag RequiredTags = PipeObjectTag.None;
    [Range(0, 3)] public int MinDuckiness = 3;

    [Header("Merge")]
    public PipeObjectData MergeInputA;
    public PipeObjectData MergeInputB;
    public PipeObjectData MergeInputC;
    public PipeObjectData MergeResult;

    [Header("Common")]
    public int RequiredAmount = 1;
    public string Description;

    [Tooltip("Additional goals are tracked and shown in UI but do not affect "
        + "level completion. They contribute to rating separately.")]
    public bool IsAdditional;
}
