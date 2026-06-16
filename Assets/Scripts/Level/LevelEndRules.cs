using System;
using UnityEngine;

[Serializable]
public struct LevelEndRules
{
    [Tooltip("Win when spawn queue is empty and pipeline has no objects.")]
    public bool WinOnPipelineDrained;

    [Tooltip("Win when every level goal is complete.")]
    public bool WinOnGoalsComplete;

    [Tooltip("Lose when the inspector rage bar reaches its maximum.")]
    public bool LoseOnRageBarFull;

    public static LevelEndRules Default =>
        new LevelEndRules
        {
            WinOnPipelineDrained = true,
            WinOnGoalsComplete = true,
            LoseOnRageBarFull = true,
        };
}
