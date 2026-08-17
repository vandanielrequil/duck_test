using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelConfig",
    menuName = "Level/Level Config"
)]
public class LevelConfig : ScriptableObject
{
    [Header("Meta")]
    public string LevelId = "level_01";
    public string DisplayName = "Level 1";

    [Header("Presentation")]
    [Tooltip("World-space background shown while this level is playing.")]
    public Sprite Background;

    [Header("Pipeline")]
    public float MoveInterval = 1.5f;

    [Header("Spawn queue")]
    public SpawnEntry[] SpawnQueue = System.Array.Empty<SpawnEntry>();

    [Tooltip("Slot where the queue head starts at level start. -1 = empty start "
        + "(default): objects spawn one-by-one from the tail. If >= 0, the "
        + "pipeline is pre-filled from this slot to the tail so the first queued "
        + "object sits here. Spawning afterwards still happens at the tail. "
        + "Slots: 0 = inspector/eject side ... last = tail/spawn end.")]
    public int QueueStartSlotIndex = -1;

    [Header("Goals")]
    public LevelGoalData[] Goals = System.Array.Empty<LevelGoalData>();

    [Header("Inspector")]
    public float BaseInspectionDuration = 5f;
    public int AngerThresholdTier1 = 3;
    public int AngerThresholdTier2 = 6;
    public int AngerMax = 6;

    [Header("End rules")]
    public LevelEndRules EndRules = LevelEndRules.Default;

    [Header("Rating (max actions inclusive)")]
    [Tooltip("3/3 if actions used is at most this value.")]
    public int MaxActionsForRating3 = 2;

    [Tooltip("2/3 if actions used is at most this value (and above Rating 3). "
        + "1/3 for anything above.")]
    public int MaxActionsForRating2 = 4;

    [Header("Rating (max rage inclusive)")]
    [Tooltip("3/3 if rage accumulated is at most this value.")]
    public int MaxRageForRating3 = 0;

    [Tooltip("2/3 if rage accumulated is at most this value (and above Rating 3). "
        + "1/3 for anything above.")]
    public int MaxRageForRating2 = 2;

    [Header("Rating (min additional goals completed)")]
    [Tooltip("3/3 requires at least this many additional goals completed. 0 = no requirement.")]
    public int MinAdditionalForRating3 = 0;

    [Tooltip("2/3 requires at least this many additional goals completed. 0 = no requirement.")]
    public int MinAdditionalForRating2 = 0;

    // Final rating = min(actions rating, rage rating, additional rating).
    public int ComputeRating(int actionsUsed, int rageAccumulated, int additionalCompleted)
    {
        int actionsRating    = ComputeActionsRating(actionsUsed);
        int rageRating       = ComputeRageRating(rageAccumulated);
        int additionalRating = ComputeAdditionalRating(additionalCompleted);
        return Mathf.Min(actionsRating, Mathf.Min(rageRating, additionalRating));
    }

    private int ComputeActionsRating(int actionsUsed)
    {
        if (actionsUsed <= MaxActionsForRating3) return 3;
        if (actionsUsed <= MaxActionsForRating2) return 2;
        return 1;
    }

    private int ComputeRageRating(int rageAccumulated)
    {
        if (rageAccumulated <= MaxRageForRating3) return 3;
        if (rageAccumulated <= MaxRageForRating2) return 2;
        return 1;
    }

    private int ComputeAdditionalRating(int additionalCompleted)
    {
        if (additionalCompleted >= MinAdditionalForRating3) return 3;
        if (additionalCompleted >= MinAdditionalForRating2) return 2;
        return 1;
    }

    private void OnValidate()
    {
        MaxActionsForRating3 = Mathf.Max(0, MaxActionsForRating3);
        MaxActionsForRating2 = Mathf.Max(
            MaxActionsForRating3,
            MaxActionsForRating2
        );
        MaxRageForRating3 = Mathf.Max(0, MaxRageForRating3);
        MaxRageForRating2 = Mathf.Max(
            MaxRageForRating3,
            MaxRageForRating2
        );
        MinAdditionalForRating3 = Mathf.Max(0, MinAdditionalForRating3);
        MinAdditionalForRating2 = Mathf.Clamp(
            MinAdditionalForRating2,
            0,
            MinAdditionalForRating3
        );
    }
}

[System.Serializable]
public struct SpawnEntry
{
    public PipeObjectData Object;
}
