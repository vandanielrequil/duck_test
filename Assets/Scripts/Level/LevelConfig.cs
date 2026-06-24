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
}

[System.Serializable]
public struct SpawnEntry
{
    public PipeObjectData Object;
}
