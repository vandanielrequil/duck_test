using System;
using UnityEngine;

[Serializable]
public struct LevelEndRules
{
    [Tooltip("Win when spawn queue is empty and pipeline has no objects.")]
    public bool WinOnPipelineDrained;

    [Tooltip("Win when every level goal is complete.")]
    public bool WinOnGoalsComplete;

    [Tooltip("Lose after this many rage mode entries (anger reaches max).")]
    public int LoseOnRageModeCount;

    public static LevelEndRules Default =>
        new LevelEndRules
        {
            WinOnPipelineDrained = true,
            WinOnGoalsComplete = true,
            LoseOnRageModeCount = 3,
        };
}
